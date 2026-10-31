using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class ReturnPrecisionTests
{
    private sealed class Clock : TimeProvider { public long Now; public override long TimestampFrequency => 1000; public override long GetTimestamp() => Now; }
    private static (double X, double Y) Motion(ScreenEdge edge, double normal, double tangent = 0) => edge switch
    { ScreenEdge.Right => (-normal, tangent), ScreenEdge.Left => (normal, tangent), ScreenEdge.Top => (tangent, normal), _ => (tangent, -normal) };
    private static void Move(InputRouter router, ScreenEdge edge, double normal, double tangent = 0)
    { var delta = Motion(edge, normal, tangent); router.Move(delta.X, delta.Y); }
    private static void ReachBoundary(InputRouter router, AppSettings settings)
    { var delta = Motion(settings.Edge, 100_000); router.Cursor.Move(delta.X, delta.Y, settings); }

    [Theory]
    [InlineData(ScreenEdge.Right, .02, true)][InlineData(ScreenEdge.Left, .02, false)]
    [InlineData(ScreenEdge.Top, .10, true)][InlineData(ScreenEdge.Bottom, .84, true)]
    [InlineData(ScreenEdge.Right, 4, true)]
    public async Task DeliberateDiagonalPressureUsesPhysicalCountsAtEverySensitivity(ScreenEdge edge, double sensitivity, bool stabilize)
    {
        var transport = new FakeTransport(); var clock = new Clock(); await using var sender = new ReportDispatcher(transport);
        var router = new InputRouter(transport, sender, clock); var settings = new AppSettings { AutoSwitch = true, Edge = edge, MouseSensitivity = sensitivity, StabilizeFastPointer = stabilize };
        router.Configure(settings); Assert.True(await router.ActivateAsync("iPad", () => true)); ReachBoundary(router, settings);
        clock.Now = 1000; Move(router, edge, 10, 20); router.FlushMotion(); Assert.True(router.IsRemote);
        for (var index = 1; index <= 3; index++)
        {
            clock.Now = 1000 + index * 100;
            // The last packet in a batch may be a sensor reversal. Its sign must not erase the net push.
            Move(router, edge, 34, 40); Move(router, edge, -2, 1); router.FlushMotion();
            Assert.Equal(index < 3, router.IsRemote);
        }
        Assert.True(router.LastReturnAtEdge);
    }
    [Theory] [InlineData(ScreenEdge.Right, .02)][InlineData(ScreenEdge.Left, 1)][InlineData(ScreenEdge.Top, 4)][InlineData(ScreenEdge.Bottom, .84)]
    public async Task SmallReverseSampleDoesNotDiscardAnOtherwiseContinuousPush(ScreenEdge edge, double sensitivity)
    {
        var transport = new FakeTransport(); var clock = new Clock(); await using var sender = new ReportDispatcher(transport);
        var router = new InputRouter(transport, sender, clock); var settings = new AppSettings { AutoSwitch = true, Edge = edge, MouseSensitivity = sensitivity, StabilizeFastPointer = false };
        router.Configure(settings); Assert.True(await router.ActivateAsync("iPad", () => true)); ReachBoundary(router, settings);
        clock.Now = 1000; Move(router, edge, 10); router.FlushMotion();
        clock.Now = 1100; Move(router, edge, 48, 10); router.FlushMotion();
        clock.Now = 1150; Move(router, edge, -1); router.FlushMotion(); Assert.True(router.IsRemote);
        clock.Now = 1200; Move(router, edge, 32, 20); router.FlushMotion(); Assert.True(router.IsRemote);
        clock.Now = 1300; Move(router, edge, 17, 20); router.FlushMotion(); Assert.False(router.IsRemote);
    }
    [Fact] public async Task SlowerTouchpadSamplingCanFinishButAnActualPauseCannot()
    {
        var guard = new EdgeReturnGuard(); var settings = new AppSettings { AutoSwitch = true };
        Assert.False(guard.Observe(true, -1000, 1000, true, true, settings, 1000));
        Assert.False(guard.Observe(true, -48, 96, true, true, settings, 1150));
        Assert.True(guard.Observe(true, -48, 96, true, true, settings, 1300));
        guard.Reset();
        Assert.False(guard.Observe(true, -1000, 0, true, true, settings, 2000));
        Assert.False(guard.Observe(true, -48, 0, true, true, settings, 2100));
        Assert.False(guard.Observe(true, -10000, 0, true, true, settings, 2400));
        await Task.CompletedTask;
    }
    [Fact] public void RepeatedRetreatOrAlongEdgeTravelStillCancelsPressure()
    {
        var guard = new EdgeReturnGuard(); var settings = new AppSettings { AutoSwitch = true };
        Assert.False(guard.Observe(true, -1000, 0, true, true, settings, 1000));
        Assert.False(guard.Observe(true, -80, 0, true, true, settings, 1100));
        for (var time = 1120; time <= 1200; time += 20) Assert.False(guard.Observe(true, 1, 0, true, true, settings, time));
        Assert.False(guard.Pending);
        Assert.False(guard.Observe(true, -1000, 0, true, true, settings, 1300));
        Assert.False(guard.Observe(true, 0, 50, true, true, settings, 1400)); Assert.False(guard.Pending);
    }
    [Fact] public void AlternatingSensorNoiseCannotAccumulateNetPressure()
    {
        var guard = new EdgeReturnGuard(); var settings = new AppSettings { AutoSwitch = true };
        Assert.False(guard.Observe(true, -1000, 0, true, true, settings, 1000));
        for (var index = 1; index <= 400; index++)
            Assert.False(guard.Observe(true, index % 2 == 0 ? 2 : -2, 0, true, true, settings, 1000 + index * 20));
    }
    [Theory] [InlineData("wheel")][InlineData("key")][InlineData("button")]
    public async Task InteractionAndMotionInTheSameBatchCannotTriggerReturn(string interaction)
    {
        var transport = new FakeTransport(); var clock = new Clock(); await using var sender = new ReportDispatcher(transport);
        var router = new InputRouter(transport, sender, clock); var settings = new AppSettings { AutoSwitch = true, StabilizeFastPointer = false };
        router.Configure(settings); Assert.True(await router.ActivateAsync("iPad", () => true)); ReachBoundary(router, settings);
        clock.Now = 1000; Move(router, ScreenEdge.Right, 10); router.FlushMotion();
        clock.Now = 1100; Move(router, ScreenEdge.Right, 80); router.FlushMotion();
        clock.Now = 1200; Move(router, ScreenEdge.Right, 80); router.FlushMotion();
        clock.Now = 1300;
        if (interaction == "wheel") router.Scroll(120);
        else if (interaction == "key") { router.Key(0x41, 0x1E, false, true, 0); router.Key(0x41, 0x1E, false, false, 1); }
        else { router.Button(MouseButtons.Left, true); router.Button(MouseButtons.Left, false); }
        Move(router, ScreenEdge.Right, 32); router.FlushMotion(); Assert.True(router.IsRemote);
    }
}
