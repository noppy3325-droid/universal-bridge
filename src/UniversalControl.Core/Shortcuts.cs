namespace UniversalControl.Core;

[Flags] public enum ShortcutModifiers { Alt = 1, Control = 2, Shift = 4 }
public sealed record ShortcutBinding(ShortcutModifiers Modifiers, int Key)
{
    public static ShortcutBinding DefaultActivate { get; } = new(ShortcutModifiers.Control | ShortcutModifiers.Alt, 0x49);
    public static ShortcutBinding DefaultReturn { get; } = new(ShortcutModifiers.Control | ShortcutModifiers.Alt, 0x51);
    public bool Valid => ((int)Modifiers & ~7) == 0 && (Modifiers & (ShortcutModifiers.Control | ShortcutModifiers.Alt)) != 0 &&
        (Key is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A or >= 0x70 and <= 0x87);
    public bool Matches(int key, byte hidModifiers)
    {
        var actual = ((hidModifiers & 0x11) != 0 ? ShortcutModifiers.Control : 0) |
            ((hidModifiers & 0x44) != 0 ? ShortcutModifiers.Alt : 0) | ((hidModifiers & 0x22) != 0 ? ShortcutModifiers.Shift : 0);
        return key == Key && (hidModifiers & 0x88) == 0 && actual == Modifiers;
    }
    public string KeyLabel => Key is >= 0x70 and <= 0x87 ? "F" + (Key - 0x6F) : ((char)Key).ToString();
    public override string ToString() => string.Join(" + ", new[] {
        Modifiers.HasFlag(ShortcutModifiers.Control) ? "Ctrl" : null, Modifiers.HasFlag(ShortcutModifiers.Alt) ? "Alt" : null,
        Modifiers.HasFlag(ShortcutModifiers.Shift) ? "Shift" : null, KeyLabel }.Where(s => s is not null));
    public static void ValidatePair(ShortcutBinding? activate, ShortcutBinding? back)
    {
        if (activate?.Valid != true || back?.Valid != true) throw new ArgumentException(L.T("ショートカットはCtrlまたはAltを含む英数字・F1～F24から選択してください。"));
        if (activate == back) throw new ArgumentException(L.T("iPadへ移る操作とWindowsへ戻る操作には別のキーを設定してください。"));
    }
}
