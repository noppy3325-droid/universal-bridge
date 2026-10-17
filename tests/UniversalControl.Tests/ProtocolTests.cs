using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class ProtocolTests
{
    [Fact] public void KeyboardUsesEightBytesWithoutGattReportId() => Assert.Equal(new byte[] { 2, 0, 4, 5, 0, 0, 0, 0 }, HidReports.Keyboard(2, [5, 4, 4]).Payload);
    [Fact] public void RolloverIsExplicitInsteadOfDroppingHeldKeys() => Assert.Equal(new byte[] { 0, 0, 1, 1, 1, 1, 1, 1 }, HidReports.Keyboard(0, [4, 5, 6, 7, 8, 9, 10]).Payload);
    [Fact] public void MouseIsSignedLittleEndian() => Assert.Equal(new byte[] { 3, 0xFE, 0xFF, 0x2C, 1, 0xFF, 0 }, HidReports.Mouse(MouseButtons.Left | MouseButtons.Right, -2, 300, -1).Payload);
    [Theory]
    [InlineData(40000, 32767)] [InlineData(-40000, -32767)]
    public void MouseLimitsMatchDescriptor(int value, int expected)
    {
        var bytes = HidReports.Mouse(0, value, 0, value).Payload;
        Assert.Equal(expected, System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(1)));
        Assert.Equal(Math.Clamp(value, -127, 127), unchecked((sbyte)bytes[5]));
    }
    [Theory]
    [InlineData(ReportKind.Keyboard, 7)] [InlineData(ReportKind.Mouse, 8)]
    public void InvalidReportLengthIsRejected(ReportKind kind, int size) => Assert.Throws<ArgumentException>(() => HidReports.Validate(new(kind, new byte[size])));
    [Fact] public void ReservedBytesAndUnsupportedButtonsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => HidReports.Validate(new(ReportKind.Keyboard, [0, 1, 0, 0, 0, 0, 0, 0])));
        Assert.Throws<ArgumentException>(() => HidReports.Validate(new(ReportKind.Mouse, [8, 0, 0, 0, 0, 0])));
    }
    [Theory]
    [InlineData(0x47, false, 0x5F)] [InlineData(0x73, false, 0x87)] [InlineData(0x7D, false, 0x89)]
    [InlineData(0x70, false, 0x88)] [InlineData(0x79, false, 0x8A)] [InlineData(0x7B, false, 0x8B)]
    [InlineData(0x1C, true, 0x58)] [InlineData(0x1A, false, 0x2F)]
    public void PhysicalNumpadAndJisMappings(int scan, bool extended, int expected)
    {
        Assert.True(VirtualKeyMap.TryGetUsage(scan == 0x1C ? 0x0D : 0x24, (uint)scan, extended, out var usage)); Assert.Equal(expected, usage);
    }
    [Fact] public void ExtendedNavigationIsNotNumpad() { Assert.True(VirtualKeyMap.TryGetUsage(0x24, 0x47, true, out var usage)); Assert.Equal(0x4A, usage); }
    [Theory] [InlineData(0xA2, 1)] [InlineData(0xA3, 16)] [InlineData(0xA5, 64)] [InlineData(0x5C, 128)]
    public void LeftAndRightModifiersRemainDistinct(int vk, int mask) => Assert.Equal(mask, HidReports.Modifier(vk, 0, false));

    [Fact] public void DescriptorBitLengthsMatchEncodersAndJisFitsUsageRange()
    {
        var map = HidDescriptors.ReportMapValue; var bits = new Dictionary<(int Id, int Type), int>();
        var size = 0; var count = 0; var id = 0; var depth = 0; var maxUsage = 0; var page = 0;
        for (var i = 0; i < map.Length;)
        {
            var prefix = map[i++]; var length = prefix & 3; if (length == 3) length = 4;
            var value = 0; for (var b = 0; b < length; b++) value |= map[i++] << (8 * b);
            switch (prefix & 0xFC)
            {
                case 0x74: size = value; break; case 0x94: count = value; break; case 0x84: id = value; break;
                case 0xA0: depth++; break; case 0xC0: depth--; Assert.True(depth >= 0); break;
                case 0x80: bits[(id, 1)] = bits.GetValueOrDefault((id, 1)) + size * count; break;
                case 0x90: bits[(id, 2)] = bits.GetValueOrDefault((id, 2)) + size * count; break;
                case 0x04: page = value; break;
                case 0x28: if (page == 7 && value < 0xE0) maxUsage = Math.Max(maxUsage, value); break;
            }
        }
        Assert.Equal(0, depth); Assert.Equal(64, bits[(1, 1)]); Assert.Equal(8, bits[(1, 2)]); Assert.Equal(56, bits[(2, 1)]); Assert.True(maxUsage >= 0x8B);
    }
}
