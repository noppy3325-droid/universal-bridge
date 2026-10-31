using System.Windows;
using UniversalControl.Core;

namespace UniversalControl.App;

public partial class MainWindow
{
    private ClipboardBridgeServer? _clipboardBridge;
    private bool _clipboardChanging;
    private int _clipboardEpoch;
    private string? _clipboardError;
    private void LoadClipboardAddresses()
    {
        var previous = (ClipboardAddressCombo.SelectedItem as ClipboardDesktop.AddressChoice)?.Address;
        var choices = ClipboardDesktop.Addresses(); ClipboardAddressCombo.ItemsSource = choices;
        ClipboardAddressCombo.SelectedItem = choices.FirstOrDefault(c => c.Address.Equals(previous)) ?? choices.FirstOrDefault();
        RefreshClipboardControls();
    }
    private void RefreshClipboardControls()
    {
        var active = _clipboardBridge is not null;
        ClipboardStartButton.IsEnabled = !active && !_clipboardChanging && ClipboardAddressCombo.SelectedItem is not null && !Controller.SmokeMode;
        ClipboardStopButton.IsEnabled = active && !_clipboardChanging;
        ClipboardCodeButton.IsEnabled = active && !_clipboardChanging;
        ClipboardAddressCombo.IsEnabled = !active && !_clipboardChanging;
        ClipboardRefreshButton.IsEnabled = !active && !_clipboardChanging;
        ClipboardCopySetup.IsEnabled = active; ClipboardCopyShare.IsEnabled = active;
        ClipboardSetupUrl.Text = _clipboardBridge?.SetupUrl ?? "—";
        ClipboardShareUrl.Text = _clipboardBridge is { } bridge ? bridge.Origin + "/" : "—";
        ClipboardCode.Text = _clipboardBridge?.PairingCode is { Length: > 0 } code ? code : "— — — — — —";
        ClipboardIdentityName.Text = _clipboardBridge?.AuthorityName ?? "—";
        ClipboardFingerprint.Text = _clipboardBridge?.CertificateFingerprint ?? "—";
        ClipboardStatus.Text = _clipboardError is { } error ? L.T(error) : active ? L.T(_clipboardBridge!.Paired ? "接続を許可済み。共有画面のボタンで受け渡せます。" : "共有中。iPadのSafariで接続してください。") : L.T("共有は停止しています。開始するまでクリップボードへアクセスしません。");
    }
    private async void StartClipboard(object sender, RoutedEventArgs e)
    {
        if (_clipboardChanging || _clipboardBridge is not null || Controller.SmokeMode || ClipboardAddressCombo.SelectedItem is not ClipboardDesktop.AddressChoice selected) return;
        _clipboardChanging = true; _clipboardError = null; RefreshClipboardControls();
        var epoch = ++_clipboardEpoch;
        try
        {
            using var authority = await Task.Run(() => ClipboardDesktop.LoadAuthority(_dataDirectory));
            if (_closing || epoch != _clipboardEpoch) return;
            _clipboardBridge = new(selected.Address, authority, token => ClipboardDesktop.ReadAsync(Dispatcher, token),
                (text, token) => ClipboardDesktop.WriteAsync(text, Dispatcher, token), L.Language);
        }
        catch (Exception) { _clipboardError = "共有を開始できません。選択したネットワークとポートの使用状況を確認してください。"; }
        finally
        {
            _clipboardChanging = false;
            RefreshClipboardControls();
        }
    }
    private async Task StopClipboardAsync()
    {
        ++_clipboardEpoch;
        _clipboardError = null;
        var bridge = _clipboardBridge; _clipboardBridge = null;
        if (bridge is not null) await bridge.DisposeAsync();
        if (!_closing) RefreshClipboardControls();
    }
    private async void StopClipboard(object sender, RoutedEventArgs e) => await StopClipboardAsync();
    private async void RefreshClipboardCode(object sender, RoutedEventArgs e)
    {
        if (_clipboardBridge is not { } bridge || _clipboardChanging) return;
        _clipboardChanging = true; RefreshClipboardControls();
        try { await bridge.RefreshPairingAsync(); }
        catch (OperationCanceledException) { }
        finally { _clipboardChanging = false; RefreshClipboardControls(); }
    }
    private void RefreshClipboardAddresses(object sender, RoutedEventArgs e) => LoadClipboardAddresses();
    private void ClipboardAddressChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    { if (Controller is not null && ClipboardStartButton is not null) RefreshClipboardControls(); }
    private async void CopyClipboardLink(object sender, RoutedEventArgs e)
    {
        if (_clipboardBridge is not { } bridge || Controller.SmokeMode) return;
        try { await ClipboardDesktop.WriteAsync(ReferenceEquals(sender, ClipboardCopySetup) ? bridge.SetupUrl : bridge.Origin + "/", Dispatcher, CancellationToken.None); }
        catch (System.Runtime.InteropServices.ExternalException) { ClipboardStatus.Text = L.T("Windowsのクリップボードを使用できません。少し待って再試行してください。"); }
    }
}
