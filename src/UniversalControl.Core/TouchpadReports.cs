using System.Buffers.Binary;

namespace UniversalControl.Core;

public readonly record struct TouchContact(byte Slot, ushort X, ushort Y, bool Down, bool Confidence = true);

/// <summary>Five real contacts, not reconstructed gestures. 100 x 60 mm virtual surface; coordinates normalized to 65535.</summary>
public static class TouchpadReports
{
    public const byte ReportId = 3;
    public const int Length = 28;
    public static HidFrame Encode(ushort scanTime, IReadOnlyList<TouchContact> contacts, byte buttons = 0)
    {
        if (contacts.Count > 5 || buttons > 3 || contacts.Select(c => c.Slot).Distinct().Count() != contacts.Count || contacts.Any(c => c.Slot > 4))
            throw new ArgumentException("Invalid touchpad contacts.");
        var bytes = new byte[Length]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, scanTime); bytes[2] = buttons;
        foreach (var contact in contacts)
        {
            var offset = 3 + contact.Slot * 5;
            bytes[offset] = (byte)((contact.Slot << 2) | (contact.Down ? 1 : 0) | (contact.Confidence ? 2 : 0));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 1), contact.X);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 3), contact.Y);
        }
        return new(ReportKind.Touchpad, bytes);
    }
    public static HidFrame Neutral(ushort scanTime = 0) => Encode(scanTime, []);
    public static byte[] Feature(bool surfaceOn) => [(byte)((60 << 1) | (surfaceOn ? 1 : 0)), 0];

    public static byte[] Descriptor()
    {
        var result = new List<byte>
        {
            0x05,0x0D, 0x09,0x05, 0xA1,0x01, 0x85,ReportId,
            0x15,0x00, 0x27,0xFF,0xFF,0x00,0x00, 0x09,0x56, 0x75,0x10, 0x95,0x01, 0x81,0x02,
            0x25,0x01, 0x09,0x57, 0x75,0x01, 0xB1,0x02,
            0x26,0xFF,0x7F, 0x09,0xA1, 0x75,0x0F, 0xB1,0x02,
            0x05,0x09, 0x19,0x01, 0x29,0x02, 0x25,0x01, 0x75,0x01, 0x95,0x02, 0x81,0x02,
            0x95,0x06, 0x81,0x01
        };
        for (var i = 0; i < 5; i++) result.AddRange(new byte[]
        {
            0xA4, // Preserve units/physical bounds across contact collections.
            0x05,0x0D, 0x09,0x22, 0xA1,0x00,
            0x25,0x01, 0x09,0x42, 0x09,0x47, 0x75,0x01, 0x95,0x02, 0x81,0x02,
            0x25,0x04, 0x09,0x51, 0x75,0x06, 0x95,0x01, 0x81,0x02,
            0x05,0x01, 0x27,0xFF,0xFF,0x00,0x00,
            0x46,0x10,0x27, 0x55,0x0D, 0x65,0x11, 0x09,0x30, 0x75,0x10, 0x95,0x01, 0x81,0x02,
            0x46,0x70,0x17, 0x09,0x31, 0x81,0x02, 0xC0, 0xB4
        });
        result.Add(0xC0); return result.ToArray();
    }
}

public readonly record struct NativeContact(uint Id, ushort X, ushort Y, bool Down, bool Confidence);

/// <summary>Stable slots; release keeps the previous position/confidence for one report.</summary>
public sealed class NativeTouchpadState
{
    private Dictionary<uint, TouchContact> _contacts = [];
    public int ActiveCount => _contacts.Values.Count(c => c.Down && c.Confidence);
    public HidFrame Update(ushort scanTime, IReadOnlyList<NativeContact> input, byte buttons)
    {
        if (input.Count > 5 || input.Select(c => c.Id).Distinct().Count() != input.Count) throw new ArgumentException("Invalid contact frame.");
        var next = _contacts.Where(p => p.Value.Down).ToDictionary();
        foreach (var id in next.Keys.ToArray())
        {
            var current = input.FirstOrDefault(c => c.Id == id);
            if (!input.Any(c => c.Id == id && c.Down)) next[id] = next[id] with { Down = false };
            else next[id] = next[id] with { X = current.X, Y = current.Y, Confidence = current.Confidence };
        }
        foreach (var contact in input.Where(c => c.Down && !next.ContainsKey(c.Id)))
        {
            var slot = Enumerable.Range(0, 5).FirstOrDefault(s => next.Values.All(c => c.Slot != s), -1);
            if (slot < 0) throw new InvalidOperationException("Touchpad contact transition exceeds five slots.");
            next[contact.Id] = new((byte)slot, contact.X, contact.Y, true, contact.Confidence);
        }
        var report = TouchpadReports.Encode(scanTime, next.Values.OrderBy(c => c.Slot).ToArray(), buttons);
        _contacts = next; return report;
    }
    public void Reset() => _contacts.Clear();
}
