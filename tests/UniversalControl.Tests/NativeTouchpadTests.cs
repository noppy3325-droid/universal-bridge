using System.Collections.Concurrent;
using System.Buffers.Binary;
using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class NativeTouchpadTests
{
    private sealed class Transport : IHidTransport
    {
        public bool NativeReady = true; public int Releases = 3;
        public Func<CancellationToken, Task>? Wait;
        public ConcurrentQueue<HidFrame> Sent { get; } = new();
        public int NeutralReportCount => 3;
        public bool IsReady(string id) => id == "iPad";
        public bool IsTouchpadReady(string id) => NativeReady && IsReady(id);
        public async Task SendAsync(string id, HidFrame frame, CancellationToken token) { if (Wait is { } wait) await wait(token); HidReports.Validate(frame); Sent.Enqueue(frame); }
        public async Task<int> ReleaseAsync(string id, CancellationToken token)
        {
            if (Releases > 0) await SendAsync(id, HidReports.Keyboard(0, []), token);
            if (Releases > 1) await SendAsync(id, HidReports.Mouse(0), token);
            if (Releases > 2) await SendAsync(id, TouchpadReports.Neutral(), token);
            return Releases;
        }
    }
    [Fact] public void ContactEncodingMatchesTheDescriptorAndLittleEndianCoordinates()
    {
        var frame = TouchpadReports.Encode(0x1234, [new(0, 0x2345, 0xABCD, true), new(4, 65535, 100, true)], 2);
        HidReports.Validate(frame); Assert.Equal(28, frame.Payload.Length);
        Assert.Equal(new byte[] { 0x34,0x12,2,3,0x45,0x23,0xCD,0xAB }, frame.Payload.Take(8));
        Assert.Equal(19, frame.Payload[23]); Assert.Equal(65535, BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.AsSpan(24)));
        Assert.Equal(new byte[] { 121, 0 }, TouchpadReports.Feature(true)); Assert.Equal(new byte[] { 120, 0 }, TouchpadReports.Feature(false));
    }
    [Fact] public void DescriptorFitsGattAndHasFiveContactsFeatureAndExactly28InputBytes()
    {
        var descriptor = TouchpadReports.Descriptor(); Assert.True(descriptor.Length + HidDescriptors.ReportMapValue.Length <= 512);
        var globals = (Size: 0, Count: 0); var stack = new Stack<(int Size, int Count)>(); var input = 0; var feature = 0; var fingers = 0;
        for (var index = 0; index < descriptor.Length;)
        {
            var prefix = descriptor[index++]; var length = prefix & 3; if (length == 3) length = 4;
            var value = 0; for (var b = 0; b < length; b++) value |= descriptor[index++] << (b * 8);
            switch (prefix & 0xFC)
            {
                case 0x74: globals.Size = value; break; case 0x94: globals.Count = value; break;
                case 0xA4: stack.Push(globals); break; case 0xB4: globals = stack.Pop(); break;
                case 0x80: input += globals.Size * globals.Count; break; case 0xB0: feature += globals.Size * globals.Count; break;
                case 0x08: if (value == 0x22) fingers++; break;
            }
        }
        Assert.Equal(5, fingers); Assert.Equal(28 * 8, input); Assert.Equal(16, feature); Assert.Empty(stack);
    }
    [Fact] public void SlotsRemainStableAndUpRetainsTheLastPositionForOneFrame()
    {
        var state = new NativeTouchpadState();
        state.Update(1, [new(99, 100, 200, true, true), new(22, 300, 400, true, true)], 0);
        var moved = state.Update(2, [new(22, 350, 450, true, true), new(99, 150, 250, true, true)], 0);
        Assert.Equal(150, BinaryPrimitives.ReadUInt16LittleEndian(moved.Payload.AsSpan(4)));
        var up = state.Update(3, [new(22, 360, 460, true, true)], 0);
        Assert.Equal(2, up.Payload[3]); Assert.Equal(150, BinaryPrimitives.ReadUInt16LittleEndian(up.Payload.AsSpan(4))); Assert.Equal(1, state.ActiveCount);
        var later = state.Update(4, [new(22, 370, 470, true, true)], 0); Assert.Equal(0, later.Payload[3]);
        state.Reset(); Assert.Equal(0, state.ActiveCount);
    }
    [Fact] public void MalformedContactsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => TouchpadReports.Encode(0, [new(5, 0, 0, true)]));
        Assert.Throws<ArgumentException>(() => TouchpadReports.Encode(0, [new(1, 0, 0, true), new(1, 0, 0, true)]));
        var bytes = new byte[28]; bytes[3] = 0xFF;
        Assert.Throws<ArgumentException>(() => HidReports.Validate(new(ReportKind.Touchpad, bytes)));
    }
    [Fact] public async Task NativeActivationRequiresNativeReadinessAndThreeNeutralReports()
    {
        var transport = new Transport { NativeReady = false }; await using var sender = new ReportDispatcher(transport); var router = new InputRouter(transport, sender);
        router.Configure(new() { NativeTouchpad = true });
        Assert.False(await router.ActivateAsync("iPad", () => true)); transport.NativeReady = true; transport.Releases = 2;
        Assert.False(await router.ActivateAsync("iPad", () => true)); transport.Releases = 3;
        Assert.True(await router.ActivateAsync("iPad", () => true));
    }
    [Fact] public async Task ContactUpdatesAreAbsoluteAndPreserveDownUpTransitionsAndKeyboardInput()
    {
        var transport = new Transport(); await using var sender = new ReportDispatcher(transport); var router = new InputRouter(transport, sender);
        router.Configure(new() { NativeTouchpad = true }); await router.ActivateAsync("iPad", () => true);
        Assert.True(router.NativeTouchpadFrame(TouchpadReports.Encode(1, [new(0, 10, 20, true)]), true));
        for (ushort time = 2; time <= 60; time++) router.NativeTouchpadFrame(TouchpadReports.Encode(time, [new(0, time, time, true)]), true);
        router.Move(300, 100); router.ScrollTouchpad(0, 100); router.FlushMotion(); // No duplicated mouse/wheel during contacts.
        router.Key(0x41, 0x1E, false, true, 100); router.Key(0x41, 0x1E, false, false, 101);
        router.NativeTouchpadFrame(TouchpadReports.Encode(61, [new(0, 60, 60, false)]), false);
        await FakeTransport.Until(() => transport.Sent.Last().Kind == ReportKind.Touchpad && transport.Sent.Last().Payload[3] == 2);
        var contacts = transport.Sent.Where(f => f.Kind == ReportKind.Touchpad).Skip(1).ToArray();
        Assert.Equal(10, BinaryPrimitives.ReadUInt16LittleEndian(contacts[0].Payload.AsSpan(4)));
        Assert.Equal(60, BinaryPrimitives.ReadUInt16LittleEndian(contacts[^2].Payload.AsSpan(4)));
        Assert.Equal(2, contacts[^1].Payload[3]);
        Assert.Contains(transport.Sent, f => f.Kind == ReportKind.Keyboard && f.Payload[2] == 4);
        Assert.All(transport.Sent.Where(f => f.Kind == ReportKind.Mouse), f => Assert.All(f.Payload, b => Assert.Equal(0, b)));
    }
    [Fact] public async Task SlowTransportCoalescesOnlyContactUpdatesAndNeverTheDownUpEdges()
    {
        var transport = new Transport(); await using var sender = new ReportDispatcher(transport, 8); var router = new InputRouter(transport, sender);
        router.Configure(new() { NativeTouchpad = true }); await router.ActivateAsync("iPad", () => true);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.Wait = async ct => { entered.TrySetResult(); await resume.Task.WaitAsync(ct); };
        router.Key(0x41, 0x1E, false, true, 0); await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        router.NativeTouchpadFrame(TouchpadReports.Encode(1, [new(0, 10, 10, true)]), true);
        for (ushort i = 2; i < 100; i++) Assert.True(router.NativeTouchpadFrame(TouchpadReports.Encode(i, [new(0, i, i, true)]), true));
        router.NativeTouchpadFrame(TouchpadReports.Encode(100, [new(0, 99, 99, false)]), false);
        router.Key(0x41, 0x1E, false, false, 101); resume.SetResult();
        await FakeTransport.Until(() => transport.Sent.Count == 8);
        var sent = transport.Sent.ToArray();
        Assert.Equal(10, BinaryPrimitives.ReadUInt16LittleEndian(sent[4].Payload.AsSpan(4)));
        Assert.Equal(99, BinaryPrimitives.ReadUInt16LittleEndian(sent[5].Payload.AsSpan(4)));
        Assert.Equal(2, sent[6].Payload[3]); Assert.Equal(ReportKind.Keyboard, sent[7].Kind);
    }
    [Fact] public async Task LosingNativeReadinessReturnsLocalAndReleasesAllThreeReports()
    {
        var transport = new Transport(); await using var sender = new ReportDispatcher(transport); var router = new InputRouter(transport, sender);
        router.Configure(new() { NativeTouchpad = true }); await router.ActivateAsync("iPad", () => true);
        transport.NativeReady = false; router.CheckConnection();
        await FakeTransport.Until(() => !sender.Settling && transport.Sent.Count == 6);
        Assert.False(router.IsRemote); Assert.Equal(ReportKind.Touchpad, transport.Sent.Last().Kind);
        Assert.All(transport.Sent.Last().Payload, b => Assert.Equal(0, b));
    }
}
