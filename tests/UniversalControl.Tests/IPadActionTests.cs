using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class IPadActionTests
{
    private static void Chord(InputRouter router, int key, bool repeat = false)
    {
        router.Key(0xA2, 0x1D, false, true, 1); router.Key(0xA4, 0x38, false, true, 2);
        router.Key(key, 0, false, true, 3);
        if (repeat) for (var i = 0; i < 20; i++) router.Key(key, 0, false, true, 4 + i);
        router.Key(key, 0, false, false, 30);
        router.Key(0xA4, 0x38, false, false, 31); router.Key(0xA2, 0x1D, false, false, 32);
    }
    [Theory] [InlineData(0x48, 0x0B)] [InlineData(0x53, 0x2C)] [InlineData(0x4E, 0x2B)]
    public async Task RemoteActionSendsOneCommandWithoutTheLocalModifiersOrRepeating(int trigger, byte usage)
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender);
        await router.ActivateAsync("iPad", () => true); Chord(router, trigger, repeat: true);
        await FakeTransport.Until(() => fake.Sent.Count >= 10);
        var keys = fake.Sent.Select(packet => packet.Frame).Where(frame => frame.Kind == ReportKind.Keyboard).ToArray();
        Assert.Single(keys, frame => frame.Payload[0] == 8 && frame.Payload[2] == usage);
        var command = Array.FindIndex(keys, frame => frame.Payload[0] == 8 && frame.Payload[2] == usage);
        Assert.All(keys[command - 1].Payload, value => Assert.Equal(0, value));
        Assert.All(keys[command + 1].Payload, value => Assert.Equal(0, value));
        Assert.Equal(5, keys[command + 2].Payload[0]); // Both physical modifiers are restored until released.
        Assert.All(keys[^1].Payload, value => Assert.Equal(0, value));
        Assert.True(router.IsRemote); Assert.True(router.PhysicalIdle);
    }
    [Fact] public async Task ActionChordsRemainNormalOnWindowsAndCanBeDisabledOnIPad()
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender);
        Assert.False(router.Key(0x48, 0, false, true, 0)); Assert.False(router.Key(0x48, 0, false, false, 1)); Assert.Empty(fake.Sent);
        router.Configure(new() { IPadShortcutsEnabled = false }); await router.ActivateAsync("iPad", () => true); Chord(router, 0x48);
        await FakeTransport.Until(() => fake.Sent.Count == 8);
        Assert.Contains(fake.Sent, packet => packet.Frame.Kind == ReportKind.Keyboard && packet.Frame.Payload[0] == 5 && packet.Frame.Payload[2] == 0x0B);
        Assert.DoesNotContain(fake.Sent, packet => packet.Frame.Kind == ReportKind.Keyboard && packet.Frame.Payload[0] == 8);
    }
    [Fact] public async Task CustomBindingWorksAndActionDoesNotInterruptAnotherKeyOrDrag()
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender);
        router.Configure(new() { HomeShortcut = new(ShortcutModifiers.Control | ShortcutModifiers.Alt, 0x7B) });
        await router.ActivateAsync("iPad", () => true);
        router.Key(0x41, 0x1E, false, true, 0); Chord(router, 0x7B); router.Key(0x41, 0x1E, false, false, 40);
        router.Button(MouseButtons.Left, true); Chord(router, 0x7B); router.Button(MouseButtons.Left, false);
        Chord(router, 0x7B);
        await FakeTransport.Until(() => fake.Sent.Any(packet => packet.Frame.Kind == ReportKind.Keyboard && packet.Frame.Payload[0] == 8));
        Assert.Single(fake.Sent, packet => packet.Frame.Kind == ReportKind.Keyboard && packet.Frame.Payload[0] == 8);
    }
    [Fact] public void MigrationPreservesExistingBindingsAndRepairsActionConflicts()
    {
        var settings = new AppSettings { ActivateShortcut = ShortcutBinding.DefaultHome, HomeShortcut = null!, SearchShortcut = ShortcutBinding.DefaultHome }.Normalize();
        Assert.Equal(ShortcutBinding.DefaultHome, settings.ActivateShortcut);
        ShortcutBinding.ValidateAll(settings.ActivateShortcut, settings.ReturnShortcut, settings.HomeShortcut, settings.SearchShortcut, settings.NextAppShortcut);
        Assert.NotEqual(settings.ActivateShortcut, settings.HomeShortcut);
        Assert.Throws<ArgumentException>(() => ShortcutBinding.ValidateAll(ShortcutBinding.DefaultActivate, ShortcutBinding.DefaultReturn, ShortcutBinding.DefaultHome, ShortcutBinding.DefaultHome, ShortcutBinding.DefaultNextApp));
    }
    [Fact] public async Task DisconnectReleasesActionModifiersAndDoesNotReplayTheCommandAfterRecovery()
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender);
        await router.ActivateAsync("iPad", () => true); Chord(router, 0x48);
        await FakeTransport.Until(() => fake.Sent.Count == 10);
        router.GoLocal(); await FakeTransport.Until(() => !sender.Settling);
        await router.ActivateAsync("iPad", () => true); router.FlushMotion();
        Assert.All(fake.Sent.Last().Frame.Payload, value => Assert.Equal(0, value));
        Assert.Single(fake.Sent, packet => packet.Frame.Kind == ReportKind.Keyboard && packet.Frame.Payload[0] == 8);
    }
    [Fact] public async Task QueueFailureDuringActionCannotRestoreHeldModifiersIntoTheNextEpoch()
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake, 2); var router = new InputRouter(fake, sender);
        await router.ActivateAsync("iPad", () => true);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.BeforeSend = async ct => { entered.TrySetResult(); await resume.Task.WaitAsync(ct); };
        router.Key(0xA2, 0x1D, false, true, 0); await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        router.Key(0xA4, 0x38, false, true, 1); router.Key(0x48, 0, false, true, 2);
        Assert.False(router.IsRemote);
        router.Key(0x48, 0, false, false, 3); router.Key(0xA4, 0x38, false, false, 4); router.Key(0xA2, 0x1D, false, false, 5);
        resume.SetResult(); await FakeTransport.Until(() => !sender.Settling);
        fake.BeforeSend = null; Assert.True(await router.ActivateAsync("iPad", () => true));
        Assert.All(fake.Sent.Last().Frame.Payload, value => Assert.Equal(0, value));
        Assert.DoesNotContain(fake.Sent, packet => packet.Frame.Kind == ReportKind.Keyboard && packet.Frame.Payload[0] == 8);
        Assert.True(router.PhysicalIdle);
    }
}
