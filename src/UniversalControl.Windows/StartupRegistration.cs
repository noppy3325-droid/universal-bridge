using Microsoft.Win32;

namespace UniversalControl.Windows;

public static class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "UniversalBridge";
    public static bool Enabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(KeyPath); return key?.GetValue(ValueName) is string; }
    }
    public static void Set(bool enabled, string executable)
    {
        if (enabled && Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(L.T("自動起動は配布版のUniversalBridge.exeから設定してください。"));
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (enabled) key.SetValue(ValueName, $"\"{Path.GetFullPath(executable)}\" --background", RegistryValueKind.String);
        else key.DeleteValue(ValueName, false);
    }
}
