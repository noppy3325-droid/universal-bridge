using System.ComponentModel;
using System.Runtime.InteropServices;
using UniversalControl.Core;

namespace UniversalControl.Windows;

/// <summary>Relays actual HIMETRIC contacts; never reconstructs fingers from gesture macros.</summary>
internal sealed class NativeTouchpadReader
{
    private readonly InputRouter _router;
    private readonly NativeTouchpadState _state = new();
    private readonly TouchInfo[] _buffer = new TouchInfo[64 * 5];
    private readonly HashSet<(nint Device, uint Frame)> _seen = [];
    private readonly Queue<(nint Device, uint Frame)> _order = [];
    private nint _device;
    public static bool Supported { get; } = CheckSupported();
    public bool Active => _state.ActiveCount > 0;
    public long Frames { get; private set; }
    public int MaximumContacts { get; private set; }
    public int MaximumValidContacts { get; private set; }
    public static NativeContactApiProbe Probe() => new(Supported, Marshal.SizeOf<PointerInfo>(), Marshal.SizeOf<TouchInfo>());
    public NativeTouchpadReader(InputRouter router) => _router = router;
    public void Reset() { _state.Reset(); _seen.Clear(); _order.Clear(); Array.Clear(_buffer); _device = 0; }
    private static bool CheckSupported()
    {
        var library = NativeLibrary.Load("user32.dll");
        try { return NativeLibrary.TryGetExport(library, "GetPointerFrameTouchpadInfoHistory", out _) && NativeLibrary.TryGetExport(library, "GetPointerFrameTouchpadInfo", out _); }
        finally { NativeLibrary.Free(library); }
    }
    public void Process(uint pointerId)
    {
        uint entries = 64, count = 5;
        if (!GetPointerFrameTouchpadInfoHistory(pointerId, ref entries, ref count, _buffer))
        {
            count = 5;
            if (!GetPointerFrameTouchpadInfo(pointerId, ref count, _buffer)) throw new Win32Exception(Marshal.GetLastWin32Error());
            entries = 1;
        }
        if (count is 0 or > 5 || entries > 64) throw new InvalidOperationException("Unsupported touchpad frame size.");
        // History is newest first. Stable frame IDs eliminate duplicates from per-finger events.
        for (var history = (int)entries - 1; history >= 0; history--)
        {
            var first = _buffer[history * (int)count].Pointer;
            var key = (first.Device, first.Frame);
            if (!_seen.Add(key)) continue;
            _order.Enqueue(key); if (_order.Count > 256) _seen.Remove(_order.Dequeue());
            var scan = first.Performance > 0
                ? unchecked((ushort)(ulong)(first.Performance * (10000d / System.Diagnostics.Stopwatch.Frequency)))
                : unchecked((ushort)(first.Time * 10));
            if (_device != 0 && _device != first.Device) { _router.NativeTouchpadFrame(TouchpadReports.Neutral(scan), false); _state.Reset(); }
            _device = first.Device;
            if (!GetPointerDeviceRects(first.Device, out var device, out _) || device.Right <= device.Left || device.Bottom <= device.Top)
                throw new InvalidOperationException("Touchpad coordinate range unavailable.");
            var samples = new List<NativeContact>((int)count); byte buttons = 0;
            var coordinateScale = Math.Min(10000d / (device.Right - (double)device.Left), 6000d / (device.Bottom - (double)device.Top));
            var offsetX = (10000 - (device.Right - (double)device.Left) * coordinateScale) / 2;
            var offsetY = (6000 - (device.Bottom - (double)device.Top) * coordinateScale) / 2;
            for (var index = 0; index < count; index++)
            {
                var point = _buffer[history * (int)count + index].Pointer;
                var down = (point.Flags & 0x0004) != 0 && (point.Flags & 0x48000) == 0;
                var confidence = (point.Flags & 0x4000) != 0;
                samples.Add(new(point.Id, Normalize(point.HimetricRaw.X, device.Left, coordinateScale, offsetX, 10000), Normalize(point.HimetricRaw.Y, device.Top, coordinateScale, offsetY, 6000), down, confidence));
                if ((point.Flags & 0x10) != 0) buttons |= 1;
                if ((point.Flags & 0x20) != 0) buttons |= 2;
            }
            var report = _state.Update(scan, samples, buttons);
            MaximumContacts = Math.Max(MaximumContacts, samples.Count(c => c.Down));
            MaximumValidContacts = Math.Max(MaximumValidContacts, _state.ActiveCount);
            if (!_router.NativeTouchpadFrame(report, _state.ActiveCount > 0)) return;
            Frames++;
        }
    }
    private static ushort Normalize(int value, int low, double scale, double offset, double extent)
        => (ushort)Math.Round(Math.Clamp(((value - (double)low) * scale + offset) / extent, 0, 1) * 65535);
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct PointerInfo
    {
        public uint Type, Id, Frame, Flags; public nint Device, Target;
        public Point Pixel, Himetric, PixelRaw, HimetricRaw;
        public uint Time, History; public int InputData; public uint KeyStates;
        public ulong Performance; public uint ButtonChange;
    }
    [StructLayout(LayoutKind.Sequential)] private struct TouchInfo
    { public PointerInfo Pointer; public uint Flags, Mask; public Rect Contact, ContactRaw; public uint Orientation, Pressure; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetPointerFrameTouchpadInfoHistory(uint id, ref uint entries, ref uint count, [In, Out] TouchInfo[] info);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetPointerFrameTouchpadInfo(uint id, ref uint count, [In, Out] TouchInfo[] info);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetPointerDeviceRects(nint device, out Rect deviceRect, out Rect displayRect);
}
