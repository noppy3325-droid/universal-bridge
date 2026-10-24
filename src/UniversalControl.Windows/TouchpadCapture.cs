using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Foundation.Metadata;
using Windows.UI.Core;
using Windows.UI.Input;
using UniversalControl.Core;

// Explicit opt-in to Microsoft's documented experimental APIs. IsSupported and API presence are checked at runtime.
#pragma warning disable CS8305

namespace UniversalControl.Windows;

internal interface ITouchpadCapture : IDisposable
{
    bool Available { get; }
    bool Active { get; }
    bool OwnsForeground { get; }
    long Events { get; }
    bool NativeContactsAvailable { get; }
    long NativeFrames { get; }
    int MaximumNativeContacts { get; }
    int MaximumValidNativeContacts { get; }
    bool Enter(int x, int y, AppSettings settings);
    void Leave();
}

/// <summary>Foreground ownership is required by Windows for global touchpad gestures.</summary>
[SupportedOSPlatform("windows10.0.26100.0")]
internal sealed class TouchpadCapture : ITouchpadCapture
{
    private readonly InputRouter _router;
    private readonly Action<string> _fault;
    private readonly WindowProc _procedure;
    private readonly TouchpadGestures _gestures = new();
    private readonly NativeTouchpadReader _native;
    private readonly string _className = "UniversalBridge.Touchpad." + Guid.NewGuid().ToString("N");
    private readonly nint _instance = GetModuleHandle(null);
    private TouchpadGesturesController? _controller;
    private PhysicalGestureRecognizer? _two, _multi;
    private nint _window, _previousForeground;
    private bool _classRegistered, _active, _disposed;
    private AppSettings _settings = new();
    public bool Available { get; private set; }
    public bool Active => _active;
    public bool OwnsForeground => _active && GetForegroundWindow() == _window;
    public long Events { get; private set; }
    public bool NativeContactsAvailable => Available && NativeTouchpadReader.Supported;
    public long NativeFrames => _native.Frames;
    public int MaximumNativeContacts => _native.MaximumContacts;
    public int MaximumValidNativeContacts => _native.MaximumValidContacts;

    public static bool Supported => ApiInformation.IsTypePresent("Windows.UI.Input.TouchpadGesturesController") &&
        ApiInformation.IsTypePresent("Windows.UI.Input.PhysicalGestureRecognizer") && TouchpadGesturesController.IsSupported();

    public TouchpadCapture(InputRouter router, Action<string> fault)
    { _router = router; _fault = fault; _procedure = Dispatch; _native = new(router); }
    public void Start()
    {
        if (!Supported) return;
        var windowClass = new WindowClass { Size = (uint)Marshal.SizeOf<WindowClass>(), Procedure = _procedure, Instance = _instance, ClassName = _className, Background = new nint(6) };
        if (RegisterClassEx(ref windowClass) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        _classRegistered = true;
        // A small visible ownership window, never a desktop-wide input lock.
        _window = CreateWindowEx(0x88, _className, L.T("iPad操作中"), 0x80C80000, 0, 0, 380, 100, 0, 0, _instance, 0);
        if (_window == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!RegisterTouchpadCapableWindow(_window, true)) throw new Win32Exception(Marshal.GetLastWin32Error());
        _two = new PhysicalGestureRecognizer { GestureSettings = GestureSettings.ManipulationTranslateX | GestureSettings.ManipulationTranslateY | GestureSettings.ManipulationScale,
            TranslationMinContactCount = 2, TranslationMaxContactCount = 2 };
        _two.ManipulationStarted += TwoStarted; _two.ManipulationUpdated += TwoUpdated; _two.ManipulationCompleted += TwoCompleted;
        _multi = new PhysicalGestureRecognizer { GestureSettings = GestureSettings.ManipulationTranslateX | GestureSettings.ManipulationTranslateY,
            TranslationMinContactCount = 3, TranslationMaxContactCount = 5 };
        _multi.ManipulationCompleted += MultiCompleted;
        _controller = TouchpadGesturesController.CreateForProcess();
        _controller.Enabled = false;
        _controller.SupportedGestures = TouchpadGlobalGestureKinds.ThreeFingerManipulations | TouchpadGlobalGestureKinds.FourFingerManipulations |
            TouchpadGlobalGestureKinds.FiveFingerManipulations | TouchpadGlobalGestureKinds.ThreeFingerActions |
            TouchpadGlobalGestureKinds.FourFingerActions | TouchpadGlobalGestureKinds.FiveFingerActions;
        _controller.PointerPressed += MultiPressed; _controller.PointerMoved += MultiMoved; _controller.PointerReleased += MultiReleased;
        _controller.GlobalActionPerformed += GlobalAction;
        Available = true;
    }
    public bool Enter(int x, int y, AppSettings settings)
    {
        if (!Available) return false;
        _settings = settings; _gestures.Reset(); _native.Reset(); _previousForeground = GetForegroundWindow();
        if (settings.NativeTouchpad && !NativeContactsAvailable) return false;
        SetWindowText(_window, L.T("iPad操作中 · 戻る: ") + settings.ReturnShortcut + L.T(" / Esc 3回"));
        _active = true; _controller!.Enabled = true;
        SetWindowPos(_window, new nint(-1), x - 190, y - 50, 380, 100, 0x40);
        SetForegroundWindow(_window); SetFocus(_window);
        if (GetForegroundWindow() == _window) return true;
        Leave(); return false;
    }
    public void Leave()
    {
        var restore = _active && GetForegroundWindow() == _window;
        _active = false;
        _native.Reset();
        if (_controller is not null) _controller.Enabled = false;
        _two?.CompleteGesture(); _multi?.CompleteGesture(); _gestures.Reset();
        if (_window != 0) ShowWindow(_window, 0);
        if (restore && _previousForeground != 0 && IsWindow(_previousForeground)) SetForegroundWindow(_previousForeground);
        _previousForeground = 0;
    }
    private void Safe(Action action)
    {
        try { if (_active && _router.IsRemote) { Events++; action(); } }
        catch (Exception ex) { _router.GoLocal(L.T("ジェスチャーの取得に失敗したためWindowsへ戻しました")); _fault(ex.Message); }
    }
    private void TwoStarted(PhysicalGestureRecognizer sender, ManipulationStartedEventArgs args)
    { _gestures.Reset(); _router.BeginTouchpadScroll(); }
    private void TwoUpdated(PhysicalGestureRecognizer sender, ManipulationUpdatedEventArgs args) => Safe(() =>
    {
        _router.ScrollTouchpad(args.Cumulative.Translation.X, args.Cumulative.Translation.Y);
        var zoom = _gestures.ZoomSteps(args.Delta.Scale);
        for (var index = 0; index < Math.Abs(zoom); index++) _router.Gesture(zoom > 0 ? GestureAction.ZoomIn : GestureAction.ZoomOut);
    });
    private void TwoCompleted(PhysicalGestureRecognizer sender, ManipulationCompletedEventArgs args) => Safe(() =>
    {
        _router.ScrollTouchpad(args.Cumulative.Translation.X, args.Cumulative.Translation.Y);
        _router.FlushMotion();
    });
    private void MultiCompleted(PhysicalGestureRecognizer sender, ManipulationCompletedEventArgs args) => Safe(() =>
        _router.Gesture(TouchpadGestures.Swipe(args.Cumulative.Translation.X, args.Cumulative.Translation.Y, _settings)));
    private void MultiPressed(TouchpadGesturesController sender, PointerEventArgs args) { args.Handled = true; Safe(() => { if (_settings.NativeTouchpad) _native.Process(args.CurrentPoint.PointerId); else _multi!.ProcessDownEvent(args.CurrentPoint); }); }
    private void MultiMoved(TouchpadGesturesController sender, PointerEventArgs args) { args.Handled = true; Safe(() => { if (_settings.NativeTouchpad) _native.Process(args.CurrentPoint.PointerId); else _multi!.ProcessMoveEvents(args.GetIntermediatePoints()); }); }
    private void MultiReleased(TouchpadGesturesController sender, PointerEventArgs args) { args.Handled = true; Safe(() => { if (_settings.NativeTouchpad) _native.Process(args.CurrentPoint.PointerId); else _multi!.ProcessUpEvent(args.CurrentPoint); }); }
    private void GlobalAction(TouchpadGesturesController sender, TouchpadGlobalActionEventArgs args) => Safe(() =>
    {
        if (!_settings.NativeTouchpad && args.Action is TouchpadGlobalAction.ThreeFingerTap or TouchpadGlobalAction.FourFingerTap or TouchpadGlobalAction.FiveFingerTap)
            _router.Gesture(GestureAction.Search);
    });
    private nint Dispatch(nint window, uint message, nuint wp, nint lp)
    {
        if (message == 0x10) { _router.GoLocal(L.T("ジェスチャー受け取り画面を閉じてWindowsへ戻しました")); return 0; }
        if (_active && message is 0x20A or 0x20E)
        {
            if (_settings.NativeTouchpad && _native.Active) return 0;
            var delta = unchecked((short)(wp >> 16));
            Safe(() => _router.Scroll(message == 0x20A ? delta : 0, message == 0x20E ? delta : 0));
            return 0;
        }
        if (message is 0x245 or 0x246 or 0x247 && _active)
        {
            var id = (uint)(wp & 0xFFFF);
            if (GetPointerType(id, out var type) && type == 5)
            {
                Safe(() =>
                {
                    if (_settings.NativeTouchpad) _native.Process(id);
                    else if (message == 0x246) _two!.ProcessDownEvent(PointerPoint.GetCurrentPoint(id));
                    else if (message == 0x247) _two!.ProcessUpEvent(PointerPoint.GetCurrentPoint(id));
                    else _two!.ProcessMoveEvents(PointerPoint.GetIntermediatePoints(id));
                });
                return 0; // Do not let DefWindowProc turn these contacts back into Windows wheel input.
            }
        }
        return DefWindowProc(window, message, wp, lp);
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        Leave(); Available = false;
        if (_controller is not null)
        {
            _controller.PointerPressed -= MultiPressed; _controller.PointerMoved -= MultiMoved; _controller.PointerReleased -= MultiReleased;
            _controller.GlobalActionPerformed -= GlobalAction; _controller = null;
        }
        if (_two is not null) { _two.ManipulationStarted -= TwoStarted; _two.ManipulationUpdated -= TwoUpdated; _two.ManipulationCompleted -= TwoCompleted; _two = null; }
        if (_multi is not null) { _multi.ManipulationCompleted -= MultiCompleted; _multi = null; }
        if (_window != 0) { RegisterTouchpadCapableWindow(_window, false); DestroyWindow(_window); _window = 0; }
        if (_classRegistered) { UnregisterClass(_className, _instance); _classRegistered = false; }
    }
    private delegate nint WindowProc(nint window, uint message, nuint wp, nint lp);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass
    {
        public uint Size, Style; public WindowProc Procedure; public int ClassExtra, WindowExtra;
        public nint Instance, Icon, Cursor, Background; public string? MenuName; public string ClassName; public nint SmallIcon;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WindowClass value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowEx(uint exStyle, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint DefWindowProc(nint window, uint message, nuint wp, nint lp);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool UnregisterClass(string name, nint instance);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterTouchpadCapableWindow(nint window, bool enable);
    [DllImport("user32.dll")] private static extern bool GetPointerType(uint id, out uint type);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern nint SetFocus(nint window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetWindowText(nint window, string value);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
}
