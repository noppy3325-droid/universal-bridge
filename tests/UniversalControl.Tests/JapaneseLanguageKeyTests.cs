using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class JapaneseLanguageKeyTests
{
    [Theory] [InlineData(0xC0, true)] [InlineData(0x19, false)] [InlineData(0xF3, false)] [InlineData(0xF4, false)]
    public async Task HalfFullKeySendsOneControlSpaceTapWithoutPunctuation(int vk, bool japanese)
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender);
        await router.ActivateAsync("iPad", () => true);
        Assert.True(router.Key(vk, 0x29, false, true, 0, japanese));
        router.Key(vk, 0x29, false, true, 1, japanese); // OS auto-repeat must not cycle repeatedly.
        router.Key(vk, 0x29, false, false, 2, japanese);
        await FakeTransport.Until(() => fake.Sent.Count == 4);
        Assert.Equal(new byte[] { 1, 0, 0x2C, 0, 0, 0, 0, 0 }, fake.Sent.ToArray()[2].Frame.Payload);
        Assert.All(fake.Sent.Last().Frame.Payload, b => Assert.Equal(0, b)); Assert.True(router.PhysicalIdle);
        router.Key(0x41, 0x1E, false, true, 3, japanese); router.Key(0x41, 0x1E, false, false, 4, japanese);
        await FakeTransport.Until(() => fake.Sent.Count == 6);
        Assert.Equal(4, fake.Sent.ToArray()[4].Frame.Payload[2]);
    }

    [Fact] public async Task AltHalfFullRestoresModifierAndAllowsItsRelease()
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender);
        await router.ActivateAsync("iPad", () => true);
        router.Key(0xA4, 0x38, false, true, 0);
        router.Key(0xC0, 0x29, false, true, 1, true); router.Key(0xC0, 0x29, false, false, 2, true);
        router.Key(0xA4, 0x38, false, false, 3);
        await FakeTransport.Until(() => fake.Sent.Count == 6);
        Assert.Equal(1, fake.Sent.ToArray()[3].Frame.Payload[0]); Assert.Equal(4, fake.Sent.ToArray()[4].Frame.Payload[0]);
        Assert.All(fake.Sent.Last().Frame.Payload, b => Assert.Equal(0, b));
    }

    [Fact] public async Task UsBacktickAndLocalJapaneseInputRetainTheirUsualBehavior()
    {
        var fake = new FakeTransport(); await using var sender = new ReportDispatcher(fake); var router = new InputRouter(fake, sender);
        Assert.False(router.Key(0xC0, 0x29, false, true, 0, true));
        Assert.False(router.Key(0xC0, 0x29, false, false, 1, true)); Assert.Empty(fake.Sent);
        await router.ActivateAsync("iPad", () => true);
        router.Key(0xC0, 0x29, false, true, 2); router.Key(0xC0, 0x29, false, false, 3);
        await FakeTransport.Until(() => fake.Sent.Count == 4);
        Assert.Equal(0x35, fake.Sent.ToArray()[2].Frame.Payload[2]);
        Assert.False(VirtualKeyMap.IsJapaneseLanguageKey(0xC0, 0x28, false, true));
        Assert.False(VirtualKeyMap.IsJapaneseLanguageKey(0xC0, 0x29, true, true));
    }
}
