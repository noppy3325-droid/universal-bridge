namespace UniversalControl.Core;

[Flags] public enum ShortcutModifiers { Alt = 1, Control = 2, Shift = 4 }
public sealed record ShortcutBinding(ShortcutModifiers Modifiers, int Key)
{
    public static ShortcutBinding DefaultActivate { get; } = new(ShortcutModifiers.Control | ShortcutModifiers.Alt, 0x49);
    public static ShortcutBinding DefaultReturn { get; } = new(ShortcutModifiers.Control | ShortcutModifiers.Alt, 0x51);
    public static ShortcutBinding DefaultHome { get; } = new(ShortcutModifiers.Control | ShortcutModifiers.Alt, 0x48);
    public static ShortcutBinding DefaultSearch { get; } = new(ShortcutModifiers.Control | ShortcutModifiers.Alt, 0x53);
    public static ShortcutBinding DefaultNextApp { get; } = new(ShortcutModifiers.Control | ShortcutModifiers.Alt, 0x4E);
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
    public static void ValidateAll(ShortcutBinding activate, ShortcutBinding back, ShortcutBinding home, ShortcutBinding search, ShortcutBinding nextApp)
    {
        ValidatePair(activate, back);
        var bindings = new[] { activate, back, home, search, nextApp };
        if (bindings.Any(binding => binding?.Valid != true)) throw new ArgumentException(L.T("ショートカットはCtrlまたはAltを含む英数字・F1～F24から選択してください。"));
        if (bindings.Distinct().Count() != bindings.Length) throw new ArgumentException(L.T("切り替え・復帰・ホーム・検索・次のアプリには、それぞれ別のショートカットを設定してください。"));
    }
    internal static ShortcutBinding Resolve(ShortcutBinding? requested, ShortcutBinding fallback, HashSet<ShortcutBinding> used)
    {
        // Keep existing activation/return bindings when migrating an older profile.
        foreach (var candidate in new[] { requested, fallback }.Concat(Enumerable.Range(0x70, 24).Select(key => new ShortcutBinding(ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Shift, key))))
            if (candidate?.Valid == true && used.Add(candidate)) return candidate;
        throw new InvalidOperationException("No unused shortcut available.");
    }
}
