using System.Text.Json;
using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class EdgeReturnTests
{
    private sealed class Clock : TimeProvider { public long Now; public override long TimestampFrequency => 1000; public override long GetTimestamp() => Now; }
    private static (int X, int Y) Toward(ScreenEdge edge, int amount) => edge switch
    { ScreenEdge.Left => (amount, 0), ScreenEdge.Right => (-amount, 0), ScreenEdge.Top => (0, amount), _ => (0, -amount) };
    private static void Move(InputRouter router, ScreenEdge edge, int amount)
    { var (x, y) = Toward(edge, amount); router.Move(x, y); router.FlushMotion(); }
    private static void ConfirmPush(InputRouter router, Clock clock, ScreenEdge edge)
    {
        clock.Now = Math.Max(clock.Now + 200, 1000); Move(router, edge, 10000); Assert.True(router.IsRemote);
        for (var sample = 1; sample <= 3; sample++)
        {
            // Real report timers also flush empty batches between physical input samples.
            for (var tick = 0; tick < 4; tick++) { clock.Now += 20; router.FlushMotion(); Assert.True(router.IsRemote); }
            clock.Now += 20; Move(router, edge, 32); Assert.Equal(sample < 3, router.IsRemote);
        }
    }

    [Theory]
    [InlineData(ScreenEdge.Right)][InlineData(ScreenEdge.Left)][InlineData(ScreenEdge.Top)][InlineData(ScreenEdge.Bottom)]
    public async Task AutomaticSwitchReturnsInEveryDirectionAndNeutralizes(ScreenEdge edge)
    {
        var fake = new FakeTransport(); var clock = new Clock(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender, clock);
        router.Configure(new() { AutoSwitch = true, Edge = edge, StabilizeFastPointer = false });
        Assert.True(await router.ActivateAsync("iPad", () => true));
        Move(router, edge, 30); Assert.True(router.IsRemote);
        Move(router, edge, 20); Assert.True(router.IsRemote); // The v1.01 false-return regression.
        ConfirmPush(router, clock, edge); Assert.True(router.LastReturnAtEdge);
        await FakeTransport.Until(() => fake.ReleaseCalls == 2 && !sender.Settling);
        Assert.True(fake.Sent.Count >= 4);
        Assert.All(fake.Sent.ToArray()[^2..], item => Assert.All(item.Frame.Payload, value => Assert.Equal(0, value)));
        Assert.True(router.PhysicalIdle);
        Assert.True(await router.ActivateAsync("iPad", () => true));
        Assert.False(router.LastReturnAtEdge); router.FlushMotion(); Assert.True(router.IsRemote);
    }

    [Theory]
    [InlineData(false, true)][InlineData(true, false)][InlineData(false, false)]
    public async Task DisabledSwitchOrReturnPreservesManualControl(bool automatic, bool returning)
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender);
        router.Configure(new() { AutoSwitch = automatic, ReturnAtEdge = returning, StabilizeFastPointer = false });
        Assert.True(await router.ActivateAsync("iPad", () => true)); Move(router, ScreenEdge.Right, 100);
        Assert.True(router.IsRemote); router.GoLocal(); Assert.False(router.IsRemote); Assert.False(router.LastReturnAtEdge);
    }

    [Theory]
    [InlineData(ScreenEdge.Right)][InlineData(ScreenEdge.Left)][InlineData(ScreenEdge.Top)][InlineData(ScreenEdge.Bottom)]
    public async Task DragReleaseIdleScrollAndAwayMotionCannotTriggerReturn(ScreenEdge edge)
    {
        var fake = new FakeTransport(); var clock = new Clock(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender, clock);
        router.Configure(new() { AutoSwitch = true, Edge = edge, StabilizeFastPointer = false });
        Assert.True(await router.ActivateAsync("iPad", () => true));
        clock.Now = 2000;
        router.Button(MouseButtons.Left, true); Move(router, edge, 10000); Assert.True(router.IsRemote);
        router.Button(MouseButtons.Left, false); router.FlushMotion(); Assert.True(router.IsRemote);
        router.Scroll(120); router.FlushMotion(); Assert.True(router.IsRemote);
        Move(router, edge, -1); Assert.True(router.IsRemote);
        Move(router, edge, 1); Assert.True(router.IsRemote);
        ConfirmPush(router, clock, edge);
    }

    [Fact] public async Task KeyReleaseDoesNotReturnUsingMotionAccumulatedWhileHeld()
    {
        var fake = new FakeTransport(); var clock = new Clock(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender, clock);
        router.Configure(new() { AutoSwitch = true, StabilizeFastPointer = false });
        Assert.True(await router.ActivateAsync("iPad", () => true));
        clock.Now = 2000;
        router.Key(0x41, 0x1E, false, true, 0); router.Move(-10000, 0);
        router.Key(0x41, 0x1E, false, false, 1); router.FlushMotion(); Assert.True(router.IsRemote);
        router.Move(-1, 0); router.FlushMotion(); Assert.True(router.IsRemote);
        ConfirmPush(router, clock, ScreenEdge.Right);
    }

    [Theory]
    [InlineData(ScreenEdge.Right)][InlineData(ScreenEdge.Left)][InlineData(ScreenEdge.Top)][InlineData(ScreenEdge.Bottom)]
    public async Task SmallReversalsAfterActivationNeverBounceToWindows(ScreenEdge edge)
    {
        var fake = new FakeTransport(); var clock = new Clock(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender, clock);
        router.Configure(new() { AutoSwitch = true, Edge = edge, StabilizeFastPointer = false });
        Assert.True(await router.ActivateAsync("iPad", () => true));
        foreach (var time in new[] { 0, 500, 1000, 5000 })
        { clock.Now = time; Move(router, edge, 80); Move(router, edge, -80); Assert.True(router.IsRemote); }
    }

    [Theory]
    [InlineData(ScreenEdge.Right)][InlineData(ScreenEdge.Left)][InlineData(ScreenEdge.Top)][InlineData(ScreenEdge.Bottom)]
    public async Task EntryFlickAndIdleCannotCauseImmediateOrDelayedReturn(ScreenEdge edge)
    {
        var fake = new FakeTransport(); var clock = new Clock(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender, clock);
        router.Configure(new() { AutoSwitch = true, Edge = edge, StabilizeFastPointer = false });
        Assert.True(await router.ActivateAsync("iPad", () => true));
        for (var time = 0; time < 1000; time += 100)
        { clock.Now = time; Move(router, edge, 10000); Assert.True(router.IsRemote); }
        clock.Now = 5000; router.FlushMotion(); Assert.True(router.IsRemote);
        Move(router, edge, 10000); Assert.True(router.IsRemote); // A flick after the grace is not a confirmed push either.
        clock.Now = 10000; router.FlushMotion(); Assert.True(router.IsRemote);
        Move(router, edge, 1); Assert.True(router.IsRemote);
        ConfirmPush(router, clock, edge);
    }

    [Fact] public async Task TinyBoundaryJitterNeedsDistanceEvenWhenDwellHasElapsed()
    {
        var fake = new FakeTransport(); var clock = new Clock(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender, clock);
        router.Configure(new() { AutoSwitch = true, StabilizeFastPointer = false });
        Assert.True(await router.ActivateAsync("iPad", () => true));
        clock.Now = 1000; Move(router, ScreenEdge.Right, 10000);
        // A large arrival must not supply the distance needed by later tiny jitter.
        for (var i = 0; i < 40; i++) { clock.Now += 100; Move(router, ScreenEdge.Right, 1); Assert.True(router.IsRemote); }
        ConfirmPush(router, clock, ScreenEdge.Right);
    }

    [Theory]
    [InlineData(150, 300)][InlineData(450, 450)][InlineData(1500, 1500)]
    public async Task ReturnWaitHonorsLayoutSettingAndMinimumSafetyDwell(int configuredMs, int expectedMs)
    {
        var fake = new FakeTransport(); var clock = new Clock(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender, clock);
        router.Configure(new() { AutoSwitch = true, EdgeDwellMs = configuredMs, StabilizeFastPointer = false });
        Assert.True(await router.ActivateAsync("iPad", () => true));
        clock.Now = 1000; Move(router, ScreenEdge.Right, 10000); Assert.True(router.IsRemote);
        for (var elapsed = 50; elapsed <= expectedMs; elapsed += 50)
        { clock.Now = 1000 + elapsed; Move(router, ScreenEdge.Right, 32); Assert.Equal(elapsed < expectedMs, router.IsRemote); }
    }

    [Theory]
    [InlineData("reverse")][InlineData("parallel")][InlineData("scroll")][InlineData("key")][InlineData("button")][InlineData("pause")]
    public async Task InterruptedPressureMustStartAgain(string interruption)
    {
        var fake = new FakeTransport(); var clock = new Clock(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender, clock);
        router.Configure(new() { AutoSwitch = true, StabilizeFastPointer = false });
        Assert.True(await router.ActivateAsync("iPad", () => true));
        clock.Now = 1000; Move(router, ScreenEdge.Right, 10000);
        clock.Now = 1100; Move(router, ScreenEdge.Right, 64);
        clock.Now = 1200;
        switch (interruption)
        {
            case "reverse": Move(router, ScreenEdge.Right, -1); break;
            case "parallel": router.Move(0, 1); router.FlushMotion(); break;
            case "scroll": router.Scroll(120); router.FlushMotion(); break;
            case "key": router.Key(0x41, 0x1E, false, true, 0); router.Key(0x41, 0x1E, false, false, 1); break;
            case "button": router.Button(MouseButtons.Left, true); router.Button(MouseButtons.Left, false); break;
            case "pause": clock.Now = 1400; router.FlushMotion(); break;
        }
        clock.Now += 100; Move(router, ScreenEdge.Right, 1); Assert.True(router.IsRemote);
        ConfirmPush(router, clock, ScreenEdge.Right);
    }

    [Fact] public async Task IdleTimerCannotFinishAnOtherwiseReadyDwell()
    {
        var fake = new FakeTransport(); var clock = new Clock(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender, clock);
        router.Configure(new() { AutoSwitch = true, StabilizeFastPointer = false });
        Assert.True(await router.ActivateAsync("iPad", () => true));
        clock.Now = 1000; Move(router, ScreenEdge.Right, 10000);
        clock.Now = 1100; Move(router, ScreenEdge.Right, 96);
        clock.Now = 1200; Move(router, ScreenEdge.Right, 1);
        clock.Now = 1250; Move(router, ScreenEdge.Right, 1);
        clock.Now = 1300; router.FlushMotion(); Assert.True(router.IsRemote);
        clock.Now = 1350; Move(router, ScreenEdge.Right, 1); Assert.False(router.IsRemote);
    }

    [Theory]
    [InlineData(ScreenEdge.Right)][InlineData(ScreenEdge.Left)][InlineData(ScreenEdge.Top)][InlineData(ScreenEdge.Bottom)]
    public void ReturnMapsAlongPortalAndLandsInsideWindowsWithoutRetriggering(ScreenEdge edge)
    {
        var display = new DisplayInfo("negative", -1920, -1080, 1920, 1080);
        var settings = new AppSettings { Edge = edge, PortalStart = .2, PortalEnd = .8 };
        var cursor = new VirtualCursor(); cursor.Enter(settings, .25);
        cursor.Move(edge is ScreenEdge.Top or ScreenEdge.Bottom ? (settings.VirtualWidth - 1) * .5 : 0,
            edge is ScreenEdge.Left or ScreenEdge.Right ? (settings.VirtualHeight - 1) * .5 : 0, settings);
        var point = cursor.WindowsReturnPoint(display, settings);
        Assert.True(display.Contains(point.X, point.Y));
        Assert.InRange(VirtualCursor.EntryAlong(point, display, settings), .748, .752);
        Assert.False(EdgeDetector.IsAtPortal(point, display, [display], settings, EdgeDetector.RetentionTolerance));
        var detector = new EdgeDetector(); detector.Reset();
        Assert.False(detector.Poll(point, display, [display], settings, 1000));
        Assert.False(detector.Observe(point, display, [display], settings, 2000));
    }

    [Fact] public void LegacySettingsEnableReturnAndRemoveContactRelayWithoutChangingOtherPreferences()
    {
        var previous = JsonSerializer.Deserialize<AppSettings>("""{"AutoSwitch":true,"EstimatedReturn":false,"NativeTouchpad":true,"CaptureTouchpad":false,"MouseSensitivity":0.84,"VirtualWidth":1920,"VirtualHeight":1080}""")!.Normalize();
        Assert.True(previous.ReturnAtEdge); Assert.True(previous.AutoSwitch); Assert.False(previous.CaptureTouchpad);
        Assert.Equal(.84, previous.MouseSensitivity); Assert.Equal(1920, previous.VirtualWidth);
        var saved = JsonSerializer.Serialize(previous);
        Assert.DoesNotContain("EstimatedReturn", saved); Assert.DoesNotContain("NativeTouchpad", saved);
        var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(previous with { ReturnAtEdge = false }))!;
        Assert.False(restored.ReturnAtEdge);
        Assert.Equal(new[] { ReportKind.Keyboard, ReportKind.Mouse }, Enum.GetValues<ReportKind>());
        Assert.Equal(144, HidDescriptors.ReportMapValue.Length);
    }
}
