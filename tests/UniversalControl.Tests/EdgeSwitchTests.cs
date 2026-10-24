using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class EdgeSwitchTests
{
    private readonly DisplayInfo _display = new("main", -1920, -300, 1920, 1200, true);
    [Fact] public void ArrivalThenStationaryPollingActivatesOnceAndNeedsLeavingToRearm()
    {
        var edge = new EdgeDetector(); var settings = new AppSettings(); var point = new PixelPoint(-1, 200);
        Assert.False(edge.Poll(point, _display, [_display], settings, 0));
        Assert.False(edge.Poll(point, _display, [_display], settings, 5000)); // Merely enabling while parked cannot arm it.
        Assert.False(edge.Observe(point, _display, [_display], settings, 5100));
        Assert.False(edge.Poll(point, _display, [_display], settings, 5399));
        Assert.True(edge.Poll(point, _display, [_display], settings, 5400));
        Assert.False(edge.Observe(point, _display, [_display], settings, 9000));
        edge.Poll(new(-20, 200), _display, [_display], settings, 9001);
        Assert.False(edge.Observe(point, _display, [_display], settings, 9100));
        Assert.True(edge.Poll(point, _display, [_display], settings, 9400));
    }
    [Theory] [InlineData(ScreenEdge.Left, -1920, 100)] [InlineData(ScreenEdge.Right, -1, 100)]
    [InlineData(ScreenEdge.Top, -1000, -300)] [InlineData(ScreenEdge.Bottom, -1000, 899)]
    public void AllEdgesCompleteWithoutAnotherMouseMessage(ScreenEdge direction, int x, int y)
    {
        var edge = new EdgeDetector(); var settings = new AppSettings { Edge = direction, EdgeDwellMs = 150 };
        Assert.False(edge.Observe(new(x, y), _display, [_display], settings, 1));
        Assert.True(edge.Poll(new(x, y), _display, [_display], settings, 151));
    }
    [Fact] public void LeavingPortalOrCancellingDuringWaitDoesNotActivateLater()
    {
        var edge = new EdgeDetector(); var settings = new AppSettings { PortalStart = .2, PortalEnd = .6 }; var point = new PixelPoint(-1, 100);
        edge.Observe(point, _display, [_display], settings, 0);
        Assert.False(edge.Poll(new(-20, 100), _display, [_display], settings, 100));
        Assert.False(edge.Poll(point, _display, [_display], settings, 1000));
        edge.Observe(point, _display, [_display], settings, 1100); edge.Reset();
        Assert.False(edge.Poll(point, _display, [_display], settings, 2000));
        Assert.False(edge.Observe(point, _display, [_display], settings, 3000));
    }
    [Fact] public void PreviewSubtractsPartialAdjacentMonitorsAndAgreesWithHitTesting()
    {
        var adjacent = new DisplayInfo("other", 0, -300, 2560, 600); var settings = new AppSettings();
        var segments = EdgeDetector.AvailableSegments(_display, [_display, adjacent], settings);
        Assert.Single(segments); Assert.Equal((.5, 1d), segments[0]);
        Assert.False(EdgeDetector.IsAtPortal(new(-1, 200), _display, [_display, adjacent], settings));
        Assert.True(EdgeDetector.IsAtPortal(new(-1, 300), _display, [_display, adjacent], settings));
        Assert.Empty(EdgeDetector.AvailableSegments(_display, [_display, adjacent], settings with { PortalEnd = .4 }));
    }
    [Theory] [InlineData(1.2, .5, ScreenEdge.Right)] [InlineData(-.2, .5, ScreenEdge.Left)]
    [InlineData(.5, -.3, ScreenEdge.Top)] [InlineData(.5, 1.3, ScreenEdge.Bottom)]
    public void LayoutDropSelectsEachDirectionAndPreservesThePortalWidth(double x, double y, ScreenEdge expected)
    {
        var placement = PortalPlacement.FromDrop(x, y, .4);
        Assert.Equal(expected, placement.Edge); Assert.Equal(.3, placement.Start, 8); Assert.Equal(.7, placement.End, 8);
    }
}
