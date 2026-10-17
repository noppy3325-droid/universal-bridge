using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class SleepRecoveryTests
{
    private sealed class Session { public bool Active = true; }

    [Fact] public void SuspendSurvivesPollingUntilResumeOnTheSameConnection()
    {
        var state = new HidSuspensionState<Session>(); var session = new Session();
        state.Set("iPad", session, true);
        for (var i = 0; i < 10; i++) Assert.True(state.Observe("IPAD", s => s.Active, true));
        state.Set("iPad", session, false);
        Assert.False(state.Observe("iPad", s => s.Active, true));
    }

    [Fact] public void NewConnectionDoesNotInheritSuspendWithoutAnExitSuspendWrite()
    {
        var state = new HidSuspensionState<Session>(); var old = new Session();
        state.Set("iPad", old, true); old.Active = false;
        Assert.False(state.Observe("iPad", s => s.Active, true));
        Assert.False(state.Observe("iPad", s => s.Active, true));
        var next = new Session(); state.Set("iPad", next, true);
        Assert.True(state.Observe("iPad", s => s.Active, true));
    }

    [Fact] public void UnsubscribeAndDisconnectClearOnlyThatPeersSuspend()
    {
        var state = new HidSuspensionState<Session>(); var session = new Session();
        state.Set("iPad", session, true); state.Set("other", new Session(), true);
        Assert.False(state.Observe("iPad", s => s.Active, false));
        Assert.True(state.Observe("other", s => s.Active, true));
        state.Reset("other"); Assert.False(state.Observe("other", s => s.Active, true));
    }

    [Fact] public async Task FailedSendRecoversOnlyOnExplicitActivationAndReleasesOldState()
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake);
        var router = new InputRouter(fake, sender); Assert.True(await router.ActivateAsync("iPad", () => true));
        fake.BeforeSend = _ => { fake.Ready = false; throw new IOException("iPad sleeping"); };
        router.Key(0x41, 0x1E, false, true, 0);
        await FakeTransport.Until(() => !sender.Healthy && !router.IsRemote && !sender.Settling);
        router.Key(0x41, 0x1E, false, false, 1);
        fake.BeforeSend = null; fake.Ready = true; router.CheckConnection();
        Assert.False(router.IsRemote); Assert.False(sender.Healthy);
        Assert.Equal(ActivationStatus.Activated, await router.TryActivateAsync("iPad", () => true));
        Assert.True(sender.Healthy); Assert.All(fake.Sent.TakeLast(2), item => Assert.All(item.Frame.Payload, b => Assert.Equal(0, b)));
        router.Key(0x42, 0x30, false, true, 2); router.Key(0x42, 0x30, false, false, 3);
        await FakeTransport.Until(() => fake.Sent.Count == 6);
        Assert.DoesNotContain(fake.Sent, item => item.Frame.Payload.Skip(2).Contains((byte)4));
        Assert.Equal(5, fake.Sent.ToArray()[4].Frame.Payload[2]);
    }

    [Theory] [InlineData(0)] [InlineData(1)]
    public async Task RecoveryRequiresBothNeutralReports(int released)
    {
        var fake = new FakeTransport { BeforeSend = _ => throw new IOException("Link lost") };
        await using var sender = new ReportDispatcher(fake);
        sender.Enqueue("iPad", 0, HidReports.Keyboard(0, [4]));
        await FakeTransport.Until(() => !sender.Healthy);
        fake.BeforeSend = null; fake.ReleaseReportLimit = released;
        Assert.False(await sender.TryRecoverAsync("iPad")); Assert.False(sender.Healthy);
        fake.ReleaseReportLimit = 2;
        Assert.True(await sender.TryRecoverAsync("iPad")); Assert.True(sender.Healthy);
    }

    [Fact] public async Task UnfinishedTimedOutSendCannotBeRecoveredOrReplayIntoNewSession()
    {
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new FakeTransport { BeforeSend = _ => blocked.Task };
        await using var sender = new ReportDispatcher(fake);
        sender.Enqueue("iPad", 0, HidReports.Keyboard(0, [4]));
        await FakeTransport.Until(() => !sender.Healthy);
        Assert.False(await sender.TryRecoverAsync("iPad"));
        blocked.SetResult(); await FakeTransport.Until(() => fake.Sent.Count == 1);
        fake.BeforeSend = null;
        Assert.True(await sender.TryRecoverAsync("iPad"));
        Assert.All(fake.Sent.TakeLast(2), item => Assert.All(item.Frame.Payload, b => Assert.Equal(0, b)));
    }

    [Fact] public async Task ReturnRequestDuringRecoveryCancelsActivation()
    {
        var fake = new FakeTransport { BeforeSend = _ => throw new IOException("Link lost") };
        await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender);
        sender.Enqueue("iPad", 0, HidReports.Keyboard(0, [4]));
        await FakeTransport.Until(() => !sender.Healthy);
        fake.BeforeSend = _ => { router.GoLocal(); return Task.CompletedTask; };
        Assert.False(await router.ActivateAsync("iPad", () => true)); Assert.False(router.IsRemote);
    }

    [Fact] public async Task InitialActivationAlsoRequiresBothNeutralReports()
    {
        var fake = new FakeTransport { ReleaseReportLimit = 1 };
        await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender);
        Assert.False(await router.ActivateAsync("iPad", () => true)); Assert.False(router.IsRemote);
        fake.ReleaseReportLimit = 2;
        Assert.True(await router.ActivateAsync("iPad", () => true));
    }
}
