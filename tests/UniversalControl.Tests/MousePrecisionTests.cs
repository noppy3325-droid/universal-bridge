using System.Buffers.Binary;
using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class MousePrecisionTests
{
    [Theory]
    [InlineData(.1, 10)] [InlineData(.05, 5)] [InlineData(.02, 2)]
    public void SlowMotionDoesNotLoseCountsAtIntegerBoundaries(double sensitivity, int expected)
    {
        var motion = new MotionAccumulator(); var x = 0; var y = 0;
        for (var i = 0; i < 100; i++)
        { motion.Add(sensitivity, -sensitivity, 0); var next = motion.Take(); x += next.X; y += next.Y; }
        Assert.Equal(expected, x); Assert.Equal(-expected, y); Assert.Equal((0, 0, 0), motion.Take());
    }
    [Fact]
    public void SlowWheelDoesNotWaitAnExtraEventAtIntegerBoundary()
    {
        var motion = new MotionAccumulator();
        for (var i = 0; i < 9; i++) { motion.Add(0, 0, .1); Assert.Equal(0, motion.Take().Wheel); }
        motion.Add(0, 0, .1); Assert.Equal(1, motion.Take().Wheel);
    }
    [Fact]
    public void RelativeDeviceCountsAreNotAcceleratedOrCombinedWithOtherDevices()
    {
        var motion = new RawMouseMotion();
        Assert.Equal((1d, -2d), motion.Translate(1, false, 1, -2, 1920, 1080));
        Assert.Equal((500d, -400d), motion.Translate(2, false, 500, -400, 3840, 2160));
        Assert.Equal((1d, -2d), motion.Translate(1, false, 1, -2, 1920, 1080));
    }
    [Fact]
    public void AbsoluteDevicesHaveIndependentBaselinesAndKeepSubpixelPrecision()
    {
        var motion = new RawMouseMotion();
        Assert.Equal((0d, 0d), motion.Translate(1, true, 10000, 20000, 1920, 1080));
        Assert.Equal((0d, 0d), motion.Translate(2, true, 50000, 40000, 1920, 1080));
        var delta = motion.Translate(1, true, 10001, 19999, 1920, 1080);
        Assert.Equal(1919 / 65535d, delta.X, 12); Assert.Equal(-1079 / 65535d, delta.Y, 12);
        Assert.Equal((0d, 0d), motion.Translate(2, true, 50000, 40000, 1920, 1080));
    }
    [Fact]
    public void AbsoluteModeAndDisplayChangesNeverCreateEntryJumps()
    {
        var motion = new RawMouseMotion();
        motion.Translate(1, true, 0, 0, 1920, 1080);
        Assert.Equal((0d, 0d), motion.Translate(1, true, 60000, 60000, 3840, 2160));
        motion.Clear(); Assert.Equal((0d, 0d), motion.Translate(1, true, 10000, 10000, 3840, 2160));
        motion.Translate(1, false, 2, 2, 3840, 2160);
        Assert.Equal((0d, 0d), motion.Translate(1, true, 50000, 50000, 3840, 2160));
    }
    [Theory]
    [InlineData(1)] [InlineData(10)] [InlineData(100)]
    public async Task FineMotionTotalsDoNotDependOnReportBatchSize(int batch)
    {
        var fake = new FakeTransport(); await using var dispatcher = new ReportDispatcher(fake);
        var router = new InputRouter(fake, dispatcher); router.Configure(new() { MouseSensitivity = .1 });
        await router.ActivateAsync("iPad", () => true);
        for (var i = 1; i <= 100; i++) { router.Move(1, -1); if (i % batch == 0) router.FlushMotion(); }
        await FakeTransport.Until(() => fake.Sent.Skip(2).Sum(p => BinaryPrimitives.ReadInt16LittleEndian(p.Frame.Payload.AsSpan(1))) == 10);
        Assert.Equal(-10, fake.Sent.Skip(2).Sum(p => BinaryPrimitives.ReadInt16LittleEndian(p.Frame.Payload.AsSpan(3))));
        Assert.True(router.IsRemote);
    }
    [Fact]
    public async Task ActivationChordWhileRemoteDoesNotTypeIOrToggleBack()
    {
        var fake = new FakeTransport(); await using var dispatcher = new ReportDispatcher(fake); var router = new InputRouter(fake, dispatcher);
        await router.ActivateAsync("iPad", () => true);
        router.Key(0xA2, 0x1D, false, true, 1); router.Key(0xA4, 0x38, false, true, 2);
        Assert.True(router.Key(0x49, 0x17, false, true, 3)); Assert.True(router.Key(0x49, 0x17, false, true, 4));
        Assert.True(router.Key(0x49, 0x17, false, false, 5));
        router.Key(0xA4, 0x38, false, false, 6); router.Key(0xA2, 0x1D, false, false, 7);
        await FakeTransport.Until(() => fake.Sent.Count == 6);
        Assert.True(router.IsRemote); Assert.True(router.PhysicalIdle);
        Assert.DoesNotContain(fake.Sent.Where(p => p.Frame.Kind == ReportKind.Keyboard), p => p.Frame.Payload.Skip(2).Contains((byte)0x0C));
    }
    [Fact]
    public void OldSettingsKeepSensitivityAndDefaultToRelativeInput()
    {
        var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{\"SchemaVersion\":1,\"MouseSensitivity\":0.1}")!.Normalize();
        Assert.True(settings.RawMouse); Assert.Equal(.1, settings.MouseSensitivity);
        Assert.Equal(.02, (settings with { MouseSensitivity = 0 }).Normalize().MouseSensitivity);
        Assert.Equal(.11, (settings with { MouseSensitivity = .1100000001 }).Normalize().MouseSensitivity);
    }
}
