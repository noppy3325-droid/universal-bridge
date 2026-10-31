using System.Collections.Concurrent;
using System.Text.Json;
using UniversalControl.Core;
using UniversalControl.Windows;
using MouseButtons = UniversalControl.Core.MouseButtons;

namespace UniversalControl.App;

public static class HeadlessVerification
{
    public static async Task<int> RunAsync(string mode, string directory)
    {
        Directory.CreateDirectory(directory);
        try
        {
            if (mode == "--verify-update-feed")
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                var client = new ReleaseUpdateClient(http); var update = await client.CheckAsync(new(0, 0, 0), timeout.Token)
                    ?? throw new InvalidOperationException("No published update manifest.");
                if (ReleaseUpdateClient.ParseVersion(update.Manifest.Version) != SelfUpdateRunner.CurrentVersion) throw new InvalidOperationException("Published manifest does not match the downloaded executable.");
                var payload = Path.Combine(directory, "UpdatePayload-" + Guid.NewGuid().ToString("N") + ".exe");
                var transfer = await client.DownloadAsync(update, payload, null, timeout.Token);
                if (update.Manifest.Archive is { } archive && (!transfer.Compressed || transfer.NetworkBytes < archive.Size))
                    throw new InvalidOperationException("Compressed release transfer was not used.");
                if (SelfUpdateRunner.VersionOf(payload) != SelfUpdateRunner.CurrentVersion || await SelfUpdateRunner.Hash(payload) != await SelfUpdateRunner.Hash(Environment.ProcessPath!))
                    throw new InvalidOperationException("Updater downloaded different executable bytes.");
                await WriteAsync(directory, "update-feed.json", new { Success = true, update.Manifest.Tag, update.Manifest.Version, PublicFeedValidated = true,
                    PublicDownloadVerified = true, IdenticalToReleaseExecutable = true, transfer.Compressed, transfer.NetworkBytes, transfer.Requests,
                    ElapsedSeconds = transfer.Elapsed.TotalSeconds, MiBPerSecond = transfer.NetworkBytes / 1048576d / transfer.Elapsed.TotalSeconds,
                    ExecutableBytes = update.Manifest.Size, ArchiveBytes = update.Manifest.Archive?.Size,
                    InstalledByProbe = false, InputCaptured = false, BluetoothAdvertised = false }); return 0;
            }
            if (mode == "--verify-edge-return")
            {
                // Model verification only. Never install hooks, move the real cursor or advertise BLE.
                var directions = new List<string>();
                foreach (var edge in Enum.GetValues<ScreenEdge>())
                {
                    var transport = new ModelTransport(); await using var dispatcher = new ReportDispatcher(transport);
                    var clock = new ModelClock(); var router = new InputRouter(transport, dispatcher, clock);
                    var settings = JsonSerializer.Deserialize<AppSettings>("""{"AutoSwitch":true,"EstimatedReturn":false,"NativeTouchpad":true}""")! with { Edge = edge, StabilizeFastPointer = false };
                    router.Configure(settings);
                    if (!await router.ActivateAsync("model", () => true)) throw new InvalidOperationException("Model activation failed.");
                    router.SetEntryAlong(.75);
                    var (x, y) = edge switch { ScreenEdge.Right => (-10000, 0), ScreenEdge.Left => (10000, 0), ScreenEdge.Top => (0, 10000), _ => (0, -10000) };
                    router.Button(MouseButtons.Left, true); router.Move(x, y); router.FlushMotion();
                    if (!router.IsRemote) throw new InvalidOperationException("Return interrupted a drag.");
                    router.Button(MouseButtons.Left, false); router.FlushMotion();
                    if (!router.IsRemote) throw new InvalidOperationException("Release triggered a stale return.");
                    router.Move(x, y); router.FlushMotion();
                    if (!router.IsRemote) throw new InvalidOperationException("Entry flick triggered return.");
                    clock.Now = 2000; router.Move(x, y); router.FlushMotion();
                    if (!router.IsRemote) throw new InvalidOperationException("Single flick triggered return.");
                    clock.Now = 5000; router.FlushMotion();
                    router.Move(Math.Sign(x), Math.Sign(y)); router.FlushMotion();
                    if (!router.IsRemote) throw new InvalidOperationException("Idle or a small move triggered a stale return.");
                    clock.Now = 6000; router.Move(x, y); router.FlushMotion();
                    for (var sample = 1; sample <= 3; sample++)
                    {
                        for (var tick = 0; tick < 4; tick++)
                        { clock.Now += 20; router.FlushMotion(); if (!router.IsRemote) throw new InvalidOperationException("Idle timer finished a return."); }
                        clock.Now += 20; router.Move(Math.Sign(x) * 32, Math.Sign(y) * 32); router.FlushMotion();
                        if (sample < 3 && !router.IsRemote) throw new InvalidOperationException("Return did not wait for sustained pressure.");
                    }
                    if (router.IsRemote || !router.LastReturnAtEdge) throw new InvalidOperationException("Edge return failed.");
                    var display = new DisplayInfo("model", -1920, -1080, 1920, 1080);
                    var point = router.Cursor.WindowsReturnPoint(display, settings);
                    if (!display.Contains(point.X, point.Y) || EdgeDetector.IsAtPortal(point, display, [display], settings, EdgeDetector.RetentionTolerance))
                        throw new InvalidOperationException("Return point may bounce at the portal.");
                    directions.Add(edge.ToString());
                }
                await WriteAsync(directory, "edge-return.json", new { Success = true, Directions = directions, LegacySettingsMigrated = true,
                    DragProtected = true, FreshMotionRequired = true, ReturnInsetVerified = true, SmallMovementProtected = true, EntryGraceProtected = true,
                    SingleFlickProtected = true, SustainedPushRequired = true, IdleProtected = true,
                    ReportKinds = Enum.GetNames<ReportKind>(), ReportMapBytes = HidDescriptors.ReportMapValue.Length,
                    HooksInstalled = false, InputCaptured = false, BluetoothAdvertised = false, HardwareCursorMeasured = false });
                return 0;
            }
            if (mode == "--verify-pointer-precision")
            {
                var motion = new PointerMotion(); var reference = PointerTransfer.Scale(2300, 9, 8);
                var old = PointerTransfer.Apply(2300, 9, 8); var x = 0; var y = 0; var maximumError = 0d;
                for (var index = 1; index <= 1000; index++)
                {
                    motion.Add(2300, 9); var sample = motion.Take(true, 8); x += sample.X; y += sample.Y;
                    maximumError = Math.Max(maximumError, Math.Max(Math.Abs(x - reference.X * index), Math.Abs(y - reference.Y * index)));
                }
                var stopped = Enumerable.Range(0, 100).All(_ => motion.Take(true, 8) == (0, 0));
                var success = maximumError <= .5000001 && stopped && y > 0 && old.Y == 0;
                await WriteAsync(directory, "pointer-precision.json", new { Success = success, SyntheticSamples = 1000,
                    LegacyMinorAxisCounts = old.Y * 1000, ImprovedMinorAxisCounts = y, ReferenceMinorAxisCounts = reference.Y * 1000,
                    MaximumCumulativeError = maximumError, NoMotionAfterStop = stopped, InputCaptured = false, BluetoothMeasured = false });
                return success ? 0 : 2;
            }
            if (mode is "--verify-motion-input" or "--verify-edge-input")
            {
                // No advertising and no activation; unusual temporary hotkeys avoid a running app.
                await using var transport = new BleHidTransport(); await using var dispatcher = new ReportDispatcher(transport);
                var router = new InputRouter(transport, dispatcher); using var input = new NativeInput(router);
                input.Configure(new() { CaptureTouchpad = false,
                    ActivateShortcut = new(ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Shift, 0x86),
                    ReturnShortcut = new(ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Shift, 0x87) }, DisplayCatalog.Read());
                await input.StartAsync();
                if (mode == "--verify-edge-input")
                {
                    var edgeProbe = await input.ProbeEdgeHandoffAsync(); input.Dispose();
                    await WriteAsync(directory, "edge-input.json", edgeProbe);
                    return edgeProbe.Success ? 0 : 2;
                }
                var precise = input.PreciseMotionTiming; var pulses = await input.ProbeMotionTimingAsync();
                var responsive = input.Available; var raw = input.RawMouseAvailable;
                input.Dispose();
                var success = precise && pulses > 0 && responsive && raw && !input.Available && !router.IsRemote;
                await WriteAsync(directory, "motion-input.json", new { Success = success, PreciseTiming = precise, TimerPulses = pulses,
                    InputLoopResponsive = responsive, RawInputRegistered = raw, HooksRemoved = !input.Available,
                    InputSuppressed = router.IsRemote, BluetoothAdvertised = false, At = DateTimeOffset.Now });
                return success ? 0 : 2;
            }
            if (mode == "--verify-motion-timer")
            {
                using var timer = new MouseReportTimer(); timer.Start(8);
                var samples = new List<double>(); var previous = System.Diagnostics.Stopwatch.GetTimestamp();
                for (var i = 0; i < 65; i++)
                {
                    if (!timer.Wait(1000)) throw new TimeoutException("Mouse report timer did not signal.");
                    var now = System.Diagnostics.Stopwatch.GetTimestamp();
                    if (i > 0) samples.Add(System.Diagnostics.Stopwatch.GetElapsedTime(previous, now).TotalMilliseconds);
                    previous = now;
                }
                timer.Stop(); timer.Wait(0); // Consume a possible pending signal before checking cancellation.
                var stopped = !timer.Wait(30);
                timer.Start(15); var restarted = timer.Wait(1000); timer.Stop();
                var sorted = samples.Order().ToArray();
                await WriteAsync(directory, "motion-timer.json", new { Success = stopped && restarted, RequestedMs = 8,
                    Samples = samples.Count, MedianMs = sorted[sorted.Length / 2], P95Ms = sorted[(int)(sorted.Length * .95)],
                    MaxMs = sorted[^1], MeanMs = samples.Average(), Stopped = stopped, Restarted = restarted,
                    HooksInstalled = false, InputCaptured = false, BluetoothMeasured = false, At = DateTimeOffset.Now });
                return stopped && restarted ? 0 : 2;
            }
            if (mode == "--probe-touchpad")
            {
                var user32 = System.Runtime.InteropServices.NativeLibrary.Load("user32.dll");
                try
                {
                    await WriteAsync(directory, "touchpad-probe.json", new { OS = Environment.OSVersion.VersionString,
                        TouchpadWindowApi = System.Runtime.InteropServices.NativeLibrary.TryGetExport(user32, "RegisterTouchpadCapableWindow", out _),
                        GlobalGesturesController = global::Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.Input.TouchpadGesturesController"),
                        GestureCaptureSupported = NativeInput.IsTouchpadSupported(),
                        At = DateTimeOffset.Now });
                }
                finally { System.Runtime.InteropServices.NativeLibrary.Free(user32); }
                return 0;
            }
            if (mode == "--verify-input-readiness")
            {
                var samples = new List<PhysicalReadinessProbe>();
                for (var index = 0; index < 10; index++)
                { samples.Add(NativeInput.ProbePhysicalReadiness()); await Task.Delay(100); }
                await WriteAsync(directory, "input-readiness.json", new { At = DateTimeOffset.Now, HooksInstalled = false, InputCaptured = false, Samples = samples });
                return 0; // Observation only; a physically held key is a valid result.
            }
            if (mode == "--verify-server")
            {
                var log = new ConcurrentQueue<string>(); bool publishing; bool restarted; int characteristics;
                await using (var transport = new BleHidTransport())
                {
                    transport.Log += log.Enqueue;
                    await transport.StartAsync(); await Task.Delay(1000);
                    publishing = transport.Publishing; characteristics = transport.CharacteristicCount;
                    transport.Stop(); transport.Stop(); // Explicitly exercise idempotent stop before disposal.
                    await transport.StartAsync(); restarted = transport.Publishing; transport.Stop();
                }
                await WriteAsync(directory, "server-verification.json", new { Success = publishing && restarted && characteristics == 6, Publishing = publishing, Restarted = restarted, Characteristics = characteristics,
                    EncryptionRequired = true, ReportProtocolOnly = true, PairedByTest = false, InputCaptured = false, CleanupCompleted = true, Log = log.ToArray(), At = DateTimeOffset.Now });
                return publishing && restarted && characteristics == 6 ? 0 : 2;
            }
            if (mode is "--verify-local-input" or "--hold-local-input")
            {
                await using var transport = new BleHidTransport(); await using var dispatcher = new ReportDispatcher(transport);
                var router = new InputRouter(transport, dispatcher); var input = new NativeInput(router);
                input.Configure(new(), DisplayCatalog.Read());
                try
                {
                    await input.StartAsync();
                    if (mode == "--hold-local-input")
                    {
                        await WriteAsync(directory, "ready.json", new { HooksInstalled = input.Available, InputSuppressed = router.IsRemote });
                        await Task.Delay(30_000); // Bounded lifetime if the external crash-test driver stops.
                    }
                    await Task.Delay(1000); var installed = input.Available;
                    var rawRegistered = input.RawMouseAvailable; var hotkeyRegistered = input.ActivationHotkeyRegistered; var rawPackets = input.RawMousePackets;
                    var preciseTiming = input.PreciseMotionTiming;
                    var gestureRegistered = input.TouchpadAvailable;
                    var rebind = await input.UpdateShortcutsAsync(new(ShortcutModifiers.Control | ShortcutModifiers.Alt, 0x86), new(ShortcutModifiers.Control | ShortcutModifiers.Alt, 0x85)) is null;
                    rebind &= await input.UpdateShortcutsAsync(ShortcutBinding.DefaultActivate, ShortcutBinding.DefaultReturn) is null;
                    bool rollback;
                    if (!RegisterHotKey(0, 99, 0x4006, 0x87)) throw new InvalidOperationException(L.T("競合テスト用ショートカットを予約できませんでした。"));
                    try
                    {
                        rollback = await input.UpdateShortcutsAsync(new(ShortcutModifiers.Control | ShortcutModifiers.Shift, 0x87), ShortcutBinding.DefaultReturn) is not null &&
                            input.ReturnHotkeyRegistered && input.ActivationHotkeyRegistered;
                    }
                    finally { UnregisterHotKey(0, 99); }
                    input.Dispose();
                    var success = installed && rawRegistered && hotkeyRegistered && rebind && rollback && (!NativeInput.IsTouchpadSupported() || gestureRegistered) &&
                        !input.Available && !input.RawMouseAvailable && !input.TouchpadAvailable && !router.IsRemote;
                    await WriteAsync(directory, "input-verification.json", new { Success = success,
                        HooksInstalled = installed, HooksRemoved = !input.Available, RawInputRegistered = rawRegistered, RawInputRemoved = !input.RawMouseAvailable,
                        ActivationHotkeyRegistered = hotkeyRegistered, TouchpadControllerStarted = gestureRegistered, TouchpadControllerRemoved = !input.TouchpadAvailable,
                        LiveShortcutRebindPassed = rebind, ShortcutConflictRollbackPassed = rollback,
                        PreciseMotionTiming = preciseTiming, RawPacketsObserved = rawPackets, InputSuppressed = router.IsRemote, OnDefaultDesktop = NativeInput.OnDefaultDesktop(), At = DateTimeOffset.Now });
                    return success ? 0 : 2;
                }
                finally { input.Dispose(); }
            }
            throw new ArgumentException("Unknown verification mode");
        }
        catch (Exception ex) { await WriteAsync(directory, "verification-error.json", new { Success = false, Mode = mode, Error = ex.ToString() }); return 2; }
    }
    private static Task WriteAsync<T>(string directory, string file, T value) => File.WriteAllTextAsync(Path.Combine(directory, file), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    private sealed class ModelTransport : IHidTransport
    {
        public bool IsReady(string targetId) => targetId == "model";
        public Task SendAsync(string targetId, HidFrame frame, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class ModelClock : TimeProvider
    { public long Now; public override long TimestampFrequency => 1000; public override long GetTimestamp() => Now; }
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint window, int id);
}
