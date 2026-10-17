using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class FakeTransport : IHidTransport
{
    public bool Ready = true;
    public Func<CancellationToken, Task>? BeforeSend;
    public bool NeutralChannelAvailableWhenNotReady;
    public int ReleaseCalls;
    public int ReleaseReportLimit = 2;
    public ConcurrentQueue<(string Target, HidFrame Frame)> Sent { get; } = new();
    public bool IsReady(string targetId) => Ready && targetId == "iPad";
    public async Task<int> ReleaseAsync(string targetId, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ReleaseCalls);
        if (!IsReady(targetId))
        {
            if (!NeutralChannelAvailableWhenNotReady) return 0;
            Sent.Enqueue((targetId, HidReports.Keyboard(0, []))); Sent.Enqueue((targetId, HidReports.Mouse(0))); return 2;
        }
        if (ReleaseReportLimit > 0) await SendAsync(targetId, HidReports.Keyboard(0, []), cancellationToken);
        if (ReleaseReportLimit > 1) await SendAsync(targetId, HidReports.Mouse(0), cancellationToken);
        return ReleaseReportLimit;
    }
    public async Task SendAsync(string targetId, HidFrame frame, CancellationToken cancellationToken)
    {
        if (BeforeSend is { } wait) await wait(cancellationToken);
        if (!IsReady(targetId)) throw new IOException("Link lost");
        Sent.Enqueue((targetId, frame));
    }
    public static async Task Until(Func<bool> condition, int timeoutMs = 3000)
    {
        var clock = Stopwatch.StartNew();
        while (!condition()) { if (clock.ElapsedMilliseconds > timeoutMs) throw new TimeoutException("Expected state was not reached"); await Task.Delay(5); }
    }
}

public sealed class RoutingTests
{
    [Fact] public async Task NoConnectionNeverSuppressesInput()
    {
        var fake = new FakeTransport { Ready = false }; await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        Assert.False(await router.ActivateAsync("iPad", () => true)); Assert.False(router.Key(0x41, 0x1E, false, true, 0)); Assert.False(router.Button(MouseButtons.Left, true)); Assert.False(router.IsRemote);
    }
    [Fact] public async Task LocalHeldKeysAndUnavailableHooksPreventActivation()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        router.Key(0xA2, 0x1D, false, true, 0); Assert.False(await router.ActivateAsync("iPad", () => true));
        router.Key(0xA2, 0x1D, false, false, 0); Assert.False(await router.ActivateAsync("iPad", () => false)); Assert.True(await router.ActivateAsync("iPad", () => true));
    }
    [Fact] public async Task KeyTransitionsAreOrderedAndRepeatIsOwnedByHost()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        Assert.True(await router.ActivateAsync("iPad", () => true));
        Assert.True(router.Key(0x41, 0x1E, false, true, 0)); Assert.True(router.Key(0x41, 0x1E, false, true, 1)); Assert.True(router.Key(0x41, 0x1E, false, false, 2));
        await FakeTransport.Until(() => fake.Sent.Count == 4);
        Assert.Equal(4, fake.Sent.Count); Assert.All(fake.Sent, entry => Assert.Equal("iPad", entry.Target));
        Assert.Equal(4, fake.Sent.ToArray()[2].Frame.Payload[2]); Assert.All(fake.Sent.Last().Frame.Payload, b => Assert.Equal(0, b));
    }
    [Fact] public async Task DisconnectDuringHeldKeyImmediatelyReturnsLocalAndQuarantinesKeyUp()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        await router.ActivateAsync("iPad", () => true); router.Key(0x41, 0x1E, false, true, 0); fake.Ready = false;
        router.CheckConnection(); Assert.False(router.IsRemote); Assert.True(router.Key(0x41, 0x1E, false, false, 1)); Assert.False(router.Key(0x42, 0x30, false, true, 2));
        fake.Ready = true; Assert.False(router.IsRemote); // Reconnect never restores suppression automatically.
    }
    [Fact] public async Task EmergencyChordBypassesBluetooth()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        await router.ActivateAsync("iPad", () => true);
        fake.BeforeSend = ct => Task.Delay(Timeout.Infinite, ct);
        router.Key(0xA2, 0x1D, false, true, 1); router.Key(0xA4, 0x38, false, true, 2);
        var watch = Stopwatch.StartNew(); Assert.True(router.Key(0x51, 0x10, false, true, 3));
        Assert.False(router.IsRemote); Assert.True(watch.ElapsedMilliseconds < 100);
        Assert.True(router.Key(0x51, 0x10, false, false, 4)); Assert.True(router.Key(0xA2, 0x1D, false, false, 5)); Assert.True(router.Key(0xA4, 0x38, false, false, 6));
    }
    [Fact] public async Task LinkLossDiscoveredByKeyUpDoesNotLeavePermanentQuarantine()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        await router.ActivateAsync("iPad", () => true); router.Key(0x41, 0x1E, false, true, 0); fake.Ready = false;
        Assert.True(router.Key(0x41, 0x1E, false, false, 1)); Assert.False(router.IsRemote); Assert.True(router.PhysicalIdle);
    }
    [Fact] public async Task ThreeSeparateEscapesRecoverButRepeatDoesNot()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        await router.ActivateAsync("iPad", () => true);
        router.Key(0x1B, 1, false, true, 1); router.Key(0x1B, 1, false, true, 2); Assert.True(router.IsRemote); router.Key(0x1B, 1, false, false, 3);
        router.Key(0x1B, 1, false, true, 100); router.Key(0x1B, 1, false, false, 101); router.Key(0x1B, 1, false, true, 200); Assert.False(router.IsRemote);
    }
    [Fact] public async Task ClickEdgesAreNeverCoalescedAway()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        await router.ActivateAsync("iPad", () => true); router.Button(MouseButtons.Left, true); router.Button(MouseButtons.Left, false);
        await FakeTransport.Until(() => fake.Sent.Count == 4);
        Assert.Equal(1, fake.Sent.ToArray()[2].Frame.Payload[0]); Assert.Equal(0, fake.Sent.ToArray()[3].Frame.Payload[0]);
    }
    [Fact] public async Task LocalReturnReleasesBothReports()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        await router.ActivateAsync("iPad", () => true); router.Button(MouseButtons.Left, true);
        await FakeTransport.Until(() => fake.Sent.Count == 3); router.GoLocal();
        await FakeTransport.Until(() => fake.Sent.Count == 5); Assert.False(router.IsRemote);
        var last = fake.Sent.TakeLast(2).ToArray(); Assert.Equal(ReportKind.Keyboard, last[0].Frame.Kind); Assert.All(last[0].Frame.Payload, b => Assert.Equal(0, b)); Assert.All(last[1].Frame.Payload, b => Assert.Equal(0, b));
    }
    [Fact] public async Task QueueBackpressureFailsOpen()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake, 2); var router = new InputRouter(fake, dispatch);
        await router.ActivateAsync("iPad", () => true); fake.BeforeSend = ct => Task.Delay(Timeout.Infinite, ct);
        for (var i = 0; i < 20; i++) { router.Key(0x41, 0x1E, false, true, i); router.Key(0x41, 0x1E, false, false, i); }
        Assert.False(router.IsRemote); Assert.False(dispatch.Healthy); Assert.False(await router.ActivateAsync("iPad", () => true));
    }
    [Fact] public async Task NotifyTimeoutCannotStrandLocalInput()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        await router.ActivateAsync("iPad", () => true); fake.BeforeSend = _ => Task.Delay(5000); router.Key(0x41, 0x1E, false, true, 0);
        await FakeTransport.Until(() => !router.IsRemote); Assert.False(dispatch.Healthy);
    }
    [Fact] public async Task RecoveryCancelsAnActivationStillWaitingForNeutralReports()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new FakeTransport { BeforeSend = async ct => { entered.TrySetResult(); await gate.Task.WaitAsync(ct); } };
        await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        var activation = router.ActivateAsync("iPad", () => true); await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        router.GoLocal(); gate.TrySetResult(); Assert.False(await activation); Assert.False(router.IsRemote);
    }
    [Fact] public async Task EventsReleasedOnSecureDesktopCanBeReconciled()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        await router.ActivateAsync("iPad", () => true); router.Key(0x41, 0x1E, false, true, 0); router.GoLocal();
        Assert.False(router.PhysicalIdle); router.ReconcilePhysical(_ => false); Assert.True(router.PhysicalIdle);
    }
    [Fact] public async Task ReturningLocalInvalidatesQueuedKeysBeforeNeutralReports()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        await router.ActivateAsync("iPad", () => true);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.BeforeSend = async ct => { entered.TrySetResult(); await resume.Task.WaitAsync(ct); };
        router.Key(0x41, 0x1E, false, true, 0); await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        router.Key(0x42, 0x30, false, true, 1); router.GoLocal(); resume.SetResult();
        await FakeTransport.Until(() => !dispatch.Settling && fake.Sent.Count >= 5);
        Assert.DoesNotContain(fake.Sent, item => item.Frame.Kind == ReportKind.Keyboard && item.Frame.Payload.Skip(2).Contains((byte)5));
        Assert.All(fake.Sent.Last().Frame.Payload, b => Assert.Equal(0, b));
    }
    [Fact] public async Task NeutralReleaseCanUseRemainingSubscriptionAfterCapturePermissionIsRevoked()
    {
        var fake = new FakeTransport { NeutralChannelAvailableWhenNotReady = true }; await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        await router.ActivateAsync("iPad", () => true); router.Key(0x41, 0x1E, false, true, 0);
        await FakeTransport.Until(() => fake.Sent.Count == 3); fake.Ready = false; router.CheckConnection();
        await FakeTransport.Until(() => fake.Sent.Count == 5); Assert.Equal(2, fake.ReleaseCalls); Assert.False(router.IsRemote);
        Assert.All(fake.Sent.TakeLast(2), item => Assert.All(item.Frame.Payload, value => Assert.Equal(0, value)));
    }
    [Fact] public async Task MotionFloodIsBoundedAndMeasuredSeparatelyFromBluetooth()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        await router.ActivateAsync("iPad", () => true); const int count = 200_000;
        var before = GC.GetAllocatedBytesForCurrentThread(); var clock = Stopwatch.StartNew();
        for (var i = 0; i < count; i++) router.Move(1, -1);
        clock.Stop(); var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        for (var i = 0; i < 7; i++) router.FlushMotion();
        await FakeTransport.Until(() => fake.Sent.Count == 9);
        var movement = fake.Sent.Skip(2).ToArray();
        Assert.Equal(count, movement.Sum(f => (int)System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(f.Frame.Payload.AsSpan(1))));
        Assert.True(router.IsRemote); Assert.True(allocated < 1024 * 1024);
        var output = Environment.GetEnvironmentVariable("UNIVERSALBRIDGE_ARTIFACTS");
        if (output is not null)
        {
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, "local-performance.json"), JsonSerializer.Serialize(new { Events = count, ElapsedMs = clock.Elapsed.TotalMilliseconds,
                MeanMicroseconds = clock.Elapsed.TotalMicroseconds / count, AllocatedBytes = allocated, BluetoothMeasured = false, Notes = "In-process fake transport; excludes native hooks, Windows scheduling and radio" }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
