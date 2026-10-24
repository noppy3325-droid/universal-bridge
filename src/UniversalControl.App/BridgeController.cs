using UniversalControl.Core;
using UniversalControl.Windows;

namespace UniversalControl.App;

public sealed class BridgeController : IAsyncDisposable
{
    private readonly SettingsStore _store;
    private readonly NativeInput _input;
    private bool _nativeStarted, _desired, _busy, _disposed;
    private readonly ReconnectPolicy _retry = new();
    public BleHidTransport Transport { get; } = new();
    public ReportDispatcher Reports { get; }
    public InputRouter Router { get; }
    public DiagnosticLog Log { get; }
    public AppSettings Settings { get; private set; }
    public IReadOnlyList<DisplayInfo> Displays { get; private set; }
    public string? SelectedPeerId { get; set; }
    public string Message { get; private set; } = L.T("「Bluetooth HIDを公開」から開始してください。");
    public bool Busy => _busy;
    public bool SmokeMode { get; }
    public string MouseMode => !_nativeStarted && Settings.RawMouse ? L.T("相対入力（HID公開後に開始）") : _input.MouseMode;
    public bool PreciseMotionTiming => _input.PreciseMotionTiming;
    public int MouseReportIntervalMs => _input.ReportIntervalMs;
    public string ActivationShortcut => _nativeStarted && !_input.ActivationHotkeyRegistered ? Settings.ActivateShortcut + L.T(" は他のアプリで使用中です") : L.T("iPadへ切り替え　") + Settings.ActivateShortcut + L.T("（キーを離すと切り替え）");
    public string ReturnShortcutText => L.T("Windowsへ戻る　") + Settings.ReturnShortcut + L.T(" ／ Esc を3回");
    public string TouchpadStatus => _nativeStarted ? _input.TouchpadStatus : L.T("HID公開後にジェスチャー取得の対応状況を確認します");
    public event Action? Changed;

    public BridgeController(string dataDirectory, bool smokeMode)
    {
        SmokeMode = smokeMode; _store = new(dataDirectory); Settings = _store.Load();
        if (!smokeMode) Settings = Settings with { AutoStart = StartupRegistration.Enabled };
        Log = new(dataDirectory); if (_store.Warning is { } warning) Log.Write(warning);
        Displays = DisplayCatalog.Read();
        Reports = new(Transport); Router = new(Transport, Reports); _input = new(Router);
        Router.Configure(Settings); _input.Configure(Settings, Displays);
        Transport.Log += Log.Write;
        Transport.Changed += TransportChanged;
        Router.Changed += RouterChanged;
        _input.Faulted += text => { Message = text; Log.Write(text); Changed?.Invoke(); };
        _input.ActivationRequested += async () => { try { await ActivateRemoteAsync(); } catch (Exception ex) { NotifyError(ex); } };
        Log.Write(L.T("Universal Bridge 0.3.4を起動しました。入力内容は記録しません。"));
    }
    private void TransportChanged() { Router.CheckConnection(); Changed?.Invoke(); }
    private void RouterChanged() { Message = Router.Reason; Log.Write(Router.Reason); Changed?.Invoke(); }
    public void NotifyError(Exception ex) { Router.GoLocal(L.T("エラーによりWindowsへ戻しました")); Message = ex.Message; Log.Write(L.T("エラー: ") + ex.Message); Changed?.Invoke(); }

    public async Task StartAsync(bool retry = false)
    {
        if (_busy || _disposed || SmokeMode) return;
        _busy = true; _desired = true;
        if (!retry) _retry.Reset();
        Changed?.Invoke();
        try
        {
            await Transport.StartAsync();
            if (!_nativeStarted) { await _input.StartAsync(); _nativeStarted = true; }
            Log.Write(L.T("マウス入力: ") + MouseMode);
            Log.Write(_input.ActivationHotkeyRegistered ? L.T("iPad切り替えショートカット ") + Settings.ActivateShortcut + L.T(" を登録しました。") : Settings.ActivateShortcut + L.T(" を登録できませんでした（他のアプリで使用中）。"));
            Log.Write(L.T("ジェスチャー取得: ") + TouchpadStatus);
            Message = _input.ActivationHotkeyRegistered ? L.F("iPadのBluetooth接続・端末選択後、{0} でiPadへ切り替えられます。", Settings.ActivateShortcut)
                : Settings.ActivateShortcut + L.T(" は他のアプリで使用中です。入力設定で変更してください。");
            _retry.Reset();
        }
        catch (Exception ex)
        {
            Router.GoLocal(L.T("公開できないためWindowsで操作を続けます")); Message = ex.Message; Log.Write(L.T("公開失敗: ") + ex.Message);
            _retry.RecordFailure(Environment.TickCount64);
        }
        finally { _busy = false; Changed?.Invoke(); }
    }
    public async Task StopAsync()
    {
        if (_busy) return;
        _desired = false; Router.GoLocal(L.T("HID公開を停止しWindowsへ戻しました"));
        if (SelectedPeerId is { } target) await Reports.NeutralizeAsync(target);
        Transport.Stop(); Message = L.T("公開を停止しました。OSのペアリング登録は維持されます。"); Changed?.Invoke();
    }
    public async Task SwitchRemoteAsync()
    {
        if (_disposed || SmokeMode) return;
        if (Router.IsRemote) { Router.GoLocal(); return; }
        await ActivateRemoteAsync();
    }
    public async Task ActivateRemoteAsync()
    {
        if (_disposed || SmokeMode || Router.IsRemote) return;
        if (Settings.CaptureTouchpad && !_input.TouchpadAvailable)
        { SwitchFailed(L.T("ジェスチャー取得未対応"), L.T("このWindowsではジェスチャー取得を開始できません。入力設定で取得をオフにするとマウス・キーボードのみ切り替えられます。")); return; }
        if (SelectedPeerId is not { } target) { SwitchFailed(L.T("端末未選択"), L.T("入力送信可能なiPadを一覧から選択してください。")); return; }
        await Transport.RefreshPeersAsync(wait: true);
        if (!Transport.IsReady(target)) { SwitchFailed(L.T("接続準備待ち"), Transport.DescribeReadiness(target)); return; }
        var generation = Router.Generation;
        // Hotkey modifiers must be released locally before ownership changes.
        var deadline = Environment.TickCount64 + 2000;
        while (true)
        {
            _input.ReconcilePhysical();
            if ((NativeInput.PhysicalKeyboardIdle() && Router.PhysicalIdle) || Environment.TickCount64 >= deadline) break;
            await Task.Delay(25);
        }
        string? captureFailure = null;
        bool CaptureAvailable()
        {
            captureFailure = _disposed || SelectedPeerId != target || Router.Generation != generation ? L.T("切り替え操作が取り消されました。端末を選び直してください。")
                : !_input.Available ? L.T("入力監視が応答していません。アプリを終了して再起動してください。")
                : !NativeInput.OnDefaultDesktop() ? L.T("通常のWindowsデスクトップへ戻ってから再試行してください。")
                : !NativeInput.PhysicalKeyboardIdle() ? L.T("キーまたはマウスボタンをすべて離してから再試行してください。") : null;
            return captureFailure is null;
        }
        var result = await Router.TryActivateAsync(target, CaptureAvailable);
        if (result == ActivationStatus.Activated)
        {
            if (!await _input.ConfirmModeAsync()) { Message = Router.Reason; Changed?.Invoke(); return; }
            Message = Settings.ReturnShortcut + L.T("、または Esc を3回押すとWindowsへ戻れます。");
        }
        else
        {
            SwitchFailed(result.ToString(), result switch
            {
                ActivationStatus.CaptureUnavailable => captureFailure ?? L.T("入力監視を確認して再試行してください。"),
                ActivationStatus.InputHeld => L.T("キーまたはマウスボタンをすべて離してから再試行してください。"),
                ActivationStatus.Disconnected => Transport.DescribeReadiness(target),
                ActivationStatus.SenderStopped => L.T("送信処理を復帰できませんでした。iPadの接続を確認し、少し待って再試行してください。"),
                ActivationStatus.Releasing or ActivationStatus.AlreadyActivating => L.T("切り替え処理中です。少し待ってから再試行してください。"),
                _ => L.T("切り替え操作が取り消されました。もう一度切り替えてください。")
            });
            return;
        }
        Changed?.Invoke();
    }
    private void SwitchFailed(string reason, string detail)
    {
        Message = L.T("切り替えできませんでした。") + detail; Log.Write(L.F("入力切り替え拒否 [{0}]: {1}", reason, detail));
        if (reason is nameof(ActivationStatus.InputHeld) or nameof(ActivationStatus.CaptureUnavailable))
        {
            var held = Router.HeldState;
            Log.Write(L.F("入力解放診断: 追跡キー={0}, 解放待ちキー={1}, マウス押下={2}, マウス解放待ち={3}, OS物理入力なし={4}",
                held.TrackedKeys, held.ReleasePendingKeys, held.MouseHeld, held.MouseReleasePending, NativeInput.PhysicalKeyboardIdle()));
        }
        Changed?.Invoke();
    }
    public async Task TickAsync()
    {
        if (_disposed || SmokeMode) return;
        Router.CheckConnection(); await Transport.RefreshPeersAsync();
        if (SelectedPeerId is { } target) _input.ReportIntervalMs = Transport.ReportIntervalMs(target);
        if (_desired && Settings.Reconnect && !Transport.Publishing && !_busy && _retry.CanRetry(Environment.TickCount64)) await StartAsync(true);
    }
    public void Save(AppSettings settings)
    {
        var normalized = settings.Normalize();
        if (normalized.RawMouse != Settings.RawMouse || normalized.CaptureTouchpad != Settings.CaptureTouchpad)
            Router.GoLocal(L.T("入力方式を変更したためWindowsへ戻しました"));
        if (!SmokeMode && normalized.AutoStart != Settings.AutoStart)
            StartupRegistration.Set(normalized.AutoStart, Environment.ProcessPath ?? throw new InvalidOperationException(L.T("実行ファイルのパスを取得できません。")));
        _store.Save(normalized); Settings = normalized;
        Router.Configure(Settings); _input.Configure(Settings, Displays); Changed?.Invoke();
    }
    public async Task ApplyShortcutsAsync(ShortcutBinding activate, ShortcutBinding back)
    {
        ShortcutBinding.ValidatePair(activate, back);
        Router.GoLocal(L.T("ショートカットの設定を変更するためWindowsへ戻しました"));
        var previous = Settings;
        if (_nativeStarted && await _input.UpdateShortcutsAsync(activate, back) is { } conflict) throw new InvalidOperationException(conflict);
        var updated = Settings with { ActivateShortcut = activate, ReturnShortcut = back };
        try { _store.Save(updated); }
        catch { if (_nativeStarted) await _input.UpdateShortcutsAsync(previous.ActivateShortcut, previous.ReturnShortcut); throw; }
        Settings = updated; Router.Configure(Settings); _input.Configure(Settings, Displays);
        Message = L.T("ショートカットを保存・適用しました。"); Log.Write(L.F("ショートカット変更: iPad={0}, Windows={1}", activate, back)); Changed?.Invoke();
    }
    public void RefreshDisplays()
    { Router.GoLocal(L.T("画面構成が変更されたためWindowsへ戻しました")); Displays = DisplayCatalog.Read(); _input.Configure(Settings, Displays); Changed?.Invoke(); }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true; _desired = false; _input.Dispose();
        if (SelectedPeerId is { } target) await Reports.NeutralizeAsync(target);
        await Reports.DisposeAsync(); Transport.Changed -= TransportChanged; Router.Changed -= RouterChanged;
        await Transport.DisposeAsync(); Log.Write(L.T("フックとBluetoothリソースを解放しました。")); await Log.DisposeAsync();
    }
}
