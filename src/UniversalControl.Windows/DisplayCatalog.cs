using System.Runtime.InteropServices;
using UniversalControl.Core;

namespace UniversalControl.Windows;

public static class DisplayCatalog
{
    public static IReadOnlyList<DisplayInfo> Read()
    {
        List<DisplayInfo> result = [];
        MonitorEnum callback = (nint monitor, nint dc, ref Rect bounds, nint data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>(), Device = "" };
            if (GetMonitorInfo(monitor, ref info)) result.Add(new(info.Device, info.Bounds.Left, info.Bounds.Top,
                info.Bounds.Right - info.Bounds.Left, info.Bounds.Bottom - info.Bounds.Top, (info.Flags & 1) != 0));
            return true;
        };
        if (!EnumDisplayMonitors(0, 0, callback, 0) || result.Count == 0) throw new InvalidOperationException(L.T("ディスプレイ情報を取得できません。"));
        return result.OrderByDescending(m => m.Primary).ThenBy(m => m.Left).ToArray();
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    { public int Size; public Rect Bounds, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
    private delegate bool MonitorEnum(nint monitor, nint dc, ref Rect bounds, nint data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorEnum callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
}
