using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using UniversalControl.Core;
using UniversalControl.Windows;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Cursors = System.Windows.Input.Cursors;
using Point = System.Windows.Point;
using WpfApplication = System.Windows.Application;

namespace UniversalControl.App;

public partial class MainWindow : Window
{
    private sealed record ModifierChoice(ShortcutModifiers Value, string Label) { public override string ToString() => Label; }
    private sealed record KeyChoice(int Value, string Label) { public override string ToString() => Label; }
    private sealed record GestureChoice(GestureAction Value, string Label) { public override string ToString() => Label; }
    private sealed record LanguageChoice(string Code, string Label) { public override string ToString() => Label; }
    private static readonly ModifierChoice[] ModifierChoices = [new(ShortcutModifiers.Control | ShortcutModifiers.Alt, "Ctrl + Alt"), new(ShortcutModifiers.Control | ShortcutModifiers.Shift, "Ctrl + Shift"), new(ShortcutModifiers.Alt | ShortcutModifiers.Shift, "Alt + Shift"), new(ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Shift, "Ctrl + Alt + Shift"), new(ShortcutModifiers.Control, "Ctrl"), new(ShortcutModifiers.Alt, "Alt")];
    private static readonly KeyChoice[] KeyChoices = Enumerable.Range(0x41, 26).Concat(Enumerable.Range(0x30, 10)).Concat(Enumerable.Range(0x70, 24)).Select(v => new KeyChoice(v, new ShortcutBinding(ShortcutModifiers.Control, v).KeyLabel)).ToArray();
    private static GestureChoice[] GestureChoices => [new(GestureAction.None, L.T("操作しない（Windowsへも渡さない）")), new(GestureAction.NextApp, L.T("次のアプリ（⌘Tab）")), new(GestureAction.PreviousApp, L.T("前のアプリ（⇧⌘Tab）")), new(GestureAction.Home, L.T("ホーム（⌘H、対応するiPadOS）")), new(GestureAction.Search, L.T("検索（⌘Space）")), new(GestureAction.ZoomIn, L.T("拡大（⇧⌘=、対応アプリ）")), new(GestureAction.ZoomOut, L.T("縮小（⌘−、対応アプリ）"))];
    public BridgeController Controller { get; }
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly System.Windows.Forms.NotifyIcon _tray;
    private readonly System.Drawing.Icon _trayIcon;
    private readonly string _dataDirectory;
    private bool _updating = true, _exitRequested, _closing, _ticking, _trayNotice, _dragging;
    private Rect _sourcePreview;
    private FrameworkElement[] _pages = [];
    private Button[] _nav = [];

    public MainWindow(string dataDirectory, bool smokeMode, bool smokeAuto)
    {
        L.SetLanguage(new SettingsStore(dataDirectory).Load().Language);
        InitializeComponent(); _dataDirectory = dataDirectory;
        Controller = new(dataDirectory, smokeMode);
        _pages = [DashboardPage, LayoutPage, InputPage, SettingsPage]; _nav = [DashboardNav, LayoutNav, InputNav, SettingsNav];
        using (var iconStream = WpfApplication.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico")).Stream)
        using (var icon = new System.Drawing.Icon(iconStream, 32, 32)) _trayIcon = (System.Drawing.Icon)icon.Clone();
        _tray = new System.Windows.Forms.NotifyIcon { Icon = _trayIcon, Text = "Universal Bridge · Windows", Visible = !smokeMode };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(L.T("開く"), null, (_, _) => Dispatcher.Invoke(ShowWindow));
        menu.Items.Add(L.T("Windowsへ戻す"), null, (_, _) => Controller.Router.GoLocal());
        menu.Items.Add(L.T("終了"), null, (_, _) => Dispatcher.Invoke(RequestExit));
        _tray.ContextMenuStrip = menu; _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowWindow);
        Controller.Changed += ControllerChanged;
        ApplyLocalization(); LoadControls(); SelectPage(0); ApplyTheme(); RefreshView(); _updating = false;
        _timer.Tick += async (_, _) =>
        {
            if (_ticking || _closing) return; _ticking = true;
            try { await Controller.TickAsync(); RefreshView(); }
            catch (Exception ex) { Controller.NotifyError(ex); }
            finally { _ticking = false; }
        };
        _timer.Start();
        SystemEvents.SessionSwitch += SessionChanged; SystemEvents.PowerModeChanged += PowerChanged; SystemEvents.DisplaySettingsChanged += DisplaysChanged;
        Loaded += async (_, _) =>
        {
            MinHeight = Math.Min(MinHeight, SystemParameters.WorkArea.Height);
            Height = Math.Min(Height, SystemParameters.WorkArea.Height);
            Top = Math.Max(SystemParameters.WorkArea.Top, Top);
            DrawLayout();
            if (smokeAuto) await RunSmokeAsync();
        };
    }
    private void ControllerChanged()
    { if (!_closing && !Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(RefreshView); }
    private void SessionChanged(object sender, SessionSwitchEventArgs e) => Controller.Router.GoLocal(L.T("セッション変更によりWindowsへ戻しました"));
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Suspend) Controller.Router.GoLocal(L.T("スリープに入るためWindowsへ戻しました")); }
    private void DisplaysChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() =>
    { try { Controller.RefreshDisplays(); LoadControls(); DrawLayout(); } catch (Exception ex) { Controller.NotifyError(ex); } });
    private void ShowWindow() { Show(); WindowState = WindowState.Normal; Activate(); }
    private void RequestExit() { _exitRequested = true; Close(); }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closing) return;
        e.Cancel = true;
        if (!_exitRequested && Controller.Settings.CloseToTray && !Controller.SmokeMode)
        {
            Hide();
            if (!_trayNotice) { _tray.ShowBalloonTip(2500, "Universal Bridge", L.T("タスクトレイで動作を続けます。終了はトレイメニューから行えます。"), System.Windows.Forms.ToolTipIcon.Info); _trayNotice = true; }
            return;
        }
        _closing = true; _timer.Stop();
        SystemEvents.SessionSwitch -= SessionChanged; SystemEvents.PowerModeChanged -= PowerChanged; SystemEvents.DisplaySettingsChanged -= DisplaysChanged;
        Controller.Changed -= ControllerChanged;
        try { await Controller.DisposeAsync(); }
        finally { _tray.Visible = false; _tray.Dispose(); _trayIcon.Dispose(); WpfApplication.Current.Shutdown(); }
    }
    private void LoadControls()
    {
        var prior = _updating; _updating = true;
        var s = Controller.Settings;
        LanguageCombo.ItemsSource = new[] { new LanguageChoice("auto", L.T("システムの言語")), new LanguageChoice("ja", "日本語"), new LanguageChoice("en", "English"), new LanguageChoice("zh-Hans", "简体中文") };
        LanguageCombo.SelectedItem = LanguageCombo.Items.Cast<LanguageChoice>().First(c => c.Code == s.Language);
        MonitorCombo.ItemsSource = Controller.Displays;
        MonitorCombo.SelectedItem = Controller.Displays.FirstOrDefault(d => d.Id == s.MonitorId) ?? Controller.Displays.FirstOrDefault();
        EdgeCombo.SelectedIndex = (int)s.Edge;
        AutoSwitchCheck.IsChecked = s.AutoSwitch; PortalStartSlider.Value = s.PortalStart; PortalEndSlider.Value = s.PortalEnd;
        MouseSlider.Value = s.MouseSensitivity; ScrollSlider.Value = s.ScrollSensitivity; EstimatedCheck.IsChecked = s.EstimatedReturn;
        TouchpadScrollSlider.Value = s.TouchpadScrollSensitivity;
        TouchpadSensitivityBox.Text = s.TouchpadScrollSensitivity.ToString("0.00", CultureInfo.InvariantCulture);
        MouseSensitivityBox.Text = s.MouseSensitivity.ToString("0.00", CultureInfo.InvariantCulture); RawMouseCheck.IsChecked = s.RawMouse;
        TouchpadCheck.IsChecked = s.CaptureTouchpad;
        LoadShortcutControls(s.ActivateShortcut, s.ReturnShortcut);
        var gestureChoices = GestureChoices;
        foreach (var pair in new[] { (SwipeLeftCombo, s.SwipeLeft), (SwipeRightCombo, s.SwipeRight), (SwipeUpCombo, s.SwipeUp), (SwipeDownCombo, s.SwipeDown) })
        { pair.Item1.ItemsSource = gestureChoices; pair.Item1.SelectedItem = gestureChoices.First(g => g.Value == pair.Item2); }
        TrayCheck.IsChecked = s.CloseToTray; ReconnectCheck.IsChecked = s.Reconnect; AutostartCheck.IsChecked = s.AutoStart;
        AutostartCheck.IsEnabled = !Controller.SmokeMode;
        DataPathLabel.Text = _dataDirectory;
        _updating = prior;
    }
    private void RefreshView()
    {
        if (_closing) return;
        var prior = _updating; _updating = true;
        try
        {
            var peers = Controller.Transport.Peers(); var selected = peers.FirstOrDefault(p => p.Id == Controller.SelectedPeerId);
            PeerCombo.ItemsSource = peers; PeerCombo.SelectedItem = selected;
            BluetoothStatus.Text = Controller.Transport.Publishing ? L.T("公開中") : Controller.Busy ? L.T("開始中…") : L.T("停止中");
            AdapterStatus.Text = L.T(Controller.Transport.AdapterSummary);
            PeerStatus.Text = selected?.Name ?? L.T("未選択");
            SubscriptionStatus.Text = selected is null ? L.T("一覧から端末を選択") : L.F("キーボード {0} / マウス {1} / {2}", selected.Keyboard ? "✓" : "—", selected.Mouse ? "✓" : "—", selected.Paired ? L.T("ペア済") : L.T("未ペア")) + (selected.Suspended ? L.T(" / iPadで一時停止") : "");
            InputTarget.Text = Controller.Router.IsRemote ? L.T("iPad / 選択端末") : "Windows";
            InputReason.Text = Controller.Router.Reason;
            StartButton.IsEnabled = !Controller.Busy && !Controller.Transport.Publishing && !Controller.SmokeMode;
            StopButton.IsEnabled = !Controller.Busy && Controller.Transport.Publishing;
            SwitchButton.IsEnabled = selected is not null && Controller.Transport.IsReady(selected.Id) && !Controller.Busy;
            MessageText.Text = Controller.Message;
            _tray.Text = "Universal Bridge · " + (Controller.Router.IsRemote ? "iPad" : "Windows");
            MouseValue.Text = $"{Controller.Settings.MouseSensitivity:0.00} ×"; ScrollValue.Text = $"{Controller.Settings.ScrollSensitivity:0.00} ×";
            MouseModeLabel.Text = Controller.MouseMode; ActivationShortcutLabel.Text = Controller.ActivationShortcut;
            DashboardReturnShortcut.Text = Controller.ReturnShortcutText; TouchpadStatusLabel.Text = Controller.TouchpadStatus;
            PortalLabel.Text = L.F("画面端の {0:P0} ～ {1:P0} ／ {2} ms の押し続け", Controller.Settings.PortalStart, Controller.Settings.PortalEnd, Controller.Settings.EdgeDwellMs);
            ReportStats.Text = L.F("送信済みレポート: {0:N0}  ·  送信処理: {1}  ·  メモリ: {2:0.0} MB", Controller.Reports.Sent, Controller.Reports.Healthy ? L.T("正常") : L.T("停止（アプリを再起動）"), Process.GetCurrentProcess().WorkingSet64 / 1048576d);
            if (SettingsPage.Visibility == Visibility.Visible) { LogBox.Text = Controller.Log.Recent; LogBox.ScrollToEnd(); }
        }
        finally { _updating = prior; }
    }
    private void Navigate(object sender, RoutedEventArgs e)
    { if (sender is Button button) SelectPage(Array.FindIndex(_pages, page => page.Name == (string)button.Tag)); }
    private void SelectPage(int index)
    {
        for (var i = 0; i < _pages.Length; i++)
        {
            _pages[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
            _nav[i].SetResourceReference(BackgroundProperty, i == index ? "AccentSoft" : "SidebarBg");
            _nav[i].SetResourceReference(ForegroundProperty, i == index ? "Accent" : "Muted");
            _nav[i].FontWeight = i == index ? FontWeights.SemiBold : FontWeights.Normal;
        }
        PageScroll.ScrollToTop();
        if (index == 1) Dispatcher.BeginInvoke(DrawLayout);
    }
    private void ApplyTheme()
    {
        var dark = Controller.Settings.Theme == "Dark";
        var palette = dark ? new[] { "#161C29", "#111723", "#202938", "#364156", "#EEF3FC", "#A8B6CD", "#7FA8FF", "#263B61" }
            : new[] { "#F4F6FA", "#EBEFF5", "#FFFFFF", "#DDE3ED", "#19243A", "#657187", "#3167DA", "#E7EEFD" };
        var keys = new[] { "PageBg", "SidebarBg", "Surface", "Line", "Ink", "Muted", "Accent", "AccentSoft" };
        for (var i = 0; i < keys.Length; i++) WpfApplication.Current.Resources[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(palette[i]));
        ThemeButton.Content = dark ? L.T("ライトモード") : L.T("ダークモード");
        DrawLayout();
    }
    private void ApplyLocalization()
    {
        foreach (var pair in L.UiSources) Resources[pair.Key] = L.T(pair.Value);
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(L.Language);
        if (_tray?.ContextMenuStrip is { } menu)
        { menu.Items[0].Text = L.T("開く"); menu.Items[1].Text = L.T("Windowsへ戻す"); menu.Items[2].Text = L.T("終了"); }
        ShortcutStatusLabel.Text = "";
    }
    private void ChangeLanguage(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || LanguageCombo.SelectedItem is not LanguageChoice choice || choice.Code == Controller.Settings.Language) return;
        var previous = Controller.Settings.Language;
        try
        {
            Controller.Save(Controller.Settings with { Language = choice.Code });
            L.SetLanguage(choice.Code);
            Controller.Router.GoLocal(L.T("表示言語を変更したためWindowsへ戻しました"));
            ApplyLocalization(); LoadControls(); ApplyTheme(); RefreshView();
        }
        catch (Exception ex) { L.SetLanguage(previous); Controller.NotifyError(ex); LoadControls(); }
    }
    private void ToggleTheme(object sender, RoutedEventArgs e)
    { try { Controller.Save(Controller.Settings with { Theme = Controller.Settings.Theme == "Dark" ? "Light" : "Dark" }); ApplyTheme(); } catch (Exception ex) { Controller.NotifyError(ex); } }
    private async void StartHid(object sender, RoutedEventArgs e) => await Controller.StartAsync();
    private async void StopHid(object sender, RoutedEventArgs e) { try { await Controller.StopAsync(); } catch (Exception ex) { Controller.NotifyError(ex); } }
    private async void SwitchInput(object sender, RoutedEventArgs e) { try { await Controller.SwitchRemoteAsync(); } catch (Exception ex) { Controller.NotifyError(ex); } }
    private void ReturnLocal(object sender, RoutedEventArgs e) => Controller.Router.GoLocal();
    private void ExitApplication(object sender, RoutedEventArgs e) => RequestExit();
    private void PreventOuterLogScroll(object sender, RequestBringIntoViewEventArgs e) => e.Handled = true;
    private void OpenBluetooth(object sender, RoutedEventArgs e)
    { try { Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true }); } catch (Exception ex) { Controller.NotifyError(ex); } }
    private void PeerChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating) return;
        Controller.Router.GoLocal(L.T("端末の選択を変更しました")); Controller.SelectedPeerId = (PeerCombo.SelectedItem as PeerInfo)?.Id; RefreshView();
    }
    private void SaveControls(object sender, RoutedEventArgs e)
    {
        if (_updating) return;
        try
        {
            Controller.Save(Controller.Settings with
            {
                AutoSwitch = AutoSwitchCheck.IsChecked == true, EstimatedReturn = EstimatedCheck.IsChecked == true,
                MouseSensitivity = MouseSlider.Value, ScrollSensitivity = ScrollSlider.Value,
                TouchpadScrollSensitivity = TouchpadScrollSlider.Value,
                RawMouse = RawMouseCheck.IsChecked == true,
                CaptureTouchpad = TouchpadCheck.IsChecked == true,
                CloseToTray = TrayCheck.IsChecked == true, Reconnect = ReconnectCheck.IsChecked == true, AutoStart = AutostartCheck.IsChecked == true,
                Edge = (ScreenEdge)Math.Max(0, EdgeCombo.SelectedIndex), MonitorId = (MonitorCombo.SelectedItem as DisplayInfo)?.Id ?? "",
                PortalStart = PortalStartSlider.Value, PortalEnd = PortalEndSlider.Value
            });
            var prior = _updating; _updating = true;
            PortalStartSlider.Value = Controller.Settings.PortalStart; PortalEndSlider.Value = Controller.Settings.PortalEnd;
            MouseSensitivityBox.Text = Controller.Settings.MouseSensitivity.ToString("0.00", CultureInfo.InvariantCulture);
            TouchpadSensitivityBox.Text = Controller.Settings.TouchpadScrollSensitivity.ToString("0.00", CultureInfo.InvariantCulture);
            _updating = prior;
            RefreshView(); DrawLayout();
        }
        catch (Exception ex) { Controller.NotifyError(ex); LoadControls(); }
    }
    private void LayoutChanged(object sender, SelectionChangedEventArgs e) => SaveControls(sender, e);
    private void LayoutSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => SaveControls(sender, e);
    private void InputSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => SaveControls(sender, e);
    private void LoadShortcutControls(ShortcutBinding activate, ShortcutBinding back)
    {
        ActivateModsCombo.ItemsSource = ReturnModsCombo.ItemsSource = ModifierChoices;
        ActivateKeyCombo.ItemsSource = ReturnKeyCombo.ItemsSource = KeyChoices;
        ActivateModsCombo.SelectedItem = ModifierChoices.First(m => m.Value == activate.Modifiers); ActivateKeyCombo.SelectedItem = KeyChoices.First(k => k.Value == activate.Key);
        ReturnModsCombo.SelectedItem = ModifierChoices.First(m => m.Value == back.Modifiers); ReturnKeyCombo.SelectedItem = KeyChoices.First(k => k.Value == back.Key);
    }
    private void CommitTouchpadSensitivity(object sender, KeyboardFocusChangedEventArgs e) => ApplyTouchpadSensitivityText();
    private void TouchpadSensitivityKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    { if (e.Key == Key.Enter) { ApplyTouchpadSensitivityText(); e.Handled = true; } }
    private void ApplyTouchpadSensitivityText()
    {
        if (_updating) return;
        if ((double.TryParse(TouchpadSensitivityBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || double.TryParse(TouchpadSensitivityBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) && double.IsFinite(value))
            SetTouchpadSensitivity(value);
        else TouchpadSensitivityBox.Text = Controller.Settings.TouchpadScrollSensitivity.ToString("0.00", CultureInfo.InvariantCulture);
    }
    private void StepTouchpadSensitivity(object sender, RoutedEventArgs e)
    { if (sender is Button button) SetTouchpadSensitivity(Controller.Settings.TouchpadScrollSensitivity + double.Parse((string)button.Tag, CultureInfo.InvariantCulture)); }
    private void ResetTouchpadSensitivity(object sender, RoutedEventArgs e) => SetTouchpadSensitivity(3);
    private void SetTouchpadSensitivity(double value)
    {
        try
        {
            Controller.Save(Controller.Settings with { TouchpadScrollSensitivity = value });
            var previous = _updating; _updating = true;
            TouchpadScrollSlider.Value = Controller.Settings.TouchpadScrollSensitivity;
            TouchpadSensitivityBox.Text = Controller.Settings.TouchpadScrollSensitivity.ToString("0.00", CultureInfo.InvariantCulture);
            _updating = previous; RefreshView();
        }
        catch (Exception ex) { Controller.NotifyError(ex); }
    }
    private void DefaultShortcuts(object sender, RoutedEventArgs e)
    { LoadShortcutControls(ShortcutBinding.DefaultActivate, ShortcutBinding.DefaultReturn); ShortcutStatusLabel.Text = L.T("初期値を入力しました。「適用」で保存します。"); }
    private async void ApplyShortcuts(object sender, RoutedEventArgs e)
    {
        if (!ApplyShortcutsButton.IsEnabled) return;
        ApplyShortcutsButton.IsEnabled = false;
        try
        {
            if (ActivateModsCombo.SelectedItem is not ModifierChoice am || ActivateKeyCombo.SelectedItem is not KeyChoice ak || ReturnModsCombo.SelectedItem is not ModifierChoice rm || ReturnKeyCombo.SelectedItem is not KeyChoice rk)
                throw new ArgumentException(L.T("修飾キーとキーを選択してください。"));
            await Controller.ApplyShortcutsAsync(new(am.Value, ak.Value), new(rm.Value, rk.Value));
            ShortcutStatusLabel.Text = L.T("保存・適用しました。");
        }
        catch (Exception ex) { ShortcutStatusLabel.Text = ex.Message; }
        finally { ApplyShortcutsButton.IsEnabled = true; }
    }
    private void GestureMappingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating) return;
        try
        {
            Controller.Router.GoLocal(L.T("ジェスチャーの割り当てを変更したためWindowsへ戻しました"));
            Controller.Save(Controller.Settings with { SwipeLeft = (SwipeLeftCombo.SelectedItem as GestureChoice)?.Value ?? Controller.Settings.SwipeLeft,
                SwipeRight = (SwipeRightCombo.SelectedItem as GestureChoice)?.Value ?? Controller.Settings.SwipeRight,
                SwipeUp = (SwipeUpCombo.SelectedItem as GestureChoice)?.Value ?? Controller.Settings.SwipeUp,
                SwipeDown = (SwipeDownCombo.SelectedItem as GestureChoice)?.Value ?? Controller.Settings.SwipeDown });
        }
        catch (Exception ex) { Controller.NotifyError(ex); }
    }
    private void CommitMouseSensitivity(object sender, KeyboardFocusChangedEventArgs e) => ApplyMouseSensitivityText();
    private void MouseSensitivityKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    { if (e.Key == Key.Enter) { ApplyMouseSensitivityText(); e.Handled = true; } }
    private void ApplyMouseSensitivityText()
    {
        if (_updating) return;
        if ((double.TryParse(MouseSensitivityBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            double.TryParse(MouseSensitivityBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) && double.IsFinite(value))
            SetMouseSensitivity(value);
        else MouseSensitivityBox.Text = Controller.Settings.MouseSensitivity.ToString("0.00", CultureInfo.InvariantCulture);
    }
    private void StepMouseSensitivity(object sender, RoutedEventArgs e)
    { if (sender is Button button) SetMouseSensitivity(Controller.Settings.MouseSensitivity + double.Parse((string)button.Tag, CultureInfo.InvariantCulture)); }
    private void SetMouseSensitivity(double value)
    {
        try
        {
            Controller.Save(Controller.Settings with { MouseSensitivity = value });
            var prior = _updating; _updating = true;
            MouseSlider.Value = Controller.Settings.MouseSensitivity;
            MouseSensitivityBox.Text = Controller.Settings.MouseSensitivity.ToString("0.00", CultureInfo.InvariantCulture);
            _updating = prior; RefreshView();
        }
        catch (Exception ex) { Controller.NotifyError(ex); }
    }
    private void LayoutSizeChanged(object sender, SizeChangedEventArgs e) { if (!_updating) DrawLayout(); }

    private Border DiagramBox(string text, bool accent, double width, double height)
    {
        var border = new Border { Width = width, Height = height, CornerRadius = new(9), BorderThickness = new(accent ? 2 : 1), Padding = new(8) };
        border.SetResourceReference(Border.BackgroundProperty, "Surface"); border.SetResourceReference(Border.BorderBrushProperty, accent ? "Accent" : "Line");
        border.Child = new TextBlock { Text = text, FontSize = 11, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        return border;
    }
    private void DrawLayout()
    {
        if (Controller is null || _dragging || LayoutCanvas.ActualWidth < 200) return;
        LayoutCanvas.Children.Clear(); var displays = Controller.Displays; if (displays.Count == 0) return;
        var left = displays.Min(d => d.Left); var top = displays.Min(d => d.Top);
        var width = displays.Max(d => d.Right) - left; var height = displays.Max(d => d.Bottom) - top;
        var scale = Math.Min((LayoutCanvas.ActualWidth - 270) / width, 140d / height);
        var xBase = (LayoutCanvas.ActualWidth - width * scale) / 2; var yBase = (LayoutCanvas.ActualHeight - height * scale) / 2;
        var settings = Controller.Settings;
        var selected = displays.FirstOrDefault(d => d.Id == settings.MonitorId) ?? displays[0];
        foreach (var display in displays)
        {
            var w = display.Width * scale; var h = display.Height * scale;
            var box = DiagramBox($"Windows\n{display.Width} × {display.Height}", display.Id == selected.Id, w, h);
            var x = xBase + (display.Left - left) * scale; var y = yBase + (display.Top - top) * scale;
            Canvas.SetLeft(box, x); Canvas.SetTop(box, y); LayoutCanvas.Children.Add(box);
            if (display.Id == selected.Id) _sourcePreview = new(x, y, w, h);
        }
        var vertical = settings.Edge is ScreenEdge.Right or ScreenEdge.Left;
        var portal = new System.Windows.Shapes.Line { StrokeThickness = 4 };
        portal.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "Accent");
        if (vertical)
        {
            portal.X1 = portal.X2 = settings.Edge == ScreenEdge.Right ? _sourcePreview.Right : _sourcePreview.Left;
            portal.Y1 = _sourcePreview.Top + settings.PortalStart * _sourcePreview.Height; portal.Y2 = _sourcePreview.Top + settings.PortalEnd * _sourcePreview.Height;
        }
        else
        {
            portal.Y1 = portal.Y2 = settings.Edge == ScreenEdge.Top ? _sourcePreview.Top : _sourcePreview.Bottom;
            portal.X1 = _sourcePreview.Left + settings.PortalStart * _sourcePreview.Width; portal.X2 = _sourcePreview.Left + settings.PortalEnd * _sourcePreview.Width;
        }
        LayoutCanvas.Children.Add(portal);
        var ipad = DiagramBox(L.T("iPad\nドラッグして配置"), true, vertical ? 105 : 135, vertical ? 125 : 70);
        var middle = (settings.PortalStart + settings.PortalEnd) / 2;
        var px = settings.Edge switch { ScreenEdge.Right => _sourcePreview.Right + 18, ScreenEdge.Left => _sourcePreview.Left - ipad.Width - 18, _ => _sourcePreview.Left + _sourcePreview.Width * middle - ipad.Width / 2 };
        var py = settings.Edge switch { ScreenEdge.Top => _sourcePreview.Top - ipad.Height - 12, ScreenEdge.Bottom => _sourcePreview.Bottom + 12, _ => _sourcePreview.Top + _sourcePreview.Height * middle - ipad.Height / 2 };
        Canvas.SetLeft(ipad, px); Canvas.SetTop(ipad, py); LayoutCanvas.Children.Add(ipad); ipad.Cursor = Cursors.SizeAll;
        ipad.MouseLeftButtonDown += (_, e) => { _dragging = true; ipad.CaptureMouse(); e.Handled = true; };
        ipad.MouseMove += (_, e) =>
        {
            if (!_dragging) return; var point = e.GetPosition(LayoutCanvas);
            if (vertical) Canvas.SetTop(ipad, Math.Clamp(point.Y - ipad.Height / 2, 0, LayoutCanvas.ActualHeight - ipad.Height));
            else Canvas.SetLeft(ipad, Math.Clamp(point.X - ipad.Width / 2, 0, LayoutCanvas.ActualWidth - ipad.Width));
        };
        ipad.MouseLeftButtonUp += (_, e) =>
        {
            if (!_dragging) return; _dragging = false; ipad.ReleaseMouseCapture();
            var center = vertical ? (Canvas.GetTop(ipad) + ipad.Height / 2 - _sourcePreview.Top) / _sourcePreview.Height
                : (Canvas.GetLeft(ipad) + ipad.Width / 2 - _sourcePreview.Left) / _sourcePreview.Width;
            var start = Math.Clamp(center - .3, 0, .4);
            Controller.Save(settings with { PortalStart = start, PortalEnd = start + .6 }); LoadControls(); DrawLayout();
        };
    }
    private void ExportDiagnostics(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = "UniversalBridge-diagnostics.json", Filter = L.T("診断ファイル (*.json)|*.json") };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(new { Version = "0.3.3", Time = DateTimeOffset.Now, Controller.MouseMode, Controller.ActivationShortcut, Controller.ReturnShortcutText, Controller.TouchpadStatus, Controller.Transport.AdapterSummary,
                Controller.Transport.Status, Peers = Controller.Transport.Peers(), Controller.Reports.Sent, Controller.Reports.Healthy, Controller.Router.HeldState, Log = Controller.Log.Recent }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { Controller.NotifyError(ex); }
    }

    private async Task RunSmokeAsync()
    {
        var output = Path.Combine(_dataDirectory, "screenshots"); Directory.CreateDirectory(output);
        try
        {
            var languages = new[] { "ja", "en", "zh-Hans" }; var verified = new List<string>();
            foreach (var language in languages)
            {
                LanguageCombo.SelectedItem = LanguageCombo.Items.Cast<LanguageChoice>().First(c => c.Code == language);
                Controller.Save(Controller.Settings with { Theme = "Light" }); ApplyTheme();
                for (var index = 0; index < _pages.Length; index++)
                {
                    SelectPage(index); RefreshView(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    Capture(Path.Combine(output, $"{language}-{index + 1}-light.png"));
                }
                SelectPage(2);
                TouchpadSensitivityBox.Text = "4.50"; ApplyTouchpadSensitivityText();
                if (new SettingsStore(_dataDirectory).Load().TouchpadScrollSensitivity != 4.5 || TouchpadScrollSlider.Value != 4.5)
                    throw new InvalidOperationException("Touchpad sensitivity edit failed: " + language);
                ResetTouchpadSensitivity(this, new RoutedEventArgs());
                if (new SettingsStore(_dataDirectory).Load().TouchpadScrollSensitivity != 3 || TouchpadSensitivityBox.Text != "3.00")
                    throw new InvalidOperationException("Touchpad sensitivity reset failed: " + language);
                PageScroll.ScrollToVerticalOffset(280); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Capture(Path.Combine(output, $"{language}-3-scroll-light.png"));
                SelectPage(2); PageScroll.ScrollToEnd(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Capture(Path.Combine(output, $"{language}-3-shortcuts-light.png"));
                Controller.Save(Controller.Settings with { Theme = "Dark" }); ApplyTheme(); SelectPage(0); RefreshView();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); Capture(Path.Combine(output, $"{language}-1-dark.png"));
                var saved = new SettingsStore(_dataDirectory).Load();
                if (saved.Language != language || L.Language != language || (string)DashboardNav.Content != L.T("接続ダッシュボード") || (string)ApplyShortcutsButton.Content != L.T("ショートカットを適用") || SwipeLeftCombo.Items.Cast<GestureChoice>().First().Label != L.T("操作しない（Windowsへも渡さない）") || _tray.ContextMenuStrip!.Items[0].Text != L.T("開く"))
                    throw new InvalidOperationException("Language switch or persistence failed: " + language);
                verified.Add(language);
            }
            Controller.Save(Controller.Settings with { MouseSensitivity = 1.25, Theme = "Dark" }); LoadControls(); ApplyTheme(); SelectPage(0); RefreshView();
            var restored = new SettingsStore(_dataDirectory).Load();
            var iconsLoaded = Icon is not null && Icon.Width > 0 && _trayIcon.Width > 0;
            var ok = !Controller.Router.IsRemote && Controller.Reports.Healthy && restored.MouseSensitivity == 1.25 && restored.Theme == "Dark" && restored.TouchpadScrollSensitivity == 3 && !SwitchButton.IsEnabled && iconsLoaded;
            await File.WriteAllTextAsync(Path.Combine(_dataDirectory, "gui-smoke.json"), JsonSerializer.Serialize(new { Success = ok && verified.Count == 3, Languages = verified, Pages = _pages.Select(p => p.Name), Screenshots = 21, InputSuppressed = Controller.Router.IsRemote, IconsLoaded = iconsLoaded, TouchpadSensitivityRoundTrip = restored.TouchpadScrollSensitivity == 3, SettingsRoundTrip = restored.MouseSensitivity == 1.25, At = DateTimeOffset.Now }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { await File.WriteAllTextAsync(Path.Combine(_dataDirectory, "gui-smoke-error.txt"), ex.ToString()); }
        RequestExit();
    }
    private void Capture(string path)
    {
        UpdateLayout(); var surface = (FrameworkElement)Content;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth), (int)Math.Ceiling(surface.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(surface); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
