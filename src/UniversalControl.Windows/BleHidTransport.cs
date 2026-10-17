using System.Collections.Concurrent;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Radios;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using UniversalControl.Core;

namespace UniversalControl.Windows;

public sealed record AdapterProbe(string OS, bool AdapterFound, string Radio, bool LowEnergy, bool Peripheral, string? HidServiceResult, string? Error);
public sealed record PeerInfo(string Id, string Name, bool Paired, bool Connected, bool Keyboard, bool Mouse, bool Ready, bool Suspended = false)
{
    public string Readiness => !Connected ? L.T("切断中。iPadのBluetooth接続を確認してください。")
        : !Paired ? L.T("ペアリング確認を完了してください。")
        : Suspended ? L.T("iPadがHIDを一時停止中です。iPadの画面を開き、Bluetooth接続を確認してください。")
        : !Keyboard || !Mouse ? L.T("iPadのキーボード・マウス購読を待機中です。")
        : Ready ? L.T("入力送信可能") : L.T("接続情報を更新中です。少し待ってから再試行してください。");
    public override string ToString() => $"{Name}  ·  {Readiness}";
}

/// <summary>Encrypted HOGP server. Notifications always target exactly one subscribed central.</summary>
public sealed class BleHidTransport : IHidTransport, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, BluetoothLEDevice> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly HidSuspensionState<GattSession> _suspended = new();
    private readonly ConcurrentDictionary<string, byte> _leds = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<GattSession, byte> _sessions = new();
    private readonly ConcurrentDictionary<string, byte[]> _lastKeyboard = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte[]> _lastMouse = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _refresh = new(1);
    private readonly SemaphoreSlim _lifecycle = new(1);
    private readonly CancellationTokenSource _stop = new();
    private readonly List<GattLocalCharacteristic> _characteristics = [];
    private GattServiceProvider? _provider;
    private GattLocalCharacteristic? _keyboard, _mouse;
    private Radio? _radio;
    private TaskCompletionSource? _advertising;
    private volatile bool _enabled, _disposed;
    private bool _buildFailed;
    private int _advertisingIssued;
    private readonly object _snapshotGate = new();
    private volatile HashSet<string> _readyTargets = new(StringComparer.OrdinalIgnoreCase);
    private volatile PeerInfo[] _peers = [];
    private long _snapshotAt, _stateRevision;
    public string Status { get; private set; } = L.T("停止中");
    private string? _adapterSummary;
    public string AdapterSummary { get => _adapterSummary ?? L.T("未確認"); private set => _adapterSummary = value; }
    public bool Publishing => _enabled && _provider?.AdvertisementStatus == GattServiceProviderAdvertisementStatus.Started;
    public int CharacteristicCount => _characteristics.Count;
    public event Action? Changed;
    public event Action<string>? Log;

    public static async Task<AdapterProbe> ProbeAsync(bool probeHid = false)
    {
        try
        {
            var adapter = await BluetoothAdapter.GetDefaultAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            if (adapter is null) return new(Environment.OSVersion.VersionString, false, L.T("なし"), false, false, null, L.T("Bluetoothアダプターがありません"));
            var radio = await adapter.GetRadioAsync();
            string? result = null;
            if (probeHid && adapter.IsPeripheralRoleSupported)
            {
                var created = await GattServiceProvider.CreateAsync(HidDescriptors.HidService).AsTask().WaitAsync(TimeSpan.FromSeconds(10));
                result = created.Error.ToString();
                // No advertising, no hooks, no pairing. The probe exits after recording the result.
            }
            return new(Environment.OSVersion.VersionString, true, $"{radio?.Name} ({radio?.State})", adapter.IsLowEnergySupported, adapter.IsPeripheralRoleSupported, result, null);
        }
        catch (Exception ex) { return new(Environment.OSVersion.VersionString, false, L.T("不明"), false, false, null, ex.Message); }
    }

    public async Task StartAsync()
    {
        await _lifecycle.WaitAsync(_stop.Token);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (Publishing) return;
            if (_buildFailed) throw new InvalidOperationException(L.T("GATTの初期化に失敗しました。アプリを再起動してください。"));
            if (_provider is null)
            {
                try { await BuildAsync(); }
                catch { _buildFailed = _provider is not null; throw; }
            }
            if (_radio?.State != RadioState.On) throw new InvalidOperationException(L.T("WindowsのBluetoothをオンにしてください。"));
            if (Volatile.Read(ref _advertisingIssued) == 0)
            {
                var stopDeadline = Environment.TickCount64 + 3000;
                while (_provider!.AdvertisementStatus == GattServiceProviderAdvertisementStatus.Started && Environment.TickCount64 < stopDeadline)
                    await Task.Delay(50, _stop.Token);
            }
            _advertising = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _enabled = true; Status = L.T("公開を開始中");
            Interlocked.Exchange(ref _advertisingIssued, 1);
            _provider!.StartAdvertising(new GattServiceProviderAdvertisingParameters { IsConnectable = true, IsDiscoverable = true });
            await _advertising.Task.WaitAsync(TimeSpan.FromSeconds(12), _stop.Token);
            Status = L.T("HID公開中・iPadからペアリングしてください");
            Log?.Invoke(L.T("暗号化必須のHIDサービスを公開しました。"));
        }
        catch (Exception ex)
        {
            _enabled = false; Status = L.T("公開失敗: ") + ex.Message;
            StopAdvertisingSafely(); throw;
        }
        finally { _lifecycle.Release(); Changed?.Invoke(); }
    }

    private async Task BuildAsync()
    {
        var adapter = await BluetoothAdapter.GetDefaultAsync().AsTask(_stop.Token).WaitAsync(TimeSpan.FromSeconds(10))
            ?? throw new NotSupportedException(L.T("Bluetoothアダプターが見つかりません。"));
        AdapterSummary = $"BLE: {adapter.IsLowEnergySupported} / Peripheral: {adapter.IsPeripheralRoleSupported}";
        if (!adapter.IsLowEnergySupported || !adapter.IsPeripheralRoleSupported)
            throw new NotSupportedException(L.T("この内蔵Bluetooth／ドライバーはBLE Peripheralに対応していません。"));
        _radio = await adapter.GetRadioAsync();
        if (_radio is null || _radio.State != RadioState.On) throw new InvalidOperationException(L.T("Bluetoothが無効です。Windows設定でオンにしてください。"));
        _radio.StateChanged += RadioChanged;
        var result = await GattServiceProvider.CreateAsync(HidDescriptors.HidService).AsTask(_stop.Token).WaitAsync(TimeSpan.FromSeconds(10));
        if (result.Error != BluetoothError.Success) throw new InvalidOperationException(L.F("HIDサービス (0x1812) の作成結果: {0}。ポリシー／ドライバーを確認してください。", result.Error));
        _provider = result.ServiceProvider;
        _provider.AdvertisementStatusChanged += AdvertisingChanged;
        await ConstantAsync(HidDescriptors.HidInformation, HidDescriptors.HidInformationValue);
        await ConstantAsync(HidDescriptors.ReportMap, HidDescriptors.ReportMapValue);
        var control = await CharacteristicAsync(HidDescriptors.HidControlPoint, GattCharacteristicProperties.WriteWithoutResponse);
        control.WriteRequested += async (sender, args) => await HandleWriteAsync(args, (id, bytes) =>
        {
            if (bytes.Length != 1 || bytes[0] > 1) return false;
            if (_sessions.TryAdd(args.Session, 0)) args.Session.SessionStatusChanged += SessionChanged;
            _suspended.Set(id, args.Session, bytes[0] == 0);
            InvalidateReadiness(); _ = RefreshPeersAsync(); return true;
        });
        _keyboard = await InputAsync(HidDescriptors.KeyboardReportId, ReportKind.Keyboard);
        var output = await CharacteristicAsync(HidDescriptors.Report, GattCharacteristicProperties.Read | GattCharacteristicProperties.Write | GattCharacteristicProperties.WriteWithoutResponse);
        output.ReadRequested += async (_, args) => await HandleReadAsync(args, id => [_leds.GetValueOrDefault(id)]);
        output.WriteRequested += async (_, args) => await HandleWriteAsync(args, (id, bytes) =>
        { if (bytes.Length != 1) return false; _leds[id] = bytes[0]; return true; });
        await ReferenceAsync(output, HidDescriptors.KeyboardReportId, HidDescriptors.ReportTypeOutput);
        _mouse = await InputAsync(HidDescriptors.MouseReportId, ReportKind.Mouse);
        // Report protocol only: omit Protocol Mode and Boot characteristics rather than claiming boot support.
        Log?.Invoke(L.T("HID 0x1812、キーボード8バイト、マウス7バイト（横スクロール対応）を作成しました。"));
    }

    private void AdvertisingChanged(GattServiceProvider sender, GattServiceProviderAdvertisementStatusChangedEventArgs args)
    {
        if (_disposed) return;
        if (args.Status == GattServiceProviderAdvertisementStatus.Started) _advertising?.TrySetResult();
        else if (args.Status == GattServiceProviderAdvertisementStatus.Aborted)
            Status = L.F("公開状態: {0} ({1})", args.Status, args.Error);
        Changed?.Invoke();
    }
    private void RadioChanged(Radio sender, object args)
    { if (_disposed) return; if (sender.State != RadioState.On) Status = L.T("Bluetoothがオフになりました"); InvalidateReadiness(); _ = RefreshPeersAsync(); }

    private async Task ConstantAsync(Guid uuid, byte[] value)
    {
        var result = await _provider!.Service.CreateCharacteristicAsync(uuid, new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read,
            ReadProtectionLevel = GattProtectionLevel.EncryptionRequired,
            StaticValue = CryptographicBuffer.CreateFromByteArray(value)
        });
        Ensure(result.Error); _characteristics.Add(result.Characteristic);
    }
    private async Task<GattLocalCharacteristic> CharacteristicAsync(Guid uuid, GattCharacteristicProperties properties)
    {
        var result = await _provider!.Service.CreateCharacteristicAsync(uuid, new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = properties,
            ReadProtectionLevel = GattProtectionLevel.EncryptionRequired,
            WriteProtectionLevel = GattProtectionLevel.EncryptionRequired
        });
        Ensure(result.Error); _characteristics.Add(result.Characteristic); return result.Characteristic;
    }
    private static void Ensure(BluetoothError error)
    { if (error != BluetoothError.Success) throw new InvalidOperationException(L.T("GATT属性の作成失敗: ") + error); }
    private static async Task ReferenceAsync(GattLocalCharacteristic characteristic, byte id, byte type)
    {
        var descriptor = await characteristic.CreateDescriptorAsync(HidDescriptors.ReportReference, new GattLocalDescriptorParameters
        { StaticValue = CryptographicBuffer.CreateFromByteArray([id, type]), ReadProtectionLevel = GattProtectionLevel.EncryptionRequired });
        Ensure(descriptor.Error);
    }
    private async Task<GattLocalCharacteristic> InputAsync(byte id, ReportKind kind)
    {
        var characteristic = await CharacteristicAsync(HidDescriptors.Report, GattCharacteristicProperties.Read | GattCharacteristicProperties.Notify);
        characteristic.ReadRequested += async (_, args) => await HandleReadAsync(args, peer => kind == ReportKind.Keyboard
            ? _lastKeyboard.GetValueOrDefault(peer, new byte[8]) : _lastMouse.GetValueOrDefault(peer, new byte[7]));
        characteristic.SubscribedClientsChanged += SubscribersChanged;
        await ReferenceAsync(characteristic, id, HidDescriptors.ReportTypeInput);
        return characteristic;
    }
    private async Task HandleReadAsync(GattReadRequestedEventArgs args, Func<string, byte[]> value)
    {
        using var deferral = args.GetDeferral();
        try
        {
            var request = await args.GetRequestAsync();
            if (request is null) return;
            var bytes = value(args.Session.DeviceId.Id);
            if (request.Offset > bytes.Length) request.RespondWithProtocolError(0x07);
            else request.RespondWithValue(CryptographicBuffer.CreateFromByteArray(bytes[(int)request.Offset..]));
        }
        catch (Exception ex) { if (!_disposed) Log?.Invoke(L.T("GATT読取処理: ") + ex.Message); }
    }
    private async Task HandleWriteAsync(GattWriteRequestedEventArgs args, Func<string, byte[], bool> apply)
    {
        using var deferral = args.GetDeferral();
        try
        {
            var request = await args.GetRequestAsync(); if (request is null) return;
            CryptographicBuffer.CopyToByteArray(request.Value, out var bytes);
            var valid = request.Offset == 0 && apply(args.Session.DeviceId.Id, bytes);
            if (request.Option == GattWriteOption.WriteWithResponse)
            { if (valid) request.Respond(); else request.RespondWithProtocolError(0x0D); }
        }
        catch (Exception ex) { if (!_disposed) Log?.Invoke(L.T("GATT書込処理: ") + ex.Message); }
    }

    private void SubscribersChanged(GattLocalCharacteristic sender, object args)
    { if (_disposed) return; InvalidateReadiness(); _ = RefreshPeersAsync(); }
    public async Task RefreshPeersAsync(bool wait = false)
    {
        if (_disposed) return;
        if (wait) await _refresh.WaitAsync(_stop.Token);
        else if (!await _refresh.WaitAsync(0)) return;
        try
        {
            var revision = Interlocked.Read(ref _stateRevision);
            var clients = (_keyboard?.SubscribedClients ?? []).Concat(_mouse?.SubscribedClients ?? []).ToArray();
            var sessions = clients.Select(client => client.Session).Distinct().ToArray();
            foreach (var session in sessions)
                if (_sessions.TryAdd(session, 0)) session.SessionStatusChanged += SessionChanged;
            foreach (var session in _sessions.Keys)
                if (!sessions.Contains(session) && session.SessionStatus == GattSessionStatus.Closed && _sessions.TryRemove(session, out _)) session.SessionStatusChanged -= SessionChanged;
            foreach (var group in clients.GroupBy(client => client.Session.DeviceId.Id, StringComparer.OrdinalIgnoreCase))
            {
                var client = group.FirstOrDefault(client => client.Session.SessionStatus == GattSessionStatus.Active) ?? group.First();
                var id = client.Session.DeviceId.Id;
                if (_devices.TryGetValue(id, out var existing) && (existing.ConnectionStatus == BluetoothConnectionStatus.Connected || client.Session.SessionStatus != GattSessionStatus.Active)) continue;
                var device = await BluetoothLEDevice.FromIdAsync(id).AsTask(_stop.Token).WaitAsync(TimeSpan.FromSeconds(5));
                if (device is null) continue;
                if (_disposed) { device.Dispose(); return; }
                if (!ReferenceEquals(existing, device)) device.ConnectionStatusChanged += DeviceChanged;
                _devices[id] = device;
                if (existing is not null && !ReferenceEquals(existing, device)) { existing.ConnectionStatusChanged -= DeviceChanged; existing.Dispose(); }
            }
            UpdateSnapshots(revision);
        }
        catch (Exception ex) when (!_disposed) { Log?.Invoke(L.T("接続状態の取得失敗: ") + ex.Message); }
        catch (OperationCanceledException) when (_disposed) { }
        finally { _refresh.Release(); if (!_disposed) Changed?.Invoke(); }
    }
    private void SessionChanged(GattSession sender, GattSessionStatusChangedEventArgs args)
    { if (_disposed) return; InvalidateReadiness(); _ = RefreshPeersAsync(); }
    private void DeviceChanged(BluetoothLEDevice sender, object args)
    {
        if (_disposed) return;
        if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
        { _lastKeyboard.TryRemove(sender.DeviceId, out _); _lastMouse.TryRemove(sender.DeviceId, out _); _suspended.Reset(sender.DeviceId); }
        InvalidateReadiness(); _ = RefreshPeersAsync();
    }
    private static bool Subscribed(GattLocalCharacteristic? characteristic, string id) => characteristic?.SubscribedClients.Any(
        client => client.Session.DeviceId.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && client.Session.SessionStatus == GattSessionStatus.Active) == true;
    // This method is called inside low-level hooks: it must never call WinRT or wait for a lock.
    public bool IsReady(string targetId) => !_disposed && _enabled && Environment.TickCount64 - Interlocked.Read(ref _snapshotAt) < 2000 && _readyTargets.Contains(targetId);
    public IReadOnlyList<PeerInfo> Peers() => _peers;
    public string DescribeReadiness(string targetId)
    {
        if (!_enabled) return L.T("Bluetooth HIDを公開してください。");
        if (_radio?.State != RadioState.On) return L.T("WindowsのBluetoothをオンにしてください。");
        var peer = _peers.FirstOrDefault(p => p.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase));
        if (peer is null) return L.T("端末とのHID接続を待機中です。iPadのBluetooth接続を確認してください。");
        if (!peer.Ready) return peer.Readiness;
        return IsReady(targetId) ? peer.Readiness : L.T("接続情報を更新中です。少し待ってから再試行してください。");
    }
    private void InvalidateReadiness()
    {
        lock (_snapshotGate) { _lastKeyboard.Clear(); _lastMouse.Clear(); _stateRevision++; _readyTargets = new(StringComparer.OrdinalIgnoreCase); Interlocked.Exchange(ref _snapshotAt, 0); }
        Changed?.Invoke();
    }
    private void UpdateSnapshots(long revision)
    {
        var radioOn = _radio?.State == RadioState.On;
        var peers = _devices.Select(pair =>
        {
            var device = pair.Value; var paired = device.DeviceInformation.Pairing.IsPaired;
            var connected = device.ConnectionStatus == BluetoothConnectionStatus.Connected;
            var keyboard = Subscribed(_keyboard, pair.Key); var mouse = Subscribed(_mouse, pair.Key);
            var suspended = _suspended.Observe(pair.Key, session => session.SessionStatus == GattSessionStatus.Active, keyboard || mouse);
            return new PeerInfo(pair.Key, string.IsNullOrWhiteSpace(device.Name) ? L.T("Bluetooth端末") : device.Name,
                paired, connected, keyboard, mouse, _enabled && radioOn && paired && connected && keyboard && mouse && !suspended, suspended);
        }).OrderBy(peer => peer.Name).ToArray();
        PeerInfo[] previous;
        lock (_snapshotGate)
        {
            if (revision != _stateRevision || _disposed) return;
            previous = _peers;
            _peers = peers; _readyTargets = peers.Where(peer => peer.Ready).Select(peer => peer.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Interlocked.Exchange(ref _snapshotAt, Environment.TickCount64);
        }
        foreach (var peer in peers)
        {
            var old = previous.FirstOrDefault(p => p.Id.Equals(peer.Id, StringComparison.OrdinalIgnoreCase));
            if (old is null || old.Paired != peer.Paired || old.Connected != peer.Connected || old.Keyboard != peer.Keyboard || old.Mouse != peer.Mouse || old.Suspended != peer.Suspended || old.Ready != peer.Ready)
                Log?.Invoke(L.F("HID接続状態: ペア={0}, 接続={1}, キーボード={2}, マウス={3}, 一時停止={4}, 送信可能={5}", peer.Paired, peer.Connected, peer.Keyboard, peer.Mouse, peer.Suspended, peer.Ready));
        }
    }

    public int ReportIntervalMs(string id)
    {
        try
        {
            if (_devices.TryGetValue(id, out var device))
                return Math.Clamp((int)Math.Ceiling(device.GetConnectionParameters().ConnectionInterval * 1.25), 15, 100);
        }
        catch (Exception) { /* Fall back to conservative 15 ms local batching. */ }
        return 15;
    }
    public async Task SendAsync(string targetId, HidFrame frame, CancellationToken cancellationToken)
    {
        HidReports.Validate(frame);
        if (!IsReady(targetId)) throw new IOException(L.T("選択端末の暗号化済みキーボード・マウス接続が利用できません。"));
        if (!await NotifyTargetAsync(targetId, frame, cancellationToken)) throw new IOException(L.T("選択端末へのHID通知が失敗しました。"));
    }
    private async Task<bool> NotifyTargetAsync(string targetId, HidFrame frame, CancellationToken cancellationToken)
    {
        var characteristic = frame.Kind == ReportKind.Keyboard ? _keyboard! : _mouse!;
        var client = characteristic.SubscribedClients.FirstOrDefault(c => c.Session.DeviceId.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase) && c.Session.SessionStatus == GattSessionStatus.Active);
        if (client is null) return false;
        var revision = Interlocked.Read(ref _stateRevision);
        var result = await characteristic.NotifyValueAsync(CryptographicBuffer.CreateFromByteArray(frame.Payload), client).AsTask(cancellationToken);
        if (result.Status != GattCommunicationStatus.Success) return false;
        lock (_snapshotGate)
        {
            if (revision != _stateRevision) return true;
            if (frame.Kind == ReportKind.Keyboard) _lastKeyboard[targetId] = frame.Payload.ToArray();
            else _lastMouse[targetId] = [(byte)(frame.Payload[0] & 7), 0, 0, 0, 0, 0, 0];
        }
        return true;
    }
    public async Task<int> ReleaseAsync(string targetId, CancellationToken cancellationToken)
    {
        _lastKeyboard.TryRemove(targetId, out _); _lastMouse.TryRemove(targetId, out _);
        if (_disposed || _radio?.State != RadioState.On || !_devices.TryGetValue(targetId, out var device) || device.ConnectionStatus != BluetoothConnectionStatus.Connected) return 0;
        var sent = 0;
        // Neutral reports contain no input. Try each still-subscribed report independently,
        // even after subscription/radio notifications revoked the fast capture permission.
        if (_keyboard is not null && await NotifyTargetAsync(targetId, HidReports.Keyboard(0, []), cancellationToken)) sent++;
        if (_mouse is not null && await NotifyTargetAsync(targetId, HidReports.Mouse(0), cancellationToken)) sent++;
        return sent;
    }
    public void Stop()
    {
        _enabled = false; StopAdvertisingSafely(); Status = L.T("停止中"); InvalidateReadiness();
    }
    private void StopAdvertisingSafely()
    {
        if (Interlocked.Exchange(ref _advertisingIssued, 0) == 0 || _provider is null) return;
        try
        {
            if (_provider.AdvertisementStatus == GattServiceProviderAdvertisementStatus.Started)
                _provider.StopAdvertising();
        }
        catch (Exception ex) { Log?.Invoke(L.T("広告停止時の状態確認: ") + ex.Message); }
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _enabled = false; _disposed = true; _stop.Cancel();
        await _lifecycle.WaitAsync();
        if (_provider is not null) { _provider.AdvertisementStatusChanged -= AdvertisingChanged; StopAdvertisingSafely(); }
        if (_radio is not null) _radio.StateChanged -= RadioChanged;
        await _refresh.WaitAsync();
        try
        {
            foreach (var session in _sessions.Keys) session.SessionStatusChanged -= SessionChanged;
            foreach (var device in _devices.Values) { device.ConnectionStatusChanged -= DeviceChanged; device.Dispose(); }
            foreach (var characteristic in _characteristics) characteristic.SubscribedClientsChanged -= SubscribersChanged;
            _devices.Clear(); _sessions.Clear(); _characteristics.Clear(); _keyboard = _mouse = null; _provider = null;
        }
        finally { _refresh.Release(); _lifecycle.Release(); }
    }
}
