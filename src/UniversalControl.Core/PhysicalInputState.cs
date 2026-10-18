namespace UniversalControl.Core;

/// <summary>Poll physical keys, not IME mode flags, packet input, or reserved virtual keys.</summary>
public static class PhysicalInputState
{
    // The JIS half/full key can arrive as VK_DBE_SBCSCHAR/DBCSCHAR. Their asynchronous
    // bits describe IME mode, not whether the physical OEM key is still held.
    public static int PollingKey(int virtualKey, uint scanCode, bool extended)
        => !extended && scanCode == 0x29 && virtualKey is 0xF3 or 0xF4 ? 0xC0 : virtualKey;

    public static bool IsIdle(Func<int, bool> isDown)
    {
        for (var key = 1; key < 256; key++)
            if (IsPhysicalKey(key) && isDown(key)) return false;
        return true;
    }

    private static bool IsPhysicalKey(int key) =>
        VirtualKeyMap.TryGetUsage(key, out _) || HidReports.Modifier(key, 0, false) != 0 ||
        key is 1 or 2 or 4 or 5 or 6 // Mouse buttons, including buttons not forwarded to iPad.
            or 0x03 or 0x0C // Cancel, Clear.
            or 0x15 or 0x19 or 0x1C or 0x1D or 0x1F // IME hardware keys, including JIS conversion keys.
            or >= 0x7C and <= 0x87 // F13-F24.
            or >= 0xA6 and <= 0xB7; // Browser, volume and media keys.
    // VK_DBE_* (0xF0 onward) can remain set as IME mode state without a held key.
    // Real hook events, including unmapped keys, are independently tracked by InputRouter.
}
