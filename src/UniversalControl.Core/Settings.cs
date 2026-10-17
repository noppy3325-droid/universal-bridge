using System.Text.Json;

namespace UniversalControl.Core;

public enum ScreenEdge { Right, Left, Top, Bottom }
public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;
    public string Theme { get; init; } = "Light";
    public string Language { get; init; } = "auto";
    public double MouseSensitivity { get; init; } = 1;
    public bool RawMouse { get; init; } = true;
    public bool CaptureTouchpad { get; init; } = true;
    public ShortcutBinding ActivateShortcut { get; init; } = ShortcutBinding.DefaultActivate;
    public ShortcutBinding ReturnShortcut { get; init; } = ShortcutBinding.DefaultReturn;
    public GestureAction SwipeLeft { get; init; } = GestureAction.NextApp;
    public GestureAction SwipeRight { get; init; } = GestureAction.PreviousApp;
    public GestureAction SwipeUp { get; init; } = GestureAction.NextApp;
    public GestureAction SwipeDown { get; init; } = GestureAction.Home;
    public double ScrollSensitivity { get; init; } = 1;
    public bool AutoSwitch { get; init; }
    public ScreenEdge Edge { get; init; } = ScreenEdge.Right;
    public string MonitorId { get; init; } = "";
    public double PortalStart { get; init; }
    public double PortalEnd { get; init; } = 1;
    public int EdgeDwellMs { get; init; } = 300;
    public bool EstimatedReturn { get; init; }
    public int VirtualWidth { get; init; } = 1366;
    public int VirtualHeight { get; init; } = 1024;
    public bool CloseToTray { get; init; } = true;
    public bool Reconnect { get; init; } = true;
    public bool AutoStart { get; init; }

    public AppSettings Normalize()
    {
        var start = double.IsFinite(PortalStart) ? Math.Clamp(PortalStart, 0, .8) : 0;
        var validShortcuts = ActivateShortcut?.Valid == true && ReturnShortcut?.Valid == true && ActivateShortcut != ReturnShortcut;
        return this with
        {
        SchemaVersion = 1, Theme = Theme == "Dark" ? "Dark" : "Light",
        Language = Language is "auto" or "ja" or "en" or "zh-Hans" ? Language : "auto",
        ActivateShortcut = validShortcuts ? ActivateShortcut! : ShortcutBinding.DefaultActivate,
        ReturnShortcut = validShortcuts ? ReturnShortcut! : ShortcutBinding.DefaultReturn,
        SwipeLeft = Enum.IsDefined(SwipeLeft) ? SwipeLeft : GestureAction.NextApp,
        SwipeRight = Enum.IsDefined(SwipeRight) ? SwipeRight : GestureAction.PreviousApp,
        SwipeUp = Enum.IsDefined(SwipeUp) ? SwipeUp : GestureAction.NextApp,
        SwipeDown = Enum.IsDefined(SwipeDown) ? SwipeDown : GestureAction.Home,
        MouseSensitivity = double.IsFinite(MouseSensitivity) ? Math.Round(Math.Clamp(MouseSensitivity, .02, 4), 2) : 1,
        ScrollSensitivity = double.IsFinite(ScrollSensitivity) ? Math.Clamp(ScrollSensitivity, .1, 4) : 1,
        Edge = Enum.IsDefined(Edge) ? Edge : ScreenEdge.Right,
        MonitorId = MonitorId ?? "",
        PortalStart = start,
        PortalEnd = double.IsFinite(PortalEnd) ? Math.Clamp(PortalEnd, start + .2, 1) : 1,
        EdgeDwellMs = Math.Clamp(EdgeDwellMs, 150, 1500),
        VirtualWidth = Math.Clamp(VirtualWidth, 320, 8192), VirtualHeight = Math.Clamp(VirtualHeight, 320, 8192)
        };
    }
}

public sealed class SettingsStore(string directory)
{
    private readonly string _path = Path.Combine(directory, "settings.json");
    public string? Warning { get; private set; }
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new();
            var value = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path));
            if (value is null || value.SchemaVersion != 1) throw new JsonException("Unsupported settings version.");
            return value.Normalize();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            Warning = L.T("設定を読み込めないため初期値を使用します: ") + ex.Message;
            return new();
        }
    }
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(directory);
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(settings.Normalize(), new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, _path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
