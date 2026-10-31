using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using UniversalControl.Core;
using UniversalControl.Windows;

namespace UniversalControl.App;

public partial class MainWindow
{
    private readonly HttpClient _updateHttp = new() { Timeout = TimeSpan.FromMinutes(10) };
    private readonly CancellationTokenSource _updateStop = new();
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromHours(6) };
    private ReleaseUpdate? _availableUpdate;
    private bool _checkingUpdate, _installingUpdate;
    private TitleBarTheme? _titleBarTheme;
    public void ShowUpdateRollback() => UpdateStatus.Text = L.T("更新版を起動できなかったため、元のexeに戻しました。");
    private void ApplyTitleBarTheme()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != 0) _titleBarTheme = NativeWindowTheme.Apply(handle, Controller.Settings.Theme == "Dark", SystemParameters.HighContrast);
    }
    private async Task StartUpdatesAsync()
    {
        UpdateStatus.Text = L.T("起動時に更新を確認します。更新確認はいつでもオフにできます。");
        if (Controller.SmokeMode) { CheckUpdateButton.IsEnabled = false; return; }
        SelfUpdateRunner.CleanCache(_dataDirectory);
        _updateTimer.Tick += async (_, _) => await CheckUpdatesAsync(false); _updateTimer.Start();
        await CheckUpdatesAsync(false);
    }
    private void SaveUpdatePreference(object sender, RoutedEventArgs e)
    {
        if (_updating) return;
        try
        {
            Controller.Save(Controller.Settings with { AutoCheckUpdates = AutoUpdateCheck.IsChecked == true });
            if (!Controller.Settings.AutoCheckUpdates) UpdateStatus.Text = L.T("自動確認はオフです。「更新を確認」から手動で確認できます。");
            else
            {
                UpdateStatus.Text = L.T("起動時に更新を確認します。更新確認はいつでもオフにできます。");
                if (!Controller.SmokeMode) _ = CheckUpdatesAsync(true);
            }
        }
        catch (Exception ex) { UpdateStatus.Text = L.T("更新設定を保存できませんでした: ") + ex.Message; LoadControls(); }
    }
    private async void CheckUpdateClicked(object sender, RoutedEventArgs e) => await CheckUpdatesAsync(true);
    private async Task CheckUpdatesAsync(bool manual)
    {
        if (_closing || Controller.SmokeMode || _checkingUpdate || _installingUpdate) return;
        if (!manual && (!Controller.Settings.AutoCheckUpdates || DateTimeOffset.UtcNow - Controller.Settings.LastUpdateCheckUtc < TimeSpan.FromHours(24))) return;
        _checkingUpdate = true; CheckUpdateButton.IsEnabled = false;
        UpdateStatus.Text = L.T("更新を確認しています…");
        try
        {
            // Independent of the HID heartbeat; a slow network must never stall connection monitoring.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_updateStop.Token); timeout.CancelAfter(TimeSpan.FromSeconds(45));
            var client = new ReleaseUpdateClient(_updateHttp);
            var version = new Version(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!.ToString(3));
            _availableUpdate = await client.CheckAsync(version, timeout.Token);
            if (_closing) return;
            Controller.Save(Controller.Settings with { LastUpdateCheckUtc = DateTimeOffset.UtcNow });
            UpdateStatus.Text = _availableUpdate is null ? L.F("最新の状態です（{0}）。", version) : L.F("{0}へ更新できます。", _availableUpdate.Manifest.Tag);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or OperationCanceledException or InvalidOperationException or FormatException or KeyNotFoundException)
        { if (!_closing) UpdateStatus.Text = L.T("更新を確認できませんでした。時間をおいて再試行してください。") + " " + ex.Message; }
        finally
        {
            _checkingUpdate = false;
            if (!_closing) { CheckUpdateButton.IsEnabled = true; InstallUpdateButton.IsEnabled = _availableUpdate is not null; }
        }
    }
    private async void InstallUpdateClicked(object sender, RoutedEventArgs e)
    {
        if (Controller.SmokeMode || _closing || _checkingUpdate || _installingUpdate || _availableUpdate is not { } update) return;
        _installingUpdate = true; InstallUpdateButton.IsEnabled = CheckUpdateButton.IsEnabled = false;
        try
        {
            var stage = SelfUpdateRunner.CreateStage(_dataDirectory);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_updateStop.Token); timeout.CancelAfter(TimeSpan.FromMinutes(10));
            var transfer = new Progress<UpdateTransferProgress>(value => UpdateStatus.Text = L.F("更新をダウンロードしています… {0:P0} · {1:F1}/{2:F1} MiB · {3:F1} MiB/s",
                value.Fraction, value.ReceivedBytes / 1048576d, value.TotalBytes / 1048576d, value.BytesPerSecond / 1048576d));
            await Task.Run(() => new ReleaseUpdateClient(_updateHttp).DownloadAsync(update, System.IO.Path.Combine(stage, "Payload.exe"),
                null, timeout.Token, transfer), timeout.Token);
            UpdateStatus.Text = L.T("更新を適用して再起動します…");
            Controller.Router.GoLocal();
            await StopClipboardAsync();
            await Controller.StopAsync(); // Return locally and release HID before the process exits.
            await SelfUpdateRunner.LaunchHelperAsync(stage, _dataDirectory, update.Manifest);
            RequestExit();
        }
        catch (Exception ex)
        { if (!_closing) UpdateStatus.Text = L.T("更新できませんでした。現在のexeは引き続き利用できます。") + " " + ex.Message; }
        finally
        {
            _installingUpdate = false;
            if (!_closing) { CheckUpdateButton.IsEnabled = true; InstallUpdateButton.IsEnabled = _availableUpdate is not null; }
        }
    }
    private void OpenReleases(object sender, RoutedEventArgs e)
    { if (!Controller.SmokeMode) Process.Start(new ProcessStartInfo(ReleaseUpdateClient.Repository + "/releases/latest") { UseShellExecute = true }); }
}
