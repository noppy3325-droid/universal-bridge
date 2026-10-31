using System.Text.Json;
using System.Windows;
using UniversalControl.Windows;
using WpfApplication = System.Windows.Application;

namespace UniversalControl.App;

public partial class App : WpfApplication
{
    private Mutex? _instance;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var dataIndex = Array.IndexOf(e.Args, "--data-dir");
        var dataDirectory = dataIndex >= 0 && dataIndex + 1 < e.Args.Length ? System.IO.Path.GetFullPath(e.Args[dataIndex + 1])
            : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalBridge");
        try
        {
            if (e.Args.Length == 2 && e.Args[0] == "--apply-update") { Shutdown(await SelfUpdateRunner.ApplyAsync(e.Args[1])); return; }
            if (e.Args.Length == 2 && e.Args[0].StartsWith("--update-fixture-", StringComparison.Ordinal))
            { Shutdown(await SelfUpdateVerification.FixtureAsync(e.Args[0], e.Args[1])); return; }
            if (e.Args.Contains("--verify-self-update")) { Shutdown(await SelfUpdateVerification.VerifyAsync(dataDirectory)); return; }
            if (e.Args.Contains("--verify-clipboard")) { Shutdown(await ClipboardVerification.RunAsync(dataDirectory)); return; }
            L.SetLanguage(new SettingsStore(dataDirectory).Load().Language);
            if (e.Args.FirstOrDefault(a => a is "--verify-update-feed" or "--verify-edge-return" or "--verify-pointer-precision" or "--verify-edge-input" or "--verify-motion-input" or "--verify-motion-timer" or "--verify-server" or "--verify-local-input" or "--hold-local-input" or "--verify-input-readiness" or "--probe-touchpad") is { } verification)
            { Shutdown(await HeadlessVerification.RunAsync(verification, dataDirectory)); return; }
            if (e.Args.Contains("--diagnose") || e.Args.Contains("--probe-gatt"))
            {
                Directory.CreateDirectory(dataDirectory);
                var probe = await BleHidTransport.ProbeAsync(e.Args.Contains("--probe-gatt"));
                await File.WriteAllTextAsync(System.IO.Path.Combine(dataDirectory, "adapter-probe.json"), JsonSerializer.Serialize(probe, new JsonSerializerOptions { WriteIndented = true }));
                Shutdown(probe.Error is null ? 0 : 2); return;
            }
            var smoke = e.Args.Contains("--smoke-test");
            _instance = new Mutex(true, smoke ? @"Local\UniversalBridge-Smoke-" + Guid.NewGuid() : @"Local\UniversalBridge-0F741DCC-2D65-4E50-A8B1-9F8DC8D214A7", out var first);
            if (!first) { System.Windows.MessageBox.Show(L.T("Universal Bridgeは起動済みです。タスクトレイから開いてください。"), "Universal Bridge"); Shutdown(1); return; }
            var window = new MainWindow(dataDirectory, e.Args.Contains("--smoke-test") || e.Args.Contains("--preview"), e.Args.Contains("--smoke-test")); MainWindow = window;
            if (smoke) { window.ShowActivated = false; window.IsHitTestVisible = false; }
            DispatcherUnhandledException += (_, args) => { window.Controller.NotifyError(args.Exception); args.Handled = true; };
            var updated = Array.IndexOf(e.Args, "--updated");
            if (updated >= 0 && updated + 1 < e.Args.Length) window.Loaded += async (_, _) => await SelfUpdateRunner.ConfirmStartupAsync(e.Args[updated + 1]);
            if (e.Args.Contains("--update-rollback")) window.Loaded += (_, _) => window.ShowUpdateRollback();
            window.Show();
            if (e.Args.Contains("--background")) { window.Hide(); await window.Controller.StartAsync(); }
        }
        catch (Exception ex) { System.Windows.MessageBox.Show(ex.ToString(), L.T("Universal Bridge 起動エラー")); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e) { _instance?.Dispose(); base.OnExit(e); }
}
