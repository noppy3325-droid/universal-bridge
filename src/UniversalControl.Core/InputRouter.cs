namespace UniversalControl.Core;

public enum ActivationStatus { Activated, AlreadyActivating, SenderStopped, Releasing, Disconnected, CaptureUnavailable, InputHeld, Cancelled }
public sealed record InputHoldState(int TrackedKeys, int ReleasePendingKeys, bool MouseHeld, bool MouseReleasePending);

/// <summary>All suppression decisions are synchronous and independent of GUI/Bluetooth awaits.</summary>
public sealed class InputRouter
{
    private readonly object _gate = new();
    private readonly IHidTransport _transport;
    private readonly ReportDispatcher _dispatcher;
    private readonly Dictionary<long, int> _physical = [];
    private readonly Dictionary<long, (byte Modifier, byte Usage)> _remoteKeys = [];
    private readonly HashSet<long> _quarantine = [];
    private readonly MotionAccumulator _motion = new();
    private readonly PointerMotion _pointer = new();
    private readonly MotionAccumulator _horizontalScroll = new();
    private readonly TouchpadScroll _touchpadScroll = new();
    private readonly MotionAccumulator _touchpadWheel = new();
    private readonly TimeProvider _clock;
    private long _lastPointerReport;
    private bool _nativeContactsActive;
    private byte[]? _lastNativeFrame;
    private readonly SemaphoreSlim _activation = new(1);
    private MouseButtons _physicalButtons, _remoteButtons, _quarantineButtons;
    private string? _target;
    private long _epoch, _lastEscape;
    private int _escapes;
    private volatile bool _remote;
    private AppSettings _settings = new();
    public bool IsRemote => _remote;
    public long Generation => Interlocked.Read(ref _epoch);
    public bool PhysicalIdle { get { lock (_gate) return _physical.Count == 0 && _physicalButtons == 0 && _quarantine.Count == 0 && _quarantineButtons == 0; } }
    public InputHoldState HeldState { get { lock (_gate) return new(_physical.Count, _quarantine.Count, _physicalButtons != 0, _quarantineButtons != 0); } }
    public VirtualCursor Cursor { get; } = new();
    public string Reason { get; private set; } = L.T("Windowsで操作中");
    public event Action? Changed;

    public InputRouter(IHidTransport transport, ReportDispatcher dispatcher, TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        _transport = transport; _dispatcher = dispatcher;
        _dispatcher.Failed += GoLocal;
    }
    public void Configure(AppSettings settings) { lock (_gate) _settings = settings.Normalize(); }

    public async Task<bool> ActivateAsync(string target, Func<bool> captureAvailable)
        => await TryActivateAsync(target, captureAvailable) == ActivationStatus.Activated;

    public async Task<ActivationStatus> TryActivateAsync(string target, Func<bool> captureAvailable)
    {
        if (!await _activation.WaitAsync(0)) return ActivationStatus.AlreadyActivating;
        var requestedEpoch = Interlocked.Read(ref _epoch);
        try
        {
            var status = CheckActivation(target, captureAvailable, checkSender: false);
            if (status != ActivationStatus.Activated) return status;
            var neutralized = !_dispatcher.Healthy ? await _dispatcher.TryRecoverAsync(target) : await _dispatcher.NeutralizeAsync(target);
            lock (_gate)
            {
                if (_epoch != requestedEpoch) return ActivationStatus.Cancelled;
                if (!neutralized) return _dispatcher.Healthy ? ActivationStatus.Disconnected : ActivationStatus.SenderStopped;
                status = CheckActivation(target, captureAvailable);
                if (status != ActivationStatus.Activated) return status;
                _target = target; _epoch++; _dispatcher.SetEpoch(_epoch);
                _remote = true; _motion.Clear(); _pointer.Clear(); _horizontalScroll.Clear(); _touchpadScroll.Reset(); _touchpadWheel.Clear(); _nativeContactsActive = false;
                _lastNativeFrame = null; _lastPointerReport = _clock.GetTimestamp(); _escapes = 0; Cursor.Enter(_settings); Reason = L.T("iPadへ入力を送信中");
            }
            Changed?.Invoke(); return ActivationStatus.Activated;
        }
        finally { _activation.Release(); }
    }

    private ActivationStatus CheckActivation(string target, Func<bool> captureAvailable, bool checkSender = true)
    {
        if (checkSender && !_dispatcher.Healthy) return ActivationStatus.SenderStopped;
        if (_dispatcher.Settling) return ActivationStatus.Releasing;
        if (!_transport.IsReady(target)) return ActivationStatus.Disconnected;
        if (_settings.NativeTouchpad && !_transport.IsTouchpadReady(target)) return ActivationStatus.Disconnected;
        if (!captureAvailable()) return ActivationStatus.CaptureUnavailable;
        if (!PhysicalIdle) return ActivationStatus.InputHeld;
        return ActivationStatus.Activated;
    }

    public void GoLocal(string? reason = null)
    {
        reason ??= L.T("Windowsへ戻しました");
        string? previous;
        lock (_gate)
        {
            previous = _remote ? _target : null;
            _remote = false; _epoch++; _dispatcher.SetEpoch(_epoch);
            _quarantine.UnionWith(_remoteKeys.Keys);
            _quarantineButtons |= _remoteButtons;
            _remoteKeys.Clear(); _remoteButtons = 0; _motion.Clear(); _pointer.Clear(); _horizontalScroll.Clear(); _touchpadScroll.Reset(); _touchpadWheel.Clear(); _nativeContactsActive = false; _lastNativeFrame = null; Reason = reason;
        }
        if (previous is not null) _ = _dispatcher.NeutralizeAsync(previous);
        Changed?.Invoke();
    }

    public void CheckConnection()
    {
        if (_remote && (_target is null || !_transport.IsReady(_target) || (_settings.NativeTouchpad && !_transport.IsTouchpadReady(_target)) || !_dispatcher.Healthy))
            GoLocal(L.T("接続断を検出しWindowsへ戻しました"));
    }

    private static long Identity(int vk, uint scan, bool extended) => scan == 0 ? 0x10000L + vk : scan | (extended ? 0x100L : 0);
    public bool Key(int vk, uint scan, bool extended, bool down, long now, bool japaneseKeyboard = false)
    {
        lock (_gate)
        {
            CheckConnection();
            var id = Identity(vk, scan, extended);
            var wasDown = _physical.ContainsKey(id);
            if (down) _physical[id] = PhysicalInputState.PollingKey(vk, scan, extended); else _physical.Remove(id);
            if (down && !wasDown)
            {
                if (vk == 0x1B) { _escapes = now - _lastEscape <= 800 ? _escapes + 1 : 1; _lastEscape = now; }
                else _escapes = 0;
                var mods = _physical.Values.Select(v => HidReports.Modifier(v, 0, false)).Aggregate((byte)0, (a, b) => (byte)(a | b));
                if (_remote && (_settings.ReturnShortcut.Matches(vk, mods) || _escapes >= 3))
                {
                    _quarantine.Add(id); GoLocal(L.T("緊急復帰ショートカットでWindowsへ戻しました")); return true;
                }
                // The activation chord is reserved locally; repeating it must not type I into iPad.
                if (_remote && _settings.ActivateShortcut.Matches(vk, mods))
                { _quarantine.Add(id); return true; }
                if (_remote && _settings.IPadShortcutsEnabled)
                {
                    var action = _settings.HomeShortcut.Matches(vk, mods) ? GestureAction.Home
                        : _settings.SearchShortcut.Matches(vk, mods) ? GestureAction.Search
                        : _settings.NextAppShortcut.Matches(vk, mods) ? GestureAction.NextApp : GestureAction.None;
                    if (action == GestureAction.None)
                        action = _settings.AdditionalShortcuts.FirstOrDefault(entry => entry.Enabled && entry.Binding.Matches(vk, mods))?.Action ?? GestureAction.None;
                    if (action != GestureAction.None)
                    {
                        _quarantine.Add(id); // Consume the trigger, its repeats, and its eventual release.
                        if (_remoteButtons == 0 && _remoteKeys.Values.All(key => key.Usage == 0)) SendActionShortcut(action);
                        return true;
                    }
                }
            }
            if (_quarantine.Contains(id)) { if (!down) _quarantine.Remove(id); return true; }
            if (!_remote) return false;
            if (VirtualKeyMap.IsJapaneseLanguageKey(vk, scan, extended, japaneseKeyboard))
            {
                // A tap, not a held/repeating key. Restore every other held key/modifier after
                // Ctrl+Space so Alt+Hankaku also works without sending an apostrophe or Alt+Space.
                if (down && !wasDown)
                {
                    FlushMotion();
                    byte heldModifiers = 0;
                    foreach (var key in _remoteKeys.Values) heldModifiers |= key.Modifier;
                    _dispatcher.Enqueue(_target!, _epoch, HidReports.Keyboard(1, [0x2C]));
                    _dispatcher.Enqueue(_target!, _epoch, HidReports.Keyboard(heldModifiers, _remoteKeys.Values.Select(k => k.Usage)));
                }
                return true;
            }
            if (down && !wasDown)
            {
                var modifier = HidReports.Modifier(vk, scan, extended);
                VirtualKeyMap.TryGetUsage(vk, scan, extended, out var usage);
                _remoteKeys[id] = (modifier, modifier == 0 ? usage : (byte)0);
            }
            else if (!down) _remoteKeys.Remove(id);
            else return true; // Let iPadOS perform key repeat from the held HID state.
            FlushMotion();
            byte modifiers = 0;
            foreach (var key in _remoteKeys.Values) modifiers |= key.Modifier;
            _dispatcher.Enqueue(_target!, _epoch, HidReports.Keyboard(modifiers, _remoteKeys.Values.Select(k => k.Usage)));
            return true;
        }
    }

    public bool Button(MouseButtons button, bool down)
    {
        lock (_gate)
        {
            CheckConnection();
            if (down) _physicalButtons |= button; else _physicalButtons &= ~button;
            if ((_quarantineButtons & button) != 0)
            { if (!down) _quarantineButtons &= ~button; return true; }
            if (!_remote) return false;
            FlushMotion();
            if (down) _remoteButtons |= button; else _remoteButtons &= ~button;
            _dispatcher.Enqueue(_target!, _epoch, HidReports.Mouse(_remoteButtons));
            return true;
        }
    }

    public void Move(double dx, double dy, int wheelDelta = 0)
    {
        lock (_gate)
        {
            CheckConnection(); if (!_remote) return;
            if (_nativeContactsActive) return;
            var x = dx * _settings.MouseSensitivity; var y = dy * _settings.MouseSensitivity;
            _pointer.Add(x, y); _motion.Add(0, 0, wheelDelta / 120d * _settings.ScrollSensitivity);
        }
    }

    public void FlushMotion()
    {
        lock (_gate)
        {
            if (!_remote) return;
            var wheel = _motion.Take().Wheel;
            var pan = _horizontalScroll.Take().Wheel;
            var touch = _touchpadWheel.Take(127); wheel += touch.Y; pan += touch.X;
            var wheelOverflow = wheel - Math.Clamp(wheel, -127, 127); var panOverflow = pan - Math.Clamp(pan, -127, 127);
            _motion.Add(0, 0, wheelOverflow); _horizontalScroll.Add(0, 0, panOverflow);
            wheel -= wheelOverflow; pan -= panOverflow;
            var now = _clock.GetTimestamp(); var elapsed = _clock.GetElapsedTime(_lastPointerReport, now).TotalMilliseconds; _lastPointerReport = now;
            var (x, y) = _pointer.Take(_settings.StabilizeFastPointer, elapsed);
            if (Cursor.Move(x, y, _settings) && _settings.EstimatedReturn)
            { GoLocal(L.T("推定カーソルが境界に達したためWindowsへ戻しました")); return; }
            if (x != 0 || y != 0 || wheel != 0 || pan != 0) _dispatcher.EnqueueMotion(_target!, _epoch, HidReports.Mouse(_remoteButtons, x, y, wheel, pan));
        }
    }
    public void Scroll(double verticalDelta, double horizontalDelta = 0)
    {
        lock (_gate)
        {
            CheckConnection(); if (!_remote) return;
            if (_settings.NativeTouchpad && _nativeContactsActive) return;
            _motion.Add(0, 0, verticalDelta / 120 * _settings.ScrollSensitivity);
            _horizontalScroll.Add(0, 0, horizontalDelta / 120 * _settings.ScrollSensitivity);
        }
    }
    public void BeginTouchpadScroll() { lock (_gate) _touchpadScroll.Reset(); }
    public void ScrollTouchpad(double cumulativeX, double cumulativeY)
    {
        lock (_gate)
        {
            CheckConnection(); if (!_remote) return;
            if (_settings.NativeTouchpad) return;
            var (vertical, horizontal) = _touchpadScroll.Update(cumulativeX, cumulativeY, _settings.TouchpadScrollSensitivity);
            _touchpadWheel.Add(horizontal / 120, vertical / 120, 0);
        }
    }
    public bool NativeTouchpadFrame(HidFrame frame, bool activeContacts)
    {
        HidReports.Validate(frame);
        if (frame.Kind != ReportKind.Touchpad) throw new ArgumentException("Expected touchpad report.");
        lock (_gate)
        {
            CheckConnection();
            if (!_remote || !_settings.NativeTouchpad || !_transport.IsTouchpadReady(_target!)) return false;
            if (!_nativeContactsActive && activeContacts) { _pointer.Clear(); _touchpadWheel.Clear(); }
            _nativeContactsActive = activeContacts;
            var transition = _lastNativeFrame is null || frame.Payload[2] != _lastNativeFrame[2] ||
                Enumerable.Range(0, 5).Any(slot => frame.Payload[3 + slot * 5] != _lastNativeFrame[3 + slot * 5]);
            var queued = transition ? _dispatcher.Enqueue(_target!, _epoch, frame) : _dispatcher.EnqueueTouchpadUpdate(_target!, _epoch, frame);
            if (queued && _remote) _lastNativeFrame = frame.Payload;
            return queued;
        }
    }
    public bool Gesture(GestureAction action)
    {
        lock (_gate)
        {
            CheckConnection();
            if (!_remote || _remoteKeys.Count != 0 || _remoteButtons != 0) return false;
            var (modifiers, usage) = TouchpadGestures.Shortcut(action);
            if (usage == 0) return false;
            FlushMotion();
            return _dispatcher.Enqueue(_target!, _epoch, HidReports.Keyboard(modifiers, [usage])) &&
                _dispatcher.Enqueue(_target!, _epoch, HidReports.Keyboard(0, []));
        }
    }

    private void SendActionShortcut(GestureAction action)
    {
        FlushMotion(); if (!_remote) return;
        var target = _target!; var epoch = _epoch;
        var (modifiers, usage) = TouchpadGestures.Shortcut(action);
        var held = _remoteKeys.Values.Aggregate((byte)0, (value, key) => (byte)(value | key.Modifier));
        // Remove the local Ctrl/Alt chord from the command; restore held modifiers afterwards.
        // Snapshot the epoch so a synchronous queue failure cannot send keys into the next mode.
        _ = _dispatcher.Enqueue(target, epoch, HidReports.Keyboard(0, [])) &&
            _dispatcher.Enqueue(target, epoch, HidReports.Keyboard(modifiers, [usage])) &&
            _dispatcher.Enqueue(target, epoch, HidReports.Keyboard(0, [])) &&
            (held == 0 || _dispatcher.Enqueue(target, epoch, HidReports.Keyboard(held, [])));
    }

    // Reconcile keys released on another desktop (UAC/lock) where hooks cannot observe them.
    public void ReconcilePhysical(Func<int, bool> isDown)
    {
        lock (_gate)
        {
            if (_remote) return;
            foreach (var pair in _physical.Where(p => !isDown(p.Value)).ToArray()) { _physical.Remove(pair.Key); _quarantine.Remove(pair.Key); }
            // No released physical key may leave an orphaned quarantine entry behind.
            _quarantine.IntersectWith(_physical.Keys);
            if (!isDown(1)) { _physicalButtons &= ~MouseButtons.Left; _quarantineButtons &= ~MouseButtons.Left; }
            if (!isDown(2)) { _physicalButtons &= ~MouseButtons.Right; _quarantineButtons &= ~MouseButtons.Right; }
            if (!isDown(4)) { _physicalButtons &= ~MouseButtons.Middle; _quarantineButtons &= ~MouseButtons.Middle; }
        }
    }
}
