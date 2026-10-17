using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class InputReadinessTests
{
    [Fact]
    public async Task PersistentImeFlagsDoNotBlockAnOtherwiseIdleActivation()
    {
        bool Idle() => PhysicalInputState.IsIdle(key => key is 0xF0 or 0xF4);
        var fake = new FakeTransport(); await using var dispatcher = new ReportDispatcher(fake);
        var router = new InputRouter(fake, dispatcher);
        Assert.True(Idle());
        Assert.Equal(ActivationStatus.Activated, await router.TryActivateAsync("iPad", Idle));
        Assert.True(router.IsRemote);
        Assert.Equal(2, fake.Sent.Count);
    }

    [Theory]
    [InlineData(0x41)] [InlineData(0xA2)] [InlineData(0x11)] [InlineData(0xA5)]
    [InlineData(1)] [InlineData(2)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    [InlineData(0x1C)] [InlineData(0x1D)] [InlineData(0x87)] [InlineData(0xAF)]
    public void ActualHeldInputStillBlocksAlongsideImeFlags(int heldKey)
    {
        Assert.False(PhysicalInputState.IsIdle(key => key == heldKey || key is 0xF0 or 0xF4));
    }

    [Fact]
    public async Task UnmappedPhysicalHookEventStillBlocksActivation()
    {
        var fake = new FakeTransport(); await using var dispatcher = new ReportDispatcher(fake);
        var router = new InputRouter(fake, dispatcher);
        router.Key(0xF0, 0x7F, false, true, 0);
        Assert.Equal(ActivationStatus.InputHeld, await router.TryActivateAsync("iPad", () => true));
        Assert.False(router.IsRemote); Assert.Empty(fake.Sent);
        router.Key(0xF0, 0x7F, false, false, 1);
        Assert.Equal(ActivationStatus.Activated, await router.TryActivateAsync("iPad", () => true));
    }

    [Fact]
    public async Task RefusalDistinguishesConnectionFromCaptureFailure()
    {
        var fake = new FakeTransport { Ready = false }; await using var dispatcher = new ReportDispatcher(fake);
        var router = new InputRouter(fake, dispatcher);
        Assert.Equal(ActivationStatus.Disconnected, await router.TryActivateAsync("iPad", () => true));
        fake.Ready = true;
        Assert.Equal(ActivationStatus.CaptureUnavailable, await router.TryActivateAsync("iPad", () => false));
        Assert.False(router.IsRemote); Assert.Empty(fake.Sent);
    }

    [Fact]
    public async Task RechecksCaptureAndCancellationAfterNeutralReports()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new FakeTransport { BeforeSend = async ct => { entered.TrySetResult(); await resume.Task.WaitAsync(ct); } };
        await using var dispatcher = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatcher);
        var capture = true;
        var activation = router.TryActivateAsync("iPad", () => capture);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(ActivationStatus.AlreadyActivating, await router.TryActivateAsync("iPad", () => true));
        capture = false; resume.SetResult();
        Assert.Equal(ActivationStatus.CaptureUnavailable, await activation); Assert.False(router.IsRemote);
        fake.BeforeSend = _ => { router.GoLocal(); return Task.CompletedTask; };
        Assert.Equal(ActivationStatus.Cancelled, await router.TryActivateAsync("iPad", () => true));
    }
}
