using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class GestureShortcutTests
{
    [Fact] public void ShortcutPairRejectsDuplicateAndUnsafeBindings()
    {
        Assert.Throws<ArgumentException>(() => ShortcutBinding.ValidatePair(ShortcutBinding.DefaultActivate, ShortcutBinding.DefaultActivate));
        Assert.Throws<ArgumentException>(() => ShortcutBinding.ValidatePair(new(0, 0x41), ShortcutBinding.DefaultReturn));
        Assert.Throws<ArgumentException>(() => ShortcutBinding.ValidatePair(new(ShortcutModifiers.Shift, 0x41), ShortcutBinding.DefaultReturn));
        Assert.Throws<ArgumentException>(() => ShortcutBinding.ValidatePair(new(ShortcutModifiers.Control, 0x1B), ShortcutBinding.DefaultReturn));
    }
    [Fact] public void ShortcutMatchHonorsBothSidesAndExtraModifiers()
    {
        var shortcut = new ShortcutBinding(ShortcutModifiers.Control | ShortcutModifiers.Shift, 0x7B);
        Assert.True(shortcut.Matches(0x7B, 0x21)); Assert.True(shortcut.Matches(0x7B, 0x12));
        Assert.False(shortcut.Matches(0x7B, 0x26)); Assert.False(shortcut.Matches(0x7B, 0x2A));
        Assert.False(shortcut.Matches(0x7A, 0x22)); Assert.Equal("Ctrl + Shift + F12", shortcut.ToString());
    }
    [Fact] public void SettingsMigrateOldProfileAndRepairInvalidPairs()
    {
        var old = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{\"SchemaVersion\":1,\"MouseSensitivity\":0.84}")!.Normalize();
        Assert.True(old.CaptureTouchpad); Assert.Equal(.84, old.MouseSensitivity); Assert.Equal(ShortcutBinding.DefaultReturn, old.ReturnShortcut);
        var invalid = (old with { ActivateShortcut = old.ReturnShortcut }).Normalize();
        Assert.Equal(ShortcutBinding.DefaultActivate, invalid.ActivateShortcut); Assert.Equal(ShortcutBinding.DefaultReturn, invalid.ReturnShortcut);
    }
    [Theory]
    [InlineData(-700, 100, GestureAction.NextApp)] [InlineData(700, 100, GestureAction.PreviousApp)]
    [InlineData(50, -700, GestureAction.NextApp)] [InlineData(50, 700, GestureAction.Home)] [InlineData(100, 100, GestureAction.None)]
    public void SwipesHaveDirectionThresholdAndConfigurableActions(double x, double y, GestureAction expected)
    { Assert.Equal(expected, TouchpadGestures.Swipe(x, y, new())); Assert.Equal(GestureAction.Search, TouchpadGestures.Swipe(-700, 0, new() { SwipeLeft = GestureAction.Search })); }
    [Fact] public void PinchAccumulatesSmallChangesAndResetsBetweenGestures()
    {
        var gestures = new TouchpadGestures(); Assert.Equal(0, gestures.ZoomSteps(1.05)); Assert.Equal(0, gestures.ZoomSteps(1.05));
        Assert.Equal(1, gestures.ZoomSteps(1.05)); gestures.Reset(); Assert.Equal(0, gestures.ZoomSteps(1));
        Assert.Equal(-1, gestures.ZoomSteps(.85)); Assert.Equal(0, gestures.ZoomSteps(double.NaN)); Assert.Equal(0, gestures.ZoomSteps(0));
    }
    [Fact] public void HorizontalScrollHasIndependentSignedByteAndValidation()
    {
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 1, 0xFE }, HidReports.Mouse(0, wheel: 1, pan: -2).Payload);
        Assert.Equal((byte)127, HidReports.Mouse(0, pan: 400).Payload[6]);
        Assert.Throws<ArgumentException>(() => HidReports.Validate(new(ReportKind.Mouse, [0, 0, 0, 0, 0, 0, 0x80])));
    }
    [Fact] public async Task GesturesOnlySendToReadyRemoteAndAlwaysRelease()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        Assert.False(router.Gesture(GestureAction.NextApp)); Assert.Empty(fake.Sent);
        await router.ActivateAsync("iPad", () => true); Assert.True(router.Gesture(GestureAction.NextApp));
        await FakeTransport.Until(() => fake.Sent.Count == 4);
        Assert.Equal(8, fake.Sent.ToArray()[2].Frame.Payload[0]); Assert.Equal(0x2B, fake.Sent.ToArray()[2].Frame.Payload[2]);
        Assert.All(fake.Sent.Last().Frame.Payload, value => Assert.Equal(0, value));
        fake.Ready = false; Assert.False(router.Gesture(GestureAction.Home)); Assert.False(router.IsRemote);
    }
    [Fact] public async Task GestureCannotClobberHeldKeyboardOrDrag()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        await router.ActivateAsync("iPad", () => true); router.Key(0x41, 0x1E, false, true, 0);
        Assert.False(router.Gesture(GestureAction.Search)); router.Key(0x41, 0x1E, false, false, 1);
        router.Button(MouseButtons.Left, true); Assert.False(router.Gesture(GestureAction.NextApp));
        router.Button(MouseButtons.Left, false); Assert.True(router.Gesture(GestureAction.NextApp));
    }
    [Fact] public async Task SubNotchScrollIsPreservedOnBothAxesAndDiscardedOnReturn()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        await router.ActivateAsync("iPad", () => true);
        for (var i = 0; i < 10; i++) { router.Scroll(12, -12); router.FlushMotion(); }
        await FakeTransport.Until(() => fake.Sent.Count == 3); Assert.Equal(1, fake.Sent.Last().Frame.Payload[5]); Assert.Equal(0xFF, fake.Sent.Last().Frame.Payload[6]);
        router.Scroll(60, 60); router.GoLocal(); await FakeTransport.Until(() => !dispatch.Settling);
        await router.ActivateAsync("iPad", () => true); router.FlushMotion();
        Assert.All(fake.Sent.Last().Frame.Payload, value => Assert.Equal(0, value));
    }
    [Fact] public async Task CustomReturnChordAndImmutableEscapeRecoveryWork()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatch);
        router.Configure(new() { ReturnShortcut = new(ShortcutModifiers.Control | ShortcutModifiers.Shift, 0x7B) });
        await router.ActivateAsync("iPad", () => true);
        router.Key(0xA2, 0x1D, false, true, 1); router.Key(0xA0, 0x2A, false, true, 2);
        Assert.True(router.Key(0x7B, 0x58, false, true, 3)); Assert.False(router.IsRemote);
        router.ReconcilePhysical(_ => false); await FakeTransport.Until(() => !dispatch.Settling); await router.ActivateAsync("iPad", () => true);
        for (var i = 0; i < 3; i++) { router.Key(0x1B, 1, false, true, 100 + i * 100); router.Key(0x1B, 1, false, false, 101 + i * 100); }
        Assert.False(router.IsRemote);
    }
}
