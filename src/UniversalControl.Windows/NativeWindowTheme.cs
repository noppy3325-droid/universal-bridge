using System.Runtime.InteropServices;

namespace UniversalControl.Windows;

public sealed record TitleBarTheme(bool Success, bool Dark, uint Caption, uint Text, uint Border, int DarkResult, int CaptionResult, int TextResult, int BorderResult);
public static class NativeWindowTheme
{
    public static TitleBarTheme Apply(nint window, bool dark, bool highContrast = false)
    {
        var enabled = dark && !highContrast ? 1u : 0u;
        var caption = highContrast ? uint.MaxValue : dark ? 0x00291C16u : 0x00FAF6F4u;
        var text = highContrast ? uint.MaxValue : dark ? 0x00FCF3EEu : 0x003A2419u;
        var border = highContrast ? uint.MaxValue : dark ? 0x00564136u : 0x00EDE3DDu;
        // These are documented DwmSetWindowAttribute options; not all support DwmGetWindowAttribute.
        var modeResult = DwmSetWindowAttribute(window, 20, ref enabled, 4);
        var captionResult = DwmSetWindowAttribute(window, 35, ref caption, 4);
        var textResult = DwmSetWindowAttribute(window, 36, ref text, 4);
        var borderResult = DwmSetWindowAttribute(window, 34, ref border, 4);
        return new(modeResult == 0 && captionResult == 0 && textResult == 0 && borderResult == 0, enabled == 1, caption, text, border,
            modeResult, captionResult, textResult, borderResult);
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, int attribute, ref uint value, int size);
}
