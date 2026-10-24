using System.Collections.Concurrent;
using System.Text.Json;
using UniversalControl.Core;
using UniversalControl.Windows;

namespace UniversalControl.App;

public static class HeadlessVerification
{
    public static async Task<int> RunAsync(string mode, string directory)
    {
        Directory.CreateDirectory(directory);
        try
        {
            if (mode == "--verify-motion-input")
            {
                // No advertising and no activation; unusual temporary hotkeys avoid a running app.
                await using var transport = new BleHidTransport(); await using var dispatcher = new ReportDispatcher(transport);
                var router = new InputRouter(transport, dispatcher); using var input = new NativeInput(router);
                input.Configure(new() { CaptureTouchpad = false,
                    ActivateShortcut = new(ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Shift, 0x86),
                    ReturnShortcut = new(ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Shift, 0x87) }, DisplayCatalog.Read());
                await input.StartAsync();
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
                        TouchpadInfoApi = System.Runtime.InteropServices.NativeLibrary.TryGetExport(user32, "GetPointerTouchpadInfo", out _),
                        GlobalGesturesController = global::Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.Input.TouchpadGesturesController"),
                        GestureCaptureSupported = NativeInput.IsTouchpadSupported(),
                        NativeContacts = NativeInput.ProbeNativeContacts(),
                        PointerTouchpadProperties = global::Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.Input.PointerTouchpadProperties"),
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
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint window, int id);
}
