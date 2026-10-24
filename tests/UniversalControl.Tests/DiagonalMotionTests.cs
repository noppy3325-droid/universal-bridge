using System.Buffers.Binary;
using System.Text.Json;
using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class DiagonalMotionTests
{
    [Theory]
    [InlineData(.13, .037)] [InlineData(-.13, .037)] [InlineData(.037, -.13)] [InlineData(-.037, -.13)]
    [InlineData(.02, .02)] [InlineData(.1, -.03)]
    public void EveryDiagonalPrefixStaysWithinHalfACountWithoutIdleOscillation(double dx, double dy)
    {
        var motion = new MotionAccumulator(); var x = 0; var y = 0;
        for (var i = 1; i <= 1000; i++)
        {
            motion.Add(dx, dy, 0); var next = motion.Take(); x += next.X; y += next.Y;
            Assert.InRange(Math.Abs(x - i * dx), 0, .500000001);
            Assert.InRange(Math.Abs(y - i * dy), 0, .500000001);
        }
        for (var i = 0; i < 100; i++) Assert.Equal((0, 0, 0), motion.Take());
    }

    [Fact] public void MinorAxisRespondsEarlierAndHasLowerTrajectoryErrorThanTruncation()
    {
        var motion = new MotionAccumulator(); double oldX = 0, oldY = 0, oldError = 0, error = 0;
        int x = 0, y = 0, ox = 0, oy = 0, firstY = 0, oldFirstY = 0;
        for (var i = 1; i <= 1000; i++)
        {
            motion.Add(.13, .037, 0); var next = motion.Take(); x += next.X; y += next.Y;
            oldX += .13; oldY += .037; var tx = LegacyAxis(oldX); var ty = LegacyAxis(oldY);
            oldX -= tx; oldY -= ty; ox += tx; oy += ty;
            if (firstY == 0 && y != 0) firstY = i;
            if (oldFirstY == 0 && oy != 0) oldFirstY = i;
            error += Math.Pow(x - i * .13, 2) + Math.Pow(y - i * .037, 2);
            oldError += Math.Pow(ox - i * .13, 2) + Math.Pow(oy - i * .037, 2);
        }
        Assert.True(firstY < oldFirstY); Assert.True(error < oldError * .5);
        var output = Environment.GetEnvironmentVariable("UNIVERSALBRIDGE_ARTIFACTS");
        if (output is not null) File.WriteAllText(Path.Combine(output, "diagonal-motion-comparison.json"), JsonSerializer.Serialize(new {
            Samples = 1000, MinorAxisFirstSampleBefore = oldFirstY, MinorAxisFirstSampleAfter = firstY,
            RmsCountErrorBefore = Math.Sqrt(oldError / 1000), RmsCountErrorAfter = Math.Sqrt(error / 1000),
            SyntheticInput = true, BluetoothMeasured = false, IpadMeasured = false }));
    }

    [Theory] [InlineData(100000, 25000)] [InlineData(-100000, 25000)] [InlineData(25000, -100000)]
    public void HidRangeSplittingPreservesDiagonalDirectionAndTotal(int inputX, int inputY)
    {
        var motion = new MotionAccumulator(); motion.Add(inputX, inputY, 0);
        var x = 0; var y = 0;
        for (var i = 0; i < 4; i++)
        {
            var next = motion.Take(); x += next.X; y += next.Y;
            Assert.InRange(Math.Abs((double)next.X * inputY - (double)next.Y * inputX), 0, Math.Abs(inputX) + Math.Abs(inputY));
        }
        Assert.Equal(inputX, x); Assert.Equal(inputY, y); Assert.Equal((0, 0, 0), motion.Take());
    }

    [Fact] public void ReversingMotionAndResetDoNotProduceALateTail()
    {
        var motion = new MotionAccumulator();
        motion.Add(.6, -.6, 0); Assert.Equal((1, -1, 0), motion.Take());
        motion.Add(-.6, .6, 0); Assert.Equal((-1, 1, 0), motion.Take());
        Assert.Equal((0, 0, 0), motion.Take());
        motion.Add(.49, -.49, 0); motion.Clear(); Assert.Equal((0, 0, 0), motion.Take());
    }

    [Fact] public async Task SlowBluetoothCoalescesTravelWithoutDelayingOrMergingClickEdges()
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake, 8);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.BeforeSend = async ct => { entered.TrySetResult(); await resume.Task.WaitAsync(ct); };
        sender.Enqueue("iPad", 0, HidReports.Keyboard(0, [4])); await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        for (var i = 0; i < 1000; i++) Assert.True(sender.EnqueueMotion("iPad", 0, HidReports.Mouse(0, 1, 2)));
        Assert.True(sender.Enqueue("iPad", 0, HidReports.Mouse(MouseButtons.Left)));
        for (var i = 0; i < 100; i++) Assert.True(sender.EnqueueMotion("iPad", 0, HidReports.Mouse(MouseButtons.Left, 1, 1)));
        Assert.True(sender.Enqueue("iPad", 0, HidReports.Mouse(0)));
        Assert.True(sender.Enqueue("iPad", 0, HidReports.Keyboard(0, [])));
        resume.SetResult(); await FakeTransport.Until(() => fake.Sent.Count == 6);
        var sent = fake.Sent.ToArray(); Assert.Equal(ReportKind.Keyboard, sent[0].Frame.Kind);
        Assert.Equal((1000, 2000), XY(sent[1].Frame)); Assert.Equal(1, sent[2].Frame.Payload[0]);
        Assert.Equal((100, 100), XY(sent[3].Frame)); Assert.Equal(1, sent[3].Frame.Payload[0]);
        Assert.Equal(0, sent[4].Frame.Payload[0]); Assert.Equal(ReportKind.Keyboard, sent[5].Frame.Kind);
        Assert.Equal(1098, sender.CoalescedMotion); Assert.True(sender.Healthy);
    }

    [Fact] public async Task ReversalWheelKeyboardAndRangeAreCoalescingBarriers()
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.BeforeSend = async ct => { entered.TrySetResult(); await resume.Task.WaitAsync(ct); };
        sender.Enqueue("iPad", 0, HidReports.Keyboard(0, [])); await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        sender.EnqueueMotion("iPad", 0, HidReports.Mouse(0, 32760, 100));
        sender.EnqueueMotion("iPad", 0, HidReports.Mouse(0, 100, 1)); // would overflow X
        sender.EnqueueMotion("iPad", 0, HidReports.Mouse(0, -100, -1)); // reversal
        sender.EnqueueMotion("iPad", 0, HidReports.Mouse(0, 0, 0, 1)); // wheel
        sender.EnqueueMotion("iPad", 0, HidReports.Mouse(0, 10, 10));
        sender.Enqueue("iPad", 0, HidReports.Keyboard(0, [4]));
        sender.EnqueueMotion("iPad", 0, HidReports.Mouse(0, 10, 10));
        resume.SetResult(); await FakeTransport.Until(() => fake.Sent.Count == 8);
        Assert.Equal(0, sender.CoalescedMotion);
        var sent = fake.Sent.ToArray(); Assert.Equal((32760, 100), XY(sent[1].Frame));
        Assert.Equal((-100, -1), XY(sent[3].Frame)); Assert.Equal(1, sent[4].Frame.Payload[5]);
        Assert.Equal(ReportKind.Keyboard, sent[6].Frame.Kind);
    }

    [Fact] public async Task ReturningLocalDiscardsCoalescedOldMovement()
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender);
        await router.ActivateAsync("iPad", () => true);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.BeforeSend = async ct => { entered.TrySetResult(); await resume.Task.WaitAsync(ct); };
        router.Key(0x41, 0x1E, false, true, 0); await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        for (var i = 0; i < 100; i++) { router.Move(1, 1); router.FlushMotion(); }
        router.GoLocal(); resume.SetResult(); await FakeTransport.Until(() => !sender.Settling && fake.Sent.Count >= 5);
        Assert.All(fake.Sent.Where(item => item.Frame.Kind == ReportKind.Mouse), item => Assert.Equal((0, 0), XY(item.Frame)));
        Assert.False(router.IsRemote);
    }

    private static (int X, int Y) XY(HidFrame frame) => (BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.AsSpan(1)), BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.AsSpan(3)));
    private static int LegacyAxis(double value)
    {
        var nearest = Math.Round(value);
        return (int)Math.Truncate(Math.Abs(value - nearest) < 1e-9 ? nearest : value);
    }
}
