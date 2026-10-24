namespace UniversalControl.Core;

public sealed record ActionShortcut(GestureAction Action, ShortcutBinding Binding, bool Enabled = true);

public static class ActionShortcuts
{
    public static IReadOnlyList<ActionShortcut> Defaults => [
        new(GestureAction.PreviousApp, Chord('P')), new(GestureAction.Screenshot, Chord('C')),
        new(GestureAction.Undo, Chord('Z')), new(GestureAction.Redo, Chord('Y')),
        new(GestureAction.ZoomIn, Chord('U')), new(GestureAction.ZoomOut, Chord('D')),
        new(GestureAction.CloseWindow, Chord('W'), false)
    ];
    private static ShortcutBinding Chord(char key) => new(ShortcutModifiers.Control | ShortcutModifiers.Alt, key);
    public static string Label(GestureAction action) => action switch
    {
        GestureAction.PreviousApp => L.T("前のアプリ"), GestureAction.Screenshot => L.T("スクリーンショット"),
        GestureAction.Undo => L.T("取り消す"), GestureAction.Redo => L.T("やり直す"), GestureAction.ZoomIn => L.T("拡大"),
        GestureAction.ZoomOut => L.T("縮小"), GestureAction.CloseWindow => L.T("ウィンドウを閉じる"), _ => action.ToString()
    };
    public static IReadOnlyList<ActionShortcut> Normalize(IReadOnlyList<ActionShortcut>? requested, HashSet<ShortcutBinding> used)
    {
        var result = new List<ActionShortcut>();
        foreach (var fallback in Defaults)
        {
            var entry = requested?.FirstOrDefault(item => item is not null && item.Action == fallback.Action) ?? fallback;
            result.Add(entry with { Binding = ShortcutBinding.Resolve(entry.Binding, fallback.Binding, used) });
        }
        return result;
    }
    public static void Validate(IReadOnlyList<ActionShortcut> entries, params ShortcutBinding[] reserved)
    {
        if (entries.Count != Defaults.Count || entries.Select(entry => entry.Action).Distinct().Count() != Defaults.Count ||
            entries.Any(entry => !Defaults.Any(item => item.Action == entry.Action) || entry.Binding?.Valid != true))
            throw new ArgumentException(L.T("操作ショートカットの設定が正しくありません。"));
        var bindings = reserved.Concat(entries.Select(entry => entry.Binding)).ToArray();
        if (bindings.Distinct().Count() != bindings.Length) throw new ArgumentException(L.T("すべての操作に別のショートカットを設定してください。"));
    }
}
