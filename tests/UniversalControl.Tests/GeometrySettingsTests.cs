using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class GeometrySettingsTests
{
    private readonly DisplayInfo _display = new("main", -1920, 100, 1920, 1080, true);
    [Fact] public void EdgeNeedsContinuousPressureAndLeavingAfterActivation()
    {
        var detector = new EdgeDetector(); var s = new AppSettings(); var p = new PixelPoint(-1, 500);
        Assert.False(detector.Observe(p, _display, [_display], s, 0));
        Assert.False(detector.Observe(p, _display, [_display], s, 299));
        Assert.True(detector.Observe(p, _display, [_display], s, 300));
        Assert.False(detector.Observe(p, _display, [_display], s, 2000));
        Assert.False(detector.Observe(new(-20, 500), _display, [_display], s, 2001));
        Assert.False(detector.Observe(p, _display, [_display], s, 2100));
        Assert.True(detector.Observe(p, _display, [_display], s, 2400));
    }
    [Fact] public void AdjacentWindowsMonitorBlocksOnlySharedEdge()
    {
        var adjacent = new DisplayInfo("other", 0, 100, 2560, 600); var detector = new EdgeDetector(); var s = new AppSettings();
        Assert.False(detector.Observe(new(-1, 500), _display, [_display, adjacent], s, 0));
        Assert.False(detector.Observe(new(-1, 500), _display, [_display, adjacent], s, 1000));
        Assert.False(detector.Observe(new(-1, 900), _display, [_display, adjacent], s, 1100));
        Assert.True(detector.Observe(new(-1, 900), _display, [_display, adjacent], s, 1400));
    }
    [Theory]
    [InlineData(ScreenEdge.Left, -1920, 500)] [InlineData(ScreenEdge.Top, -1000, 100)] [InlineData(ScreenEdge.Bottom, -1000, 1179)]
    public void AllDirectionsUsePhysicalPixelCoordinates(ScreenEdge edge, int x, int y)
    {
        var detector = new EdgeDetector(); var s = new AppSettings { Edge = edge }; var p = new PixelPoint(x, y);
        Assert.False(detector.Observe(p, _display, [_display], s, 1)); Assert.True(detector.Observe(p, _display, [_display], s, 301));
    }
    [Fact] public void PortalRangeFiltersPositions()
    {
        var detector = new EdgeDetector(); var s = new AppSettings { PortalStart = .4, PortalEnd = .6 };
        Assert.False(detector.Observe(new(-1, 101), _display, [_display], s, 0)); Assert.False(detector.Observe(new(-1, 101), _display, [_display], s, 1000));
    }
    [Fact] public void FractionalMotionAndHighResolutionWheelArePreserved()
    {
        var motion = new MotionAccumulator(); motion.Add(.5, -.5, .25); Assert.Equal((0, 0, 0), motion.Take());
        motion.Add(.5, -.5, .75); Assert.Equal((1, -1, 1), motion.Take()); Assert.Equal((0, 0, 0), motion.Take());
    }
    [Fact] public void LargeMotionIsSplitWithoutLoss()
    {
        var motion = new MotionAccumulator(); motion.Add(65535, -65535, 300);
        var a = motion.Take(); var b = motion.Take(); var c = motion.Take(); Assert.Equal(65535, a.X + b.X + c.X); Assert.Equal(-65535, a.Y + b.Y + c.Y); Assert.Equal(300, a.Wheel + b.Wheel + c.Wheel);
    }
    [Fact] public void VirtualCursorStartsConservativelyAndDoesNotKeepOvershootDebt()
    {
        var cursor = new VirtualCursor(); var s = new AppSettings(); cursor.Enter(s);
        Assert.Equal((s.VirtualWidth - 1) / 2d, cursor.X);
        Assert.False(cursor.Move(-30, 0, s)); Assert.False(cursor.Move(-20, 0, s));
        Assert.True(cursor.Move(-10000, 0, s)); Assert.Equal(0, cursor.X);
        Assert.False(cursor.Move(1, 0, s)); Assert.Equal(1, cursor.X);
    }
    [Fact] public void SettingsRoundTripAndMalformedFileRecovery()
    {
        var directory = Path.Combine(Path.GetTempPath(), "UniversalBridge-test-" + Guid.NewGuid());
        try
        {
            var store = new SettingsStore(directory); Assert.Equal(1, store.Load().MouseSensitivity);
            store.Save(new() { MouseSensitivity = 1.5, Theme = "Dark", Edge = ScreenEdge.Left, AutoSwitch = true });
            Assert.Equal(1.5, store.Load().MouseSensitivity); Assert.Equal(ScreenEdge.Left, store.Load().Edge);
            File.WriteAllText(Path.Combine(directory, "settings.json"), "{broken"); Assert.Equal(1, store.Load().MouseSensitivity); Assert.NotNull(store.Warning);
            Assert.Equal("{broken", File.ReadAllText(Path.Combine(directory, "settings.json"))); Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact] public void InvalidSettingsAreBounded()
    {
        var s = new AppSettings { MouseSensitivity = double.NaN, ScrollSensitivity = -99, EdgeDwellMs = 0, PortalStart = .9, PortalEnd = .1, Edge = (ScreenEdge)100 };
        s = s.Normalize(); Assert.Equal(1, s.MouseSensitivity); Assert.Equal(.1, s.ScrollSensitivity); Assert.Equal(150, s.EdgeDwellMs); Assert.Equal(.8, s.PortalStart); Assert.Equal(1, s.PortalEnd); Assert.Equal(ScreenEdge.Right, s.Edge);
    }
    [Fact] public void ReconnectUsesBoundedExponentialBackoff()
    {
        var retry = new ReconnectPolicy(); var delays = new long[] { 2000, 4000, 8000, 16000, 30000, 30000 };
        for (var attempt = 0; attempt < delays.Length; attempt++)
        {
            retry.RecordFailure(1000); Assert.Equal(1000 + delays[attempt], retry.NextAttemptAt);
            Assert.False(retry.CanRetry(1001)); Assert.Equal(attempt < 5, retry.CanRetry(retry.NextAttemptAt));
        }
        retry.Reset(); Assert.True(retry.CanRetry(0)); Assert.Equal(0, retry.Failures);
    }
}
