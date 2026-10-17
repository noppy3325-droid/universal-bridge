using System.ComponentModel;
using System.Runtime.InteropServices;
using UniversalControl.Core;

namespace UniversalControl.Windows;

/// <summary>A message-only window on the input thread. Never changes global mouse settings.</summary>
internal sealed class RawMouseCapture : IDisposable
{
    private const uint Input = 0xFF, RidInput = 0x10000003;
    private readonly WindowProc _procedure;
    private readonly Action<double, double> _move;
    private readonly Action<string> _fault;
    private readonly RawMouseMotion _motion = new();
    private readonly string _className = "UniversalBridge.RawMouse." + Guid.NewGuid().ToString("N");
    private readonly nint _instance;
    private nint _window, _buffer;
    private uint _capacity = 256;
    private bool _registered, _inputRegistered, _disposed;
    public bool Available { get; private set; }
    public long Packets { get; private set; }

    public RawMouseCapture(Action<double, double> move, Action<string> fault)
    { _move = move; _fault = fault; _procedure = Dispatch; _instance = GetModuleHandle(null); }

    public void Start()
    {
        var windowClass = new WindowClass { Size = (uint)Marshal.SizeOf<WindowClass>(), Procedure = _procedure, Instance = _instance, ClassName = _className };
        if (RegisterClassEx(ref windowClass) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        _registered = true;
        _window = CreateWindowEx(0, _className, "", 0, 0, 0, 0, 0, new nint(-3), 0, _instance, 0);
        if (_window == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        _buffer = Marshal.AllocHGlobal((int)_capacity);
        if (!RegisterRawInputDevices([new RawDevice { Page = 1, Usage = 2, Flags = 0x100, Target = _window }], 1, (uint)Marshal.SizeOf<RawDevice>()))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        _inputRegistered = true; Available = true;
    }

    public void Reset() => _motion.Clear();
    private nint Dispatch(nint window, uint message, nuint wp, nint lp)
    {
        if (message == Input && Available)
        {
            try { Read(lp); }
            catch (Exception ex) { Available = false; _fault(L.T("マウスの相対入力を取得できません: ") + ex.Message); }
        }
        // Windows requires DefWindowProc for foreground WM_INPUT cleanup.
        return DefWindowProc(window, message, wp, lp);
    }
    private void Read(nint handle)
    {
        var headerSize = (uint)Marshal.SizeOf<RawHeader>(); uint required = 0;
        if (GetRawInputData(handle, RidInput, 0, ref required, headerSize) == uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (required < headerSize + 24 || required > 4096) return;
        if (required > _capacity) { _buffer = Marshal.ReAllocHGlobal(_buffer, (nint)required); _capacity = required; }
        var size = _capacity;
        var read = GetRawInputData(handle, RidInput, _buffer, ref size, headerSize);
        if (read == uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (read < headerSize + 24) return;
        var header = Marshal.PtrToStructure<RawHeader>(_buffer);
        if (header.Type != 0) return;
        var mouse = Marshal.PtrToStructure<RawMouse>(_buffer + (int)headerSize);
        Packets++;
        var absolute = (mouse.Flags & 1) != 0; var virtualDesktop = (mouse.Flags & 2) != 0;
        var (x, y) = _motion.Translate(header.Device, absolute, mouse.X, mouse.Y,
            absolute ? GetSystemMetrics(virtualDesktop ? 78 : 0) : 0, absolute ? GetSystemMetrics(virtualDesktop ? 79 : 1) : 0);
        if (x != 0 || y != 0) _move(x, y);
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        if (_inputRegistered) RegisterRawInputDevices([new RawDevice { Page = 1, Usage = 2, Flags = 1 }], 1, (uint)Marshal.SizeOf<RawDevice>());
        _inputRegistered = false;
        Available = false;
        if (_window != 0) { DestroyWindow(_window); _window = 0; }
        if (_registered) { UnregisterClass(_className, _instance); _registered = false; }
        if (_buffer != 0) { Marshal.FreeHGlobal(_buffer); _buffer = 0; }
    }
    private delegate nint WindowProc(nint window, uint message, nuint wp, nint lp);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass
    {
        public uint Size, Style; public WindowProc Procedure; public int ClassExtra, WindowExtra;
        public nint Instance, Icon, Cursor, Background; public string? MenuName; public string ClassName; public nint SmallIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct RawDevice { public ushort Page, Usage; public uint Flags; public nint Target; }
    [StructLayout(LayoutKind.Sequential)] private struct RawHeader { public uint Type, Size; public nint Device; public nuint WParam; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] private struct RawMouse
    { [FieldOffset(0)] public ushort Flags; [FieldOffset(12)] public int X; [FieldOffset(16)] public int Y; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WindowClass value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowEx(uint exStyle, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint DefWindowProc(nint window, uint message, nuint wp, nint lp);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool UnregisterClass(string name, nint instance);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterRawInputDevices([In] RawDevice[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputData(nint input, uint command, nint data, ref uint size, uint headerSize);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
}
