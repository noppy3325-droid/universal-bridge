using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Concurrent;
using UniversalControl.Core;

namespace UniversalControl.Windows;

public sealed record PhysicalReadinessProbe(bool PhysicalIdle, bool AllVirtualKeysIdle, int ImeModeStatesActive, bool OnDefaultDesktop, int KeyboardType);
public sealed record EdgeHandoffStatus(long Attempts, long Completed, EdgeCancellation LastCancellation);
public sealed record EdgeInputProbe(bool Success, bool ArrivalBandValidated, bool ReadinessCancellationValidated, long Attempts,
    bool PhysicalCursorMoved, bool InputReadinessScripted = true, bool CursorScripted = true, bool InputCaptured = false, bool BluetoothAdvertised = false);

public sealed class NativeInput : IDisposable
{
    private const uint Quit = 0x12, TimerMessage = 0x113, Hotkey = 0x312, SyncMode = 0x8001, Command = 0x8002;
    private readonly InputRouter _router;
    private readonly EdgeDetector _edge = new();
    private readonly HookProc _keyboardProc, _mouseProc;
    private TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Thread? _thread;
    private System.Threading.Timer? _watchdog;
    private RawMouseCapture? _rawMouse;
    private MouseReportTimer? _motionTimer;
    private ITouchpadCapture? _touchpad;
    private readonly ConcurrentQueue<(Func<string?> Action, TaskCompletionSource<string?> Result)> _commands = new();
    private uint _threadId;
    private nint _keyboardHook, _mouseHook;
    private volatile bool _running, _nativeRemote;
    private long _lastPulse, _lastFlush, _lastReconcile, _rawWaitingSince, _lastRawPackets;
    private long _motionTimerTicks;
    private volatile bool _rawFallback;
    private nuint _timerId;
    private Point _anchor, _returnPoint;
    private sealed record EdgeRequest(EdgeActivationLease Lease);
    private volatile EdgeRequest? _edgeRequest;
    private int _edgeRevision, _observedEdgeRevision = -1, _activationPending;
    private int _edgeMotionPending, _lastEdgeCancellation;
    private long _edgeAttempts, _edgeCompleted;
    private volatile bool _edgeProbeIdle;
    private sealed record ProbeCursor(PixelPoint Position);
    private volatile ProbeCursor? _edgeProbeCursor;
    private bool EdgeInputIdle => _edgeProbeIdle || (_router.PhysicalIdle && PhysicalKeyboardIdle());
    private AppSettings _settings = new();
    private IReadOnlyList<DisplayInfo> _displays = [];
    public bool Available => _running && ReturnHotkeyRegistered && _keyboardHook != 0 && _mouseHook != 0 && Environment.TickCount64 - Interlocked.Read(ref _lastPulse) < 1500;
    private int _reportIntervalMs = 8;
    public int ReportIntervalMs { get => Volatile.Read(ref _reportIntervalMs); set => Volatile.Write(ref _reportIntervalMs, Math.Clamp(value, 8, 100)); }
    public bool PreciseMotionTiming => _motionTimer is not null;
    public async Task<long> ProbeMotionTimingAsync()
    {
        var before = Interlocked.Read(ref _motionTimerTicks);
        await InvokeAsync(() =>
        {
            if (_router.IsRemote) throw new InvalidOperationException("Motion timing probe requires local input.");
            _motionTimer?.Start(8); return null;
        });
        try { await Task.Delay(200); }
        finally { await InvokeAsync(() => { if (!_router.IsRemote) _motionTimer?.Stop(); return null; }); }
        return Interlocked.Read(ref _motionTimerTicks) - before;
    }
    public bool ActivationHotkeyRegistered { get; private set; }
    public bool ReturnHotkeyRegistered { get; private set; }
    public bool TouchpadAvailable => _touchpad?.Available == true;
    public bool TouchpadActive => _touchpad?.Active == true;
    public long TouchpadEvents => _touchpad?.Events ?? 0;
    public string TouchpadStatus => TouchpadAvailable ? L.T("対応：iPad操作中に専用の受け取り画面で取得") : L.T("このWindowsではジェスチャー取得APIを使用できません");
    public static bool IsTouchpadSupported() => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100) && TouchpadCapture.Supported;
    public static Task<bool?> ProbeTouchpadReceiverAsync(InputRouter router) => Task.Run<bool?>(() =>
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100) || !TouchpadCapture.Supported) return null;
        var foreground = GetForegroundWindow();
        using var receiver = new TouchpadCapture(router, _ => { }); receiver.Start();
        return receiver.Available && receiver.HiddenReceiverVerified && !receiver.Active && GetForegroundWindow() == foreground;
    });
    public bool RawMouseAvailable => _rawMouse?.Available == true;
    public long RawMousePackets => _rawMouse?.Packets ?? 0;
    private bool UsesRawMouse => _settings.RawMouse && RawMouseAvailable && !_rawFallback;
    public string MouseMode => UsesRawMouse ? L.T("相対入力（Windowsの加速なし）") : _settings.RawMouse ? L.T("互換入力（相対入力を使用できないため）") : L.T("互換入力（Windowsのカーソル座標）");
    public event Func<bool, Task>? ActivationRequested;
    public Func<bool>? EdgeActivationAllowed { get; set; }
    public EdgeHandoffStatus EdgeStatus => new(Interlocked.Read(ref _edgeAttempts), Interlocked.Read(ref _edgeCompleted), (EdgeCancellation)Volatile.Read(ref _lastEdgeCancellation));
    public async Task<EdgeInputProbe> ProbeEdgeHandoffAsync()
    {
        if (_router.IsRemote || ActivationRequested is not null) throw new InvalidOperationException("Edge probe requires an unused, local-only input instance.");
        var oldSettings = _settings; var oldDisplays = _displays; var oldAllowed = EdgeActivationAllowed;
        var origin = ReadPhysicalCursor() ?? throw new InvalidOperationException("Physical cursor unavailable.");
        var ready = true; var scenario = 0; var before = EdgeStatus.Attempts;
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Handle(bool fromEdge)
        {
            var valid = fromEdge && EdgeActivationCurrent;
            if (scenario == 0) { await Task.Delay(40); valid &= EdgeActivationCurrent; }
            else
            {
                await InvokeAsync(() => { ready = false; CheckEdge(false); ready = true; return null; });
                valid &= !EdgeActivationCurrent && EdgeStatus.LastCancellation == EdgeCancellation.NotReady;
            }
            completed.TrySetResult(valid);
        }
        async Task Prepare()
        {
            var deadline = Environment.TickCount64 + 1000;
            while (Volatile.Read(ref _activationPending) != 0)
            { if (Environment.TickCount64 > deadline) throw new TimeoutException("Previous edge probe is still running."); await Task.Delay(5); }
            await InvokeAsync(() =>
            {
                // The physical pointer stays untouched. Only the test display rectangle is virtual.
                var display = new DisplayInfo("edge-probe", origin.X - 1917, origin.Y - 500, 1920, 1080);
                Configure(oldSettings with { AutoSwitch = true, CaptureTouchpad = false, Edge = ScreenEdge.Right,
                    MonitorId = display.Id, PortalStart = 0, PortalEnd = 1, EdgeDwellMs = 150 }, [display]);
                _observedEdgeRevision = Volatile.Read(ref _edgeRevision); _edge.Reset(false);
                Interlocked.Exchange(ref _edgeMotionPending, 1); return null;
            });
        }
        ActivationRequested += Handle; EdgeActivationAllowed = () => ready; _edgeProbeIdle = true; _edgeProbeCursor = new(origin);
        try
        {
            await Prepare(); var first = await completed.Task.WaitAsync(TimeSpan.FromSeconds(3));
            scenario = 1; completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await Prepare(); var second = await completed.Task.WaitAsync(TimeSpan.FromSeconds(3));
            return new(first && second, first, second, EdgeStatus.Attempts - before, GetPhysicalCursorPos(out var actual) && new PixelPoint(actual.X, actual.Y) != origin);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"Edge probe timed out: scenario={scenario}, attempts={EdgeStatus.Attempts - before}, cancellation={EdgeStatus.LastCancellation}, physicalIdle={PhysicalKeyboardIdle()}, trackedIdle={_router.PhysicalIdle}, cursorSame={ReadPhysicalCursor() == origin}, desktop={OnDefaultDesktop()}, available={Available}.", ex);
        }
        finally
        {
            ActivationRequested -= Handle; EdgeActivationAllowed = oldAllowed; _edgeProbeIdle = false; _edgeProbeCursor = null;
            await InvokeAsync(() => { Configure(oldSettings, oldDisplays); CancelEdge(EdgeCancellation.SettingsChanged); return null; });
        }
    }
    public bool EdgeActivationCurrent
    {
        get
        {
            if (_edgeRequest is not { } request) return false;
            var valid = request.Lease.Validate(ReadPhysicalCursor(), Volatile.Read(ref _edgeRevision), _settings.AutoSwitch && EdgeActivationAllowed?.Invoke() == true,
                EdgeInputIdle, Environment.TickCount64);
            if (!valid) Volatile.Write(ref _lastEdgeCancellation, (int)request.Lease.Cancellation);
            return valid;
        }
    }
    private PixelPoint? ReadPhysicalCursor() => _edgeProbeCursor?.Position ?? (GetPhysicalCursorPos(out var cursor) ? new PixelPoint(cursor.X, cursor.Y) : null);
    private void CancelEdge(EdgeCancellation reason) { _edge.Reset(); _edgeRequest?.Lease.Cancel(reason); }
    public event Action<string>? Faulted;

    public NativeInput(InputRouter router)
    {
        _router = router; _keyboardProc = KeyboardHook; _mouseProc = MouseHook;
        _router.Changed += RouterChanged;
    }
    public void Configure(AppSettings settings, IReadOnlyList<DisplayInfo> displays)
    { if (_settings.RawMouse != settings.RawMouse) _rawFallback = false; _settings = settings; _displays = displays; Interlocked.Increment(ref _edgeRevision); }
    public Task<string?> UpdateShortcutsAsync(ShortcutBinding activate, ShortcutBinding back) => InvokeAsync(() => RebindShortcuts(activate, back));
    public async Task<bool> ConfirmModeAsync()
    { await InvokeAsync(() => { SynchronizeMode(); return null; }); return _router.IsRemote && _nativeRemote; }
    private Task<string?> InvokeAsync(Func<string?> action)
    {
        if (!_running || _threadId == 0) return Task.FromException<string?>(new InvalidOperationException(L.T("入力監視が開始されていません。")));
        var result = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _commands.Enqueue((action, result));
        if (!PostThreadMessage(_threadId, Command, 0, 0)) result.TrySetException(new Win32Exception(Marshal.GetLastWin32Error()));
        return result.Task;
    }
    private string? RebindShortcuts(ShortcutBinding activate, ShortcutBinding back)
    {
        ShortcutBinding.ValidatePair(activate, back);
        var old = _settings;
        UnregisterHotKey(0, 1); UnregisterHotKey(0, 2);
        ReturnHotkeyRegistered = RegisterHotKey(0, 1, 0x4000 | (uint)back.Modifiers, (uint)back.Key);
        ActivationHotkeyRegistered = RegisterHotKey(0, 2, 0x4000 | (uint)activate.Modifiers, (uint)activate.Key);
        if (ReturnHotkeyRegistered && ActivationHotkeyRegistered)
        { _settings = old with { ActivateShortcut = activate, ReturnShortcut = back }; return null; }
        UnregisterHotKey(0, 1); UnregisterHotKey(0, 2);
        ReturnHotkeyRegistered = RegisterHotKey(0, 1, 0x4000 | (uint)old.ReturnShortcut.Modifiers, (uint)old.ReturnShortcut.Key);
        ActivationHotkeyRegistered = RegisterHotKey(0, 2, 0x4000 | (uint)old.ActivateShortcut.Modifiers, (uint)old.ActivateShortcut.Key);
        if (!ReturnHotkeyRegistered) _router.GoLocal(L.T("復帰ショートカットの再登録に失敗しました。アプリを再起動してください。"));
        return L.T("指定したショートカットは他のアプリで使用中です。元の設定を維持しました。");
    }
    public async Task StartAsync()
    {
        if (_thread?.IsAlive == true) { await _started.Task.WaitAsync(TimeSpan.FromSeconds(5)); return; }
        _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _watchdog?.Dispose();
        _thread = new Thread(Run) { IsBackground = true, Name = "Universal Bridge input" };
        _thread.SetApartmentState(ApartmentState.MTA); _thread.Start();
        await _started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _watchdog = new System.Threading.Timer(_ =>
        {
            try
            {
                _router.CheckConnection();
                if (_router.IsRemote && (!Available || !OnDefaultDesktop())) _router.GoLocal(L.T("入力監視またはデスクトップの変更によりWindowsへ戻しました"));
                if (_router.IsRemote && _settings.CaptureTouchpad && _touchpad?.Active == true && !_touchpad.OwnsForeground)
                    _router.GoLocal(L.T("Windows側へフォーカスが移ったため入力を戻しました"));
            }
            catch (Exception) { _router.GoLocal(L.T("入力監視の安全確認に失敗したためWindowsへ戻しました")); }
        }, null, 250, 250);
    }
    public static bool PhysicalKeyboardIdle() => PhysicalInputState.IsIdle(IsPhysicalKeyDown);
    private static bool IsPhysicalKeyDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    public void ReconcilePhysical() => _router.ReconcilePhysical(IsPhysicalKeyDown);
    public static PhysicalReadinessProbe ProbePhysicalReadiness()
    {
        var allIdle = true; var imeModes = 0;
        for (var key = 1; key < 256; key++)
        {
            if (!IsPhysicalKeyDown(key)) continue;
            allIdle = false;
            if (key is >= 0xF0 and <= 0xF5) imeModes++;
        }
        return new(PhysicalKeyboardIdle(), allIdle, imeModes, OnDefaultDesktop(), GetKeyboardType(0));
    }
    public static bool OnDefaultDesktop()
    {
        var desktop = OpenInputDesktop(0, false, 1); if (desktop == 0) return false;
        try
        {
            var name = new StringBuilder(256);
            return GetUserObjectInformation(desktop, 2, name, name.Capacity * 2, out _) && name.ToString().Equals("Default", StringComparison.OrdinalIgnoreCase);
        }
        finally { CloseDesktop(desktop); }
    }
    private DisplayInfo? SelectedDisplay => _displays.FirstOrDefault(d => d.Id == _settings.MonitorId) ?? _displays.FirstOrDefault();
    private void RouterChanged() { if (_threadId != 0) PostThreadMessage(_threadId, SyncMode, 0, 0); }
    private void SynchronizeMode()
    {
        if (_router.IsRemote && !_nativeRemote)
        {
            GetCursorPos(out _returnPoint);
            var display = SelectedDisplay;
            if (display is null) { _router.GoLocal(L.T("ディスプレイを取得できません")); return; }
            _router.SetEntryAlong(VirtualCursor.EntryAlong(new(_returnPoint.X, _returnPoint.Y), display, _settings));
            _anchor = new Point { X = display.Left + display.Width / 2, Y = display.Top + display.Height / 2 };
            _rawMouse?.Reset(); _rawWaitingSince = 0; _lastRawPackets = RawMousePackets;
            _nativeRemote = true;
            _motionTimer?.Start(ReportIntervalMs);
            if (!SetCursorPos(_anchor.X, _anchor.Y)) _router.GoLocal(L.T("カーソル制御に失敗したためWindowsへ戻しました"));
            if (_router.IsRemote && _settings.CaptureTouchpad && _touchpad?.Enter(_anchor.X, _anchor.Y, _settings) != true)
                _router.GoLocal(L.T("ジェスチャーの受け取り画面を有効にできません。アプリを前面に出して再試行するか、入力設定でジェスチャー取得をオフにしてください。"));
            if (!_router.IsRemote) SynchronizeMode();
        }
        else if (!_router.IsRemote && _nativeRemote)
        {
            _nativeRemote = false; _touchpad?.Leave(); _rawMouse?.Reset(); _edge.Reset();
            _motionTimer?.Stop();
            var display = SelectedDisplay;
            if (display is not null)
            {
                var point = _router.LastReturnAtEdge ? _router.Cursor.WindowsReturnPoint(display, _settings) : new PixelPoint(
                    Math.Clamp(_returnPoint.X, display.Left + 32, display.Right - 33), Math.Clamp(_returnPoint.Y, display.Top + 32, display.Bottom - 33));
                SetCursorPos(point.X, point.Y);
            }
        }
    }
    private void Run()
    {
        _threadId = GetCurrentThreadId();
        PeekMessage(out _, 0, 0, 0, 0); // Create the message queue before exposing this thread.
        try
        {
            _keyboardHook = SetWindowsHookEx(13, _keyboardProc, GetModuleHandle(null), 0);
            _mouseHook = SetWindowsHookEx(14, _mouseProc, GetModuleHandle(null), 0);
            if (_keyboardHook == 0 || _mouseHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), L.T("入力フックを開始できません。"));
            ReturnHotkeyRegistered = RegisterHotKey(0, 1, 0x4000 | (uint)_settings.ReturnShortcut.Modifiers, (uint)_settings.ReturnShortcut.Key);
            if (!ReturnHotkeyRegistered) throw new Win32Exception(Marshal.GetLastWin32Error(), _settings.ReturnShortcut + L.T(" は別のアプリで使用中です。入力設定で復帰ショートカットを変更してください。"));
            ActivationHotkeyRegistered = RegisterHotKey(0, 2, 0x4000 | (uint)_settings.ActivateShortcut.Modifiers, (uint)_settings.ActivateShortcut.Key);
            if (!ActivationHotkeyRegistered) ThreadPool.QueueUserWorkItem(_ => Faulted?.Invoke(_settings.ActivateShortcut + L.T(" は別のアプリで使用中です。入力設定で変更してください。")));
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
            {
                var touchpad = new TouchpadCapture(_router, text => ThreadPool.QueueUserWorkItem(_ => Faulted?.Invoke(text)));
                try { touchpad.Start(); _touchpad = touchpad; }
                catch (Exception ex) { touchpad.Dispose(); ThreadPool.QueueUserWorkItem(_ => Faulted?.Invoke(L.T("ジェスチャー取得を開始できません: ") + ex.Message)); }
            }
            _rawFallback = false;
            _rawMouse = new RawMouseCapture((x, y) =>
            {
                SynchronizeMode();
                if (_router.IsRemote && UsesRawMouse) _router.Move(x, y);
                else if (!_router.IsRemote) Interlocked.Exchange(ref _edgeMotionPending, 1);
            }, text => { _rawFallback = true; ThreadPool.QueueUserWorkItem(_ => Faulted?.Invoke(text + L.T(" 互換入力へ切り替えました。"))); });
            try { _rawMouse.Start(); }
            catch (Exception ex)
            {
                _rawMouse.Dispose(); _rawMouse = null;
                ThreadPool.QueueUserWorkItem(_ => Faulted?.Invoke(L.T("マウスの相対入力を開始できないため互換入力を使います: ") + ex.Message));
            }
            _timerId = SetTimer(0, 1, 10, 0);
            if (_timerId == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), L.T("入力監視タイマーを開始できません。"));
            try { _motionTimer = new MouseReportTimer(); }
            catch (Exception ex) { ThreadPool.QueueUserWorkItem(_ => Faulted?.Invoke(L.T("高精度マウスタイマーを使用できないため標準タイマーを使います: ") + ex.Message)); }
            Interlocked.Exchange(ref _lastPulse, Environment.TickCount64); _running = true; _started.TrySetResult();
            int result;
            while ((result = NextMessage(out var message)) > 0)
            {
                Interlocked.Exchange(ref _lastPulse, Environment.TickCount64);
                if (message.Message == TimerMessage)
                {
                    SynchronizeMode(); var now = Environment.TickCount64;
                    CheckEdge(physicalMotion: Interlocked.Exchange(ref _edgeMotionPending, 0) != 0);
                    if (_motionTimer is null && now >= _lastFlush)
                    {
                        _router.FlushMotion();
                        // Keep a deadline instead of starting a second full interval after a late tick.
                        _lastFlush = _lastFlush == 0 || now - _lastFlush >= ReportIntervalMs ? now + ReportIntervalMs : _lastFlush + ReportIntervalMs;
                    }
                    else if (_router.IsRemote) _motionTimer?.Start(ReportIntervalMs);
                    if (!_router.IsRemote && now - _lastReconcile >= 250)
                    { _router.ReconcilePhysical(vk => (GetAsyncKeyState(vk) & 0x8000) != 0); _lastReconcile = now; }
                }
                else if (message.Message == SyncMode) SynchronizeMode();
                else if (message.Message == Command)
                {
                    while (_commands.TryDequeue(out var command))
                    { if (command.Result.Task.IsCompleted) continue; try { command.Result.TrySetResult(command.Action()); } catch (Exception ex) { command.Result.TrySetException(ex); } }
                }
                else if (message.Message == Hotkey)
                {
                    var binding = message.WParam == 1 ? _settings.ReturnShortcut : _settings.ActivateShortcut;
                    var key = (int)((long)message.LParam >> 16) & 0xFFFF; var modifiers = (int)message.LParam & 7;
                    if (key != binding.Key || modifiers != (int)binding.Modifiers) continue;
                    if (message.WParam == 1) _router.GoLocal(L.T("ショートカットでWindowsへ戻しました"));
                    else if (message.WParam == 2 && !_router.IsRemote) RequestActivation();
                }
                else { TranslateMessage(ref message); DispatchMessage(ref message); }
            }
            if (result == -1) throw new Win32Exception(Marshal.GetLastWin32Error(), L.T("入力監視ループが終了しました。"));
        }
        catch (Exception ex) { _started.TrySetException(ex); NotifyFault(ex.Message); }
        finally
        {
            _running = false; _router.GoLocal(L.T("入力監視を終了しました")); SynchronizeMode();
            if (_keyboardHook != 0) UnhookWindowsHookEx(_keyboardHook);
            if (_mouseHook != 0) UnhookWindowsHookEx(_mouseHook);
            _keyboardHook = _mouseHook = 0;
            _rawMouse?.Dispose(); _rawMouse = null;
            _motionTimer?.Dispose(); _motionTimer = null;
            _touchpad?.Dispose(); _touchpad = null;
            UnregisterHotKey(0, 1); UnregisterHotKey(0, 2); ActivationHotkeyRegistered = ReturnHotkeyRegistered = false;
            while (_commands.TryDequeue(out var command)) command.Result.TrySetException(new InvalidOperationException(L.T("入力監視を終了しました。")));
            if (_timerId != 0) KillTimer(0, _timerId); _timerId = 0; _threadId = 0;
        }
    }
    private int NextMessage(out MessageData message)
    {
        if (_motionTimer is null) return GetMessage(out message, 0, 0, 0);
        while (true)
        {
            var handle = _motionTimer.Handle;
            var result = MsgWaitForMultipleObjectsEx(1, in handle, uint.MaxValue, 0x1CFF, 4); // Includes touch and pointer input.
            if (result == uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (result == 0)
            {
                Interlocked.Increment(ref _motionTimerTicks);
                Interlocked.Exchange(ref _lastPulse, Environment.TickCount64);
                SynchronizeMode();
                if (_router.IsRemote) { _motionTimer.Start(ReportIntervalMs); _router.FlushMotion(); }
                // Service queued input as well: a slow flush must not starve emergency hotkeys.
            }
            if (PeekMessage(out message, 0, 0, 0, 1)) return message.Message == Quit ? 0 : 1;
        }
    }
    private void RequestActivation(EdgeRequest? request = null)
    {
        if (Interlocked.CompareExchange(ref _activationPending, 1, 0) != 0) return;
        _edgeRequest = request;
        if (request is not null) { Interlocked.Increment(ref _edgeAttempts); Volatile.Write(ref _lastEdgeCancellation, 0); }
        _ = Task.Run(async () =>
        {
            try { if (ActivationRequested is { } activate) await activate(request is not null); }
            catch (Exception ex) { NotifyFault(ex.Message); }
            finally
            {
                if (request is not null)
                {
                    if (_router.IsRemote) { Interlocked.Increment(ref _edgeCompleted); Volatile.Write(ref _lastEdgeCancellation, 0); }
                    else Volatile.Write(ref _lastEdgeCancellation, (int)request.Lease.Cancellation);
                }
                _edgeRequest = null; Volatile.Write(ref _activationPending, 0);
            }
        });
    }
    private void CheckEdge(bool physicalMotion)
    {
        var revision = Volatile.Read(ref _edgeRevision);
        if (_observedEdgeRevision != revision) { CancelEdge(EdgeCancellation.SettingsChanged); _observedEdgeRevision = revision; }
        if (_router.IsRemote) { _edge.Reset(); return; } // A committed handoff must not cancel its own request.
        if (!_settings.AutoSwitch || EdgeActivationAllowed?.Invoke() != true || SelectedDisplay is not { } display)
        { CancelEdge(EdgeCancellation.NotReady); return; }
        if (!EdgeInputIdle) { CancelEdge(EdgeCancellation.InputHeld); return; }
        if (ReadPhysicalCursor() is not { } point) { CancelEdge(EdgeCancellation.CursorUnavailable); return; }
        var settings = _settings; var displays = _displays;
        if (_edgeRequest is { } request)
        { request.Lease.Validate(point, revision, true, true, Environment.TickCount64); return; }
        var ready = physicalMotion ? _edge.Observe(point, display, displays, settings, Environment.TickCount64)
            : _edge.Poll(point, display, displays, settings, Environment.TickCount64);
        if (ready && EdgeInputIdle && OnDefaultDesktop()) RequestActivation(new(new(display, displays, settings, revision, Environment.TickCount64)));
    }
    private void NotifyFault(string text)
    { _router.GoLocal(L.T("入力監視の異常によりWindowsへ戻しました")); ThreadPool.QueueUserWorkItem(_ => Faulted?.Invoke(text)); }
    private nint KeyboardHook(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            try
            {
                var key = Marshal.PtrToStructure<KeyboardData>(lParam);
                // Real physical events only; leave injected input to Windows and other accessibility tools.
                if ((key.Flags & 0x10) == 0)
                {
                    var down = wParam == 0x100 || wParam == 0x104;
                    if (down && !_router.IsRemote && !_edgeProbeIdle) CancelEdge(EdgeCancellation.InputHeld);
                    if (_router.Key((int)key.VirtualKey, key.ScanCode, (key.Flags & 1) != 0, down, Environment.TickCount64,
                        japaneseKeyboard: key.ScanCode == 0x29 && GetKeyboardType(0) == 7)) return 1;
                }
            }
            catch (Exception ex) { NotifyFault(ex.Message); }
        }
        return CallNextHookEx(0, code, wParam, lParam);
    }
    private nint MouseHook(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            try
            {
                var mouse = Marshal.PtrToStructure<MouseData>(lParam);
                // Touch/touchpad promotion is OS input; ordinary automation remains local.
                if ((mouse.Flags & 1) != 0 && !(_router.IsRemote && (mouse.Extra & 0xFFFFFF00u) == 0xFF515700u))
                    return CallNextHookEx(0, code, wParam, lParam);
                SynchronizeMode(); var message = (int)wParam;
                var (button, down) = message switch
                {
                    0x201 => (MouseButtons.Left, true), 0x202 => (MouseButtons.Left, false),
                    0x204 => (MouseButtons.Right, true), 0x205 => (MouseButtons.Right, false),
                    0x207 => (MouseButtons.Middle, true), 0x208 => (MouseButtons.Middle, false),
                    _ => (MouseButtons.None, false)
                };
                if (button != 0)
                {
                    if (!_router.IsRemote && !_edgeProbeIdle) CancelEdge(EdgeCancellation.InputHeld);
                    return _router.Button(button, down) ? 1 : CallNextHookEx(0, code, wParam, lParam);
                }
                if (!_router.IsRemote && !_edgeProbeIdle && message is 0x20A or 0x20E) CancelEdge(EdgeCancellation.InputHeld);
                if (_router.IsRemote)
                {
                    if (message == 0x200)
                    {
                        var now = Environment.TickCount64;
                        if (UsesRawMouse)
                        {
                            var packets = RawMousePackets;
                            if (packets != _lastRawPackets) { _lastRawPackets = packets; _rawWaitingSince = 0; }
                            else if (_rawWaitingSince == 0) _rawWaitingSince = now;
                            else if (now - _rawWaitingSince > 500)
                            {
                                _rawFallback = true;
                                ThreadPool.QueueUserWorkItem(_ => Faulted?.Invoke(L.T("マウスの相対入力を受信できないため互換入力へ切り替えました。")));
                            }
                        }
                        if (!UsesRawMouse)
                        {
                            var dx = mouse.Position.X - _anchor.X; var dy = mouse.Position.Y - _anchor.Y;
                            if (dx != 0 || dy != 0)
                            {
                                _router.Move(dx, dy);
                                if (!SetCursorPos(_anchor.X, _anchor.Y)) _router.GoLocal(L.T("カーソル制御に失敗しました"));
                            }
                        }
                    }
                    else if (message == 0x20A) _router.Scroll(unchecked((short)(mouse.Data >> 16)));
                    else if (message == 0x20E) _router.Scroll(0, unchecked((short)(mouse.Data >> 16)));
                    return 1;
                }
                // A low-level hook runs before Windows finishes the cursor update. Sample its
                // physical, clipped position on the input timer rather than validating stale coordinates.
                if (message == 0x200) Interlocked.Exchange(ref _edgeMotionPending, 1);
            }
            catch (Exception ex) { NotifyFault(ex.Message); }
        }
        return CallNextHookEx(0, code, wParam, lParam);
    }
    public void Dispose()
    {
        _router.GoLocal(L.T("アプリを終了します")); _watchdog?.Dispose();
        if (_threadId != 0) PostThreadMessage(_threadId, Quit, 0, 0);
        if (_thread is not null && Thread.CurrentThread != _thread) _thread.Join(TimeSpan.FromSeconds(3));
        _router.Changed -= RouterChanged;
    }

    private delegate nint HookProc(int code, nint wParam, nint lParam);
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardData { public uint VirtualKey, ScanCode, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseData { public Point Position; public uint Data, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MessageData { public nint HWnd; public uint Message; public nuint WParam; public nint LParam; public uint Time; public Point Position; public uint Private; }
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int id, HookProc proc, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint wp, nint lp);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? module);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)] private static extern int GetMessage(out MessageData message, nint window, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out MessageData message, nint window, uint min, uint max, uint remove);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint MsgWaitForMultipleObjectsEx(uint count, in nint handles, uint milliseconds, uint wakeMask, uint flags);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MessageData message);
    [DllImport("user32.dll")] private static extern nint DispatchMessage(ref MessageData message);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint message, nuint wp, nint lp);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint window, int id, uint mods, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")] private static extern nuint SetTimer(nint window, nuint id, uint interval, nint callback);
    [DllImport("user32.dll")] private static extern bool KillTimer(nint window, nuint id);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetPhysicalCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern int GetKeyboardType(int typeFlag);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] private static extern bool CloseDesktop(nint desktop);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetUserObjectInformation(nint handle, int index, StringBuilder value, int length, out int needed);
}
