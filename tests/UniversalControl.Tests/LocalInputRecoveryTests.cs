using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class LocalInputRecoveryTests
{
    [Fact] public async Task RepeatedEmergencyReturnsAndMissedKeyUpsCannotLeaveAnIdleRouterBlocked()
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake);
        var router = new InputRouter(fake, sender);
        var random = new Random(1847);
        var keys = new[] { (0x41, 0x1E), (0xA2, 0x1D), (0xA4, 0x38), (0x51, 0x10), (0xC0, 0x29), (0xF4, 0x29), (0x1B, 1) };
        for (var cycle = 0; cycle < 100; cycle++)
        {
            await FakeTransport.Until(() => !sender.Settling);
            Assert.True(await router.ActivateAsync("iPad", () => true));
            for (var i = 0; i < 15; i++)
            {
                var key = keys[random.Next(keys.Length)];
                router.Key(key.Item1, (uint)key.Item2, random.Next(5) == 0, random.Next(2) == 0, i, true);
            }
            router.GoLocal(); router.ReconcilePhysical(_ => false);
            Assert.True(router.PhysicalIdle);
        }
    }

    [Theory] [InlineData(0xF3)] [InlineData(0xF4)]
    public async Task JisModeFlagCannotKeepMissedReleaseHeldAfterReturningLocal(int mode)
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender);
        Assert.True(await router.ActivateAsync("iPad", () => true));
        router.Key(mode, 0x29, false, true, 0, true);
        router.GoLocal();
        router.ReconcilePhysical(key => key == mode);
        Assert.True(router.PhysicalIdle);
        await FakeTransport.Until(() => !sender.Settling);
        Assert.True(await router.ActivateAsync("iPad", () => true));
    }

    [Theory] [InlineData(0xF3)] [InlineData(0xF4)]
    public async Task ActualHeldOemKeyStillBlocksAfterModeKeyNormalization(int mode)
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender);
        router.Key(mode, 0x29, false, true, 0, true);
        router.ReconcilePhysical(key => key is 0xC0 or 0xF4);
        Assert.False(router.PhysicalIdle); Assert.False(await router.ActivateAsync("iPad", () => true));
        router.ReconcilePhysical(key => key == mode);
        Assert.True(router.PhysicalIdle); Assert.True(await router.ActivateAsync("iPad", () => true));
    }

    [Fact] public async Task ReconciliationDoesNotDiscardAnUnmappedHardwareKeyOrRunWhileRemote()
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender);
        router.Key(0xF0, 0x7F, false, true, 0);
        router.ReconcilePhysical(key => key == 0xF0); Assert.False(router.PhysicalIdle);
        router.Key(0xF0, 0x7F, false, false, 1); Assert.True(await router.ActivateAsync("iPad", () => true));
        router.Key(0x41, 0x1E, false, true, 2); router.ReconcilePhysical(_ => false);
        Assert.False(router.PhysicalIdle); Assert.Equal(1, router.HeldState.TrackedKeys);
        router.GoLocal(); router.ReconcilePhysical(_ => false);
        Assert.True(router.PhysicalIdle); Assert.Equal(0, router.HeldState.ReleasePendingKeys);
    }
}
