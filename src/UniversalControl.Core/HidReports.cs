using System.Buffers.Binary;

namespace UniversalControl.Core;

[Flags]
public enum MouseButtons : byte { None = 0, Left = 1, Right = 2, Middle = 4 }
public enum ReportKind { Keyboard, Mouse, Touchpad }
public sealed record HidFrame(ReportKind Kind, byte[] Payload);

public static class HidReports
{
    public static HidFrame Keyboard(byte modifiers, IEnumerable<byte> usages)
    {
        var keys = usages.Where(x => x > 0 && x < 0xE0).Distinct().Order().ToArray();
        var bytes = new byte[8];
        bytes[0] = modifiers;
        if (keys.Length > 6) Array.Fill(bytes, (byte)1, 2, 6); // HID ErrorRollOver
        else keys.CopyTo(bytes, 2);
        return new(ReportKind.Keyboard, bytes);
    }

    public static HidFrame Mouse(MouseButtons buttons, int dx = 0, int dy = 0, int wheel = 0, int pan = 0)
    {
        var bytes = new byte[7];
        bytes[0] = (byte)((byte)buttons & 7);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(1), (short)Math.Clamp(dx, -32767, 32767));
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(3), (short)Math.Clamp(dy, -32767, 32767));
        bytes[5] = unchecked((byte)(sbyte)Math.Clamp(wheel, -127, 127));
        bytes[6] = unchecked((byte)(sbyte)Math.Clamp(pan, -127, 127));
        return new(ReportKind.Mouse, bytes);
    }

    public static void Validate(HidFrame frame)
    {
        if (!Enum.IsDefined(frame.Kind)) throw new ArgumentException("Unknown report kind.");
        if (frame.Payload.Length != (frame.Kind == ReportKind.Keyboard ? 8 : frame.Kind == ReportKind.Mouse ? 7 : TouchpadReports.Length))
            throw new ArgumentException("HID report length does not match the Report Map.");
        if (frame.Kind == ReportKind.Touchpad)
        {
            if (frame.Payload[2] > 3) throw new ArgumentException("Invalid touchpad buttons.");
            for (var slot = 0; slot < 5; slot++)
            {
                var flags = frame.Payload[3 + slot * 5];
                if (flags != 0 && flags >> 2 != slot) throw new ArgumentException("Invalid touchpad contact ID.");
            }
        }
        if (frame.Kind == ReportKind.Keyboard && frame.Payload[1] != 0)
            throw new ArgumentException("Keyboard reserved byte must be zero.");
        if (frame.Kind == ReportKind.Mouse && (frame.Payload[0] & ~7) != 0)
            throw new ArgumentException("Unsupported mouse button bits.");
        if (frame.Kind == ReportKind.Mouse && (BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.AsSpan(1)) == short.MinValue ||
            BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.AsSpan(3)) == short.MinValue || frame.Payload[5] == 0x80 || frame.Payload[6] == 0x80))
            throw new ArgumentException("Mouse values are outside the Report Map logical range.");
    }

    public static byte Modifier(int vk, uint scan, bool extended) => vk switch
    {
        0xA2 => 1, 0xA0 => 2, 0xA4 => 4, 0x5B => 8,
        0xA3 => 16, 0xA1 => 32, 0xA5 => 64, 0x5C => 128,
        0x11 => extended ? (byte)16 : (byte)1,
        0x10 => scan == 0x36 ? (byte)32 : (byte)2,
        0x12 => extended ? (byte)64 : (byte)4, _ => 0
    };
}
