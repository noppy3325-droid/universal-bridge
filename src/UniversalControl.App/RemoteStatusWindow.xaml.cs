using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UniversalControl.Core;
using Size = System.Windows.Size;

namespace UniversalControl.App;

public partial class RemoteStatusWindow : Window
{
    private nint _handle;
    public RemoteStatusWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            _handle = new WindowInteropHelper(this).Handle;
            var style = GetWindowLongPtr(_handle, -20).ToInt64();
            SetWindowLongPtr(_handle, -20, new nint(style | 0x080000A0L)); // NOACTIVATE | TOOLWINDOW | TRANSPARENT; WPF supplies LAYERED.
            HwndSource.FromHwnd(_handle).AddHook((nint window, int message, nint wp, nint lp, ref bool handled) =>
            {
                if (message == 0x84) { handled = true; return new nint(-1); } // HTTRANSPARENT
                if (message == 0x21) { handled = true; return new nint(3); } // MA_NOACTIVATE
                return 0;
            });
        };
    }
    public void SetText(ShortcutBinding binding)
    {
        StatusHeading.Text = L.T("iPadを操作中"); ReturnKey.Text = binding.ToString(); ReturnHint.Text = L.T("で戻る · Esc 3回");
    }
    public void ShowFor(DisplayInfo display, ShortcutBinding binding)
    {
        SetText(binding);
        _handle = new WindowInteropHelper(this).EnsureHandle();
        var monitor = MonitorFromPoint(new NativePoint(display.Left + display.Width / 2, display.Top + display.Height / 2), 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return;
        // Move while still hidden to obtain the destination monitor's DPI without a visible jump.
        if (!IsVisible) SetWindowPos(_handle, new nint(-1), info.Work.Right - 330, info.Work.Bottom - 150, 310, 118, 0x10);
        var scale = Math.Max(1, GetDpiForWindow(_handle)) / 96d;
        var width = Math.Min(info.Work.Right - info.Work.Left, (int)Math.Ceiling(310 * scale));
        var height = Math.Min(info.Work.Bottom - info.Work.Top, (int)Math.Ceiling(118 * scale));
        var gap = (int)Math.Round(9 * scale);
        var x = Math.Max(info.Work.Left, info.Work.Right - width - gap);
        var y = Math.Max(info.Work.Top, info.Work.Bottom - height - gap);
        SetWindowPos(_handle, new nint(-1), x, y, width, height, 0x10);
        if (!IsVisible) Show();
    }
    public bool PassiveStylesVerified
    {
        get { var handle = new WindowInteropHelper(this).EnsureHandle(); return (GetWindowLongPtr(handle, -20).ToInt64() & 0x080800A0L) == 0x080800A0L && !ShowActivated && !ShowInTaskbar && !IsHitTestVisible; }
    }
    public bool VerifyPlacement(DisplayInfo display, ShortcutBinding binding)
    {
        var foreground = GetForegroundWindow(); ShowFor(display, binding);
        var monitor = MonitorFromPoint(new NativePoint(display.Left + display.Width / 2, display.Top + display.Height / 2), 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        var valid = GetMonitorInfo(monitor, ref info) && GetWindowRect(_handle, out var bounds) &&
            bounds.Left >= info.Work.Left && bounds.Top >= info.Work.Top && bounds.Right <= info.Work.Right && bounds.Bottom <= info.Work.Bottom &&
            GetForegroundWindow() == foreground && PassiveStylesVerified;
        Hide(); return valid;
    }
    public void CapturePreview(string path, ShortcutBinding binding)
    {
        SetText(binding);
        var root = (FrameworkElement)Content; root.Measure(new Size(310, 118)); root.Arrange(new Rect(0, 0, 310, 118)); root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(310, 118, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var output = File.Create(path); png.Save(output);
    }
    [StructLayout(LayoutKind.Sequential)] private readonly record struct NativePoint(int X, int Y);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Bounds, Work; public uint Flags; }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(NativePoint point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out NativeRect bounds);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
}
