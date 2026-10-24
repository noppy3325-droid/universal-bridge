using System.Buffers.Binary;
using System.Text.Json;
using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class FastPointerTests
{
    private sealed class Clock : TimeProvider { public long Now; public override long TimestampFrequency => 1000; public override long GetTimestamp() => Now; }
    [Theory] [InlineData(1, 1, 8)] [InlineData(-10, 5, 15)] [InlineData(0, 10, 8)]
    public void FineMotionIsUnchanged(int x, int y, int milliseconds) => Assert.Equal((x, y), PointerTransfer.Apply(x, y, milliseconds));

    [Theory] [InlineData(1000, 0, 8)] [InlineData(-1000, 250, 15)] [InlineData(2000, -3000, 32)] [InlineData(30000, 30000, 300)]
    public void FastMotionIsRadiallyBoundedWithoutChangingDirection(int x, int y, int milliseconds)
    {
        var result = PointerTransfer.Apply(x, y, milliseconds);
        Assert.True(Math.Sqrt((double)result.X * result.X + (double)result.Y * result.Y) <= 8 * Math.Clamp(milliseconds, 4, 32) + 1);
        Assert.InRange(Math.Abs((double)result.X * y - (double)result.Y * x), 0, Math.Abs(x) + Math.Abs(y));
        Assert.Equal(Math.Sign(x), Math.Sign(result.X)); Assert.Equal(Math.Sign(y), Math.Sign(result.Y));
    }
    [Fact] public async Task AQuickFlickHasNoDelayedCarryAndLinearModeIsStillAvailable()
    {
        var fake = new FakeTransport(); var clock = new Clock(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender, clock);
        await router.ActivateAsync("iPad", () => true);
        router.Move(100000, 50000); clock.Now = 8; router.FlushMotion();
        await FakeTransport.Until(() => fake.Sent.Count == 3);
        var report = fake.Sent.Last().Frame;
        Assert.InRange((int)BinaryPrimitives.ReadInt16LittleEndian(report.Payload.AsSpan(1)), 1, 64);
        for (var i = 0; i < 100; i++) { clock.Now += 8; router.FlushMotion(); }
        Assert.Equal(3, fake.Sent.Count);
        router.Configure(new() { StabilizeFastPointer = false }); router.Move(200, 100); router.FlushMotion();
        await FakeTransport.Until(() => fake.Sent.Count == 4);
        Assert.Equal(200, BinaryPrimitives.ReadInt16LittleEndian(fake.Sent.Last().Frame.Payload.AsSpan(1)));
    }
    [Fact] public async Task TouchpadScrollRespondsBeforeOneFullLegacyTickAndDoesNotOscillate()
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender);
        await router.ActivateAsync("iPad", () => true); router.BeginTouchpadScroll();
        router.ScrollTouchpad(0, 60); router.FlushMotion(); // .6 wheel ticks at default gain.
        await FakeTransport.Until(() => fake.Sent.Count == 3); Assert.Equal(1, fake.Sent.Last().Frame.Payload[5]);
        for (var i = 0; i < 100; i++) router.FlushMotion(); Assert.Equal(3, fake.Sent.Count);
        router.ScrollTouchpad(0, 100); router.FlushMotion(); Assert.Equal(3, fake.Sent.Count); // Total remains one tick.
    }
    [Fact] public void OldSettingsEnableBurstProtectionButKeepNativeModeOptIn()
    {
        var old = JsonSerializer.Deserialize<AppSettings>("{\"SchemaVersion\":1,\"MouseSensitivity\":0.37}")!.Normalize();
        Assert.True(old.StabilizeFastPointer); Assert.False(old.NativeTouchpad); Assert.Equal(.37, old.MouseSensitivity);
        Assert.True((old with { NativeTouchpad = true, CaptureTouchpad = false }).Normalize().CaptureTouchpad);
    }
}
