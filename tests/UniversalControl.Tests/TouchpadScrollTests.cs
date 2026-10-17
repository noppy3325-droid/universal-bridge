using System.Text.Json;
using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class TouchpadScrollTests
{
    [Fact] public void FirstUpdateIncludesMovementBeforeRecognitionAndCompletionDoesNotRepeatIt()
    {
        var scroll = new TouchpadScroll();
        var first = scroll.Update(-100, 200, 3);
        Assert.Equal(240, first.Vertical, 8); Assert.Equal(-120, first.Horizontal, 8);
        Assert.Equal((0d, 0d), scroll.Update(-100, 200, 3));
        var final = scroll.Update(-150, 250, 3);
        Assert.Equal(60, final.Vertical, 8); Assert.Equal(-60, final.Horizontal, 8);
    }
    [Fact] public void CoalescingDoesNotChangeTotalScrollAndDirectionChangesAreRetained()
    {
        var split = new TouchpadScroll(); var vertical = 0d; var horizontal = 0d;
        foreach (var (x, y) in new[] { (10d, 20d), (40d, 80d), (-30d, 50d) })
        { var delta = split.Update(x, y, 3); vertical += delta.Vertical; horizontal += delta.Horizontal; }
        var combined = new TouchpadScroll().Update(-30, 50, 3);
        Assert.Equal(combined.Vertical, vertical, 8); Assert.Equal(combined.Horizontal, horizontal, 8);
        split.Reset(); Assert.Equal(24, split.Update(0, 20, 3).Vertical, 8);
    }
    [Theory]
    [InlineData(double.NaN, 5, 3)][InlineData(5, double.PositiveInfinity, 3)]
    [InlineData(5, 5, double.NaN)][InlineData(5, 5, 0)]
    public void InvalidSamplesCannotPoisonSubsequentMotion(double x, double y, double sensitivity)
    {
        var scroll = new TouchpadScroll(); Assert.Equal((0d, 0d), scroll.Update(x, y, sensitivity));
        Assert.Equal(120, scroll.Update(0, 100, 3).Vertical, 8);
    }
    [Theory]
    [InlineData(-1, .25)][InlineData(50, 12)][InlineData(double.NaN, 3)][InlineData(double.PositiveInfinity, 3)]
    public void TouchpadSensitivityHasSafeBoundsAndDefault(double value, double expected)
        => Assert.Equal(expected, new AppSettings { TouchpadScrollSensitivity = value }.Normalize().TouchpadScrollSensitivity);

    [Fact] public void OldProfilesKeepWheelSettingsAndReceiveFasterTouchpadDefault()
    {
        var old = JsonSerializer.Deserialize<AppSettings>("{\"SchemaVersion\":1,\"ScrollSensitivity\":0.5}")!.Normalize();
        Assert.Equal(.5, old.ScrollSensitivity); Assert.Equal(3, old.TouchpadScrollSensitivity);
        var saved = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(old with { TouchpadScrollSensitivity = 5.75 }))!;
        Assert.Equal(5.75, saved.Normalize().TouchpadScrollSensitivity);
    }
    [Fact] public async Task TouchpadAndWheelGainsAreIndependentAndCompletionPreservesTailMotion()
    {
        var fake = new FakeTransport(); await using var dispatcher = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatcher);
        router.Configure(new() { ScrollSensitivity = .5, TouchpadScrollSensitivity = 3 });
        await router.ActivateAsync("iPad", () => true);
        router.BeginTouchpadScroll(); router.ScrollTouchpad(-100, 100); router.FlushMotion();
        await FakeTransport.Until(() => fake.Sent.Count == 3);
        Assert.Equal(1, fake.Sent.Last().Frame.Payload[5]); Assert.Equal(0xFF, fake.Sent.Last().Frame.Payload[6]);
        router.ScrollTouchpad(-150, 150); router.ScrollTouchpad(-200, 200); router.FlushMotion();
        await FakeTransport.Until(() => fake.Sent.Count == 4);
        Assert.Equal(1, fake.Sent.Last().Frame.Payload[5]); Assert.Equal(0xFF, fake.Sent.Last().Frame.Payload[6]);
        router.ScrollTouchpad(-200, 200); router.FlushMotion(); // repeated final cumulative sample emits nothing
        router.Scroll(120); router.FlushMotion(); router.Scroll(120); router.FlushMotion();
        await FakeTransport.Until(() => fake.Sent.Count == 5);
        Assert.Equal(1, fake.Sent.Last().Frame.Payload[5]); Assert.Equal(0, fake.Sent.Last().Frame.Payload[6]);
    }
    [Fact] public async Task TinySeparateGesturesAccumulateButNeverLeakAcrossReturn()
    {
        var fake = new FakeTransport(); await using var dispatcher = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatcher);
        await router.ActivateAsync("iPad", () => true);
        for (var gesture = 0; gesture < 5; gesture++) { router.BeginTouchpadScroll(); router.ScrollTouchpad(0, 20); router.FlushMotion(); }
        await FakeTransport.Until(() => fake.Sent.Count == 3); Assert.Equal(1, fake.Sent.Last().Frame.Payload[5]);
        router.BeginTouchpadScroll(); router.ScrollTouchpad(0, 50); router.GoLocal();
        await FakeTransport.Until(() => !dispatcher.Settling);
        await router.ActivateAsync("iPad", () => true);
        router.ScrollTouchpad(0, 100); router.FlushMotion();
        await FakeTransport.Until(() => fake.Sent.Last().Frame.Payload[5] == 1);
        Assert.Equal(1, fake.Sent.Last().Frame.Payload[5]);
    }
}
