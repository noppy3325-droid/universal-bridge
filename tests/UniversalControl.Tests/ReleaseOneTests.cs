using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class ReleaseOneTests
{
    private readonly DisplayInfo _display = new("main", 0, 0, 1920, 1080);
    [Fact] public void NeutralHandshakeToleratesSmallInwardJitterThatTheOldExactEdgeRejected()
    {
        var settings = new AppSettings(); var point = new PixelPoint(1917, 500);
        Assert.False(EdgeDetector.IsAtPortal(point, _display, [_display], settings));
        var lease = new EdgeActivationLease(_display, [_display], settings, 1, 0);
        Assert.True(lease.Validate(point, 1, true, true, 50));
        Assert.True(lease.Validate(new(1910, 500), 1, true, true, 200));
        Assert.Equal(EdgeCancellation.None, lease.Cancellation);
    }
    [Theory] [InlineData(ScreenEdge.Left, 2, 500)] [InlineData(ScreenEdge.Right, 1917, 500)]
    [InlineData(ScreenEdge.Top, 1000, 2)] [InlineData(ScreenEdge.Bottom, 1000, 1077)]
    public void ArrivalBandWorksWithPhysicalCursorSampling(ScreenEdge direction, int x, int y)
    {
        var detector = new EdgeDetector(); var settings = new AppSettings { Edge = direction };
        Assert.False(detector.Observe(new(x, y), _display, [_display], settings, 1));
        Assert.True(detector.Poll(new(x, y), _display, [_display], settings, 301));
    }
    [Theory] [InlineData(2, true, true, 100, EdgeCancellation.SettingsChanged)]
    [InlineData(1, false, true, 100, EdgeCancellation.NotReady)] [InlineData(1, true, false, 100, EdgeCancellation.InputHeld)]
    [InlineData(1, true, true, 5001, EdgeCancellation.Expired)]
    public void CancellationIsPermanentEvenIfTheConnectionReturns(int revision, bool ready, bool idle, long now, EdgeCancellation expected)
    {
        var lease = new EdgeActivationLease(_display, [_display], new(), 1, 0);
        Assert.False(lease.Validate(new(1919, 500), revision, ready, idle, now)); Assert.Equal(expected, lease.Cancellation);
        Assert.False(lease.Validate(new(1919, 500), 1, true, true, 200));
    }
    [Fact] public void DeliberateDepartureAndCrossingIntoWindowsMonitorStillCancel()
    {
        var lease = new EdgeActivationLease(_display, [_display], new(), 1, 0);
        Assert.False(lease.Validate(new(1900, 500), 1, true, true, 100)); Assert.Equal(EdgeCancellation.PointerLeft, lease.Cancellation);
        var other = new DisplayInfo("other", 1920, 0, 1920, 1080);
        lease = new(_display, [_display, other], new(), 1, 0);
        Assert.False(lease.Validate(new(1919, 500), 1, true, true, 100));
        lease = new(_display, [_display], new(), 1, 0);
        Assert.False(lease.Validate(null, 1, true, true, 100)); Assert.Equal(EdgeCancellation.CursorUnavailable, lease.Cancellation);
    }
    [Theory] [InlineData(2300, 9)] [InlineData(-2300, 9)] [InlineData(9, -2300)] [InlineData(.013, .0037)]
    public void StabilizedDiagonalCarriesFractionalDirectionWithBoundedCumulativeError(double x, double y)
    {
        var pointer = new PointerMotion(); var expected = PointerTransfer.Scale(x, y, 8); var sumX = 0; var sumY = 0;
        for (var i = 1; i <= 1000; i++)
        {
            pointer.Add(x, y); var emitted = pointer.Take(true, 8); sumX += emitted.X; sumY += emitted.Y;
            Assert.InRange(Math.Abs(sumX - expected.X * i), 0, .5000001);
            Assert.InRange(Math.Abs(sumY - expected.Y * i), 0, .5000001);
        }
        for (var i = 0; i < 100; i++) Assert.Equal((0, 0), pointer.Take(true, 8));
        pointer.Clear(); Assert.Equal((0, 0), pointer.Take(true, 8));
    }
    [Fact] public void DirectPointerPreservesHighDpiDistanceAndDoesNotInventAcceleration()
    {
        var pointer = new PointerMotion(); pointer.Add(70000, 35000);
        var a = pointer.Take(false, 8); var b = pointer.Take(false, 8); var c = pointer.Take(false, 8);
        Assert.Equal(70000, a.X + b.X + c.X); Assert.Equal(35000, a.Y + b.Y + c.Y);
        Assert.Equal((0, 0), pointer.Take(false, 8));
    }
    [Theory] [InlineData(GestureAction.PreviousApp, 10, 0x2B)] [InlineData(GestureAction.Screenshot, 10, 0x20)]
    [InlineData(GestureAction.Undo, 8, 0x1D)] [InlineData(GestureAction.Redo, 10, 0x1D)]
    [InlineData(GestureAction.ZoomIn, 10, 0x2E)] [InlineData(GestureAction.ZoomOut, 8, 0x2D)] [InlineData(GestureAction.CloseWindow, 8, 0x1A)]
    public async Task ExtraActionsUseStandardHidAndReleaseTheTrigger(GestureAction action, byte modifiers, byte usage)
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender);
        var settings = new AppSettings(); var extras = settings.AdditionalShortcuts.Select(entry => entry with { Enabled = true }).ToArray();
        router.Configure(settings with { AdditionalShortcuts = extras }); await router.ActivateAsync("iPad", () => true);
        var trigger = extras.First(entry => entry.Action == action).Binding.Key;
        router.Key(0xA2, 0x1D, false, true, 1); router.Key(0xA4, 0x38, false, true, 2);
        router.Key(trigger, 0, false, true, 3); router.Key(trigger, 0, false, true, 4); router.Key(trigger, 0, false, false, 5);
        router.Key(0xA4, 0x38, false, false, 6); router.Key(0xA2, 0x1D, false, false, 7);
        await FakeTransport.Until(() => fake.Sent.Count == 10);
        Assert.Single(fake.Sent, entry => entry.Frame.Kind == ReportKind.Keyboard && entry.Frame.Payload[0] == modifiers && entry.Frame.Payload[2] == usage);
        Assert.All(fake.Sent.Last().Frame.Payload, value => Assert.Equal(0, value));
    }
    [Fact] public void ExtraActionsMigrateConflictsAndKeepClosingWindowsOptIn()
    {
        var settings = new AppSettings { HomeShortcut = new(ShortcutModifiers.Control | ShortcutModifiers.Alt, 'P') }.Normalize();
        ActionShortcuts.Validate(settings.AdditionalShortcuts, settings.ActivateShortcut, settings.ReturnShortcut, settings.HomeShortcut, settings.SearchShortcut, settings.NextAppShortcut);
        Assert.False(settings.AdditionalShortcuts.First(entry => entry.Action == GestureAction.CloseWindow).Enabled);
        Assert.NotEqual(settings.HomeShortcut, settings.AdditionalShortcuts.First(entry => entry.Action == GestureAction.PreviousApp).Binding);
        var bad = settings.AdditionalShortcuts.Select(entry => entry with { Binding = settings.HomeShortcut }).ToArray();
        Assert.Throws<ArgumentException>(() => ActionShortcuts.Validate(bad, settings.HomeShortcut));
    }
    [Fact] public async Task SettingsSaveSurvivesATransientReadLockWithoutPartialWrites()
    {
        var directory = Path.Combine(Path.GetTempPath(), "UniversalBridge-lock-" + Guid.NewGuid());
        var store = new SettingsStore(directory); store.Save(new() { TouchpadScrollSensitivity = 3 });
        try
        {
            using var held = new FileStream(Path.Combine(directory, "settings.json"), FileMode.Open, FileAccess.Read, FileShare.Read);
            var saving = Task.Run(() => store.Save(new() { TouchpadScrollSensitivity = 4.5 }));
            await Task.Delay(60); held.Dispose(); await saving.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(4.5, store.Load().TouchpadScrollSensitivity); Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally { foreach (var path in Directory.GetFiles(directory)) File.Delete(path); Directory.Delete(directory); }
    }
}
