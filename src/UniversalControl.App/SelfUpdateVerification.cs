using System.Diagnostics;
using System.Text.Json;
using UniversalControl.Core;
using UniversalControl.Windows;

namespace UniversalControl.App;

/// <summary>Real process/file replacement against private copies. Never uses the normal app mutex or BLE.</summary>
public static class SelfUpdateVerification
{
    public static async Task<int> FixtureAsync(string mode, string value)
    {
        if (mode == "--update-fixture-parent")
        {
            var directory = Path.GetDirectoryName(Environment.ProcessPath!)!;
            try
            {
                var data = Path.Combine(directory, "data"); var stage = SelfUpdateRunner.CreateStage(data);
                var payload = Path.Combine(stage, "Payload.exe"); File.Copy(Environment.ProcessPath!, payload);
                var hash = await SelfUpdateRunner.Hash(payload); if (value == "3") hash = new string('0', 64);
                var manifest = new UpdateManifest(1, SelfUpdateRunner.CurrentVersion.ToString(3), "v1.05", "UniversalBridge.exe", new FileInfo(payload).Length, hash);
                var job = await SelfUpdateRunner.LaunchHelperAsync(stage, data, manifest, value == "4" ? 1 : value == "5" ? 3 : int.Parse(value));
                await File.WriteAllTextAsync(Path.Combine(directory, "job-path.txt"), job); return 0;
            }
            catch (Exception ex) { await File.WriteAllTextAsync(Path.Combine(directory, "fixture-error.txt"), ex.Message); return 3; }
        }
        if (mode == "--update-fixture-healthy") { await SelfUpdateRunner.ConfirmStartupAsync(value); await Task.Delay(2000); return 0; }
        if (mode == "--update-fixture-locked-marker")
        {
            var job = JsonSerializer.Deserialize<UpdateJob>(await File.ReadAllTextAsync(value))!;
            await ReleaseUpdateClient.VerifyFileAsync(Environment.ProcessPath!, job.Manifest.Size, job.Manifest.Sha256);
            // Deliberately publish a locked marker to reproduce the reader/writer race.
            await using (var marker = new FileStream(Path.Combine(Path.GetDirectoryName(value)!, "healthy"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await marker.WriteAsync(System.Text.Encoding.UTF8.GetBytes(job.Id));
                await marker.FlushAsync();
                await Task.Delay(700);
            }
            await Task.Delay(2000); return 0;
        }
        if (mode == "--update-fixture-fail") return 31;
        if (mode == "--update-fixture-rollback") { await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(value)!, "rollback-restarted"), "ok"); return 0; }
        return 2;
    }
    public static async Task<int> VerifyAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        try
        {
            var outcomes = new List<object>();
            for (var mode = 1; mode <= 5; mode++)
            {
                var root = Path.Combine(directory, $"transaction-{mode}-{Guid.NewGuid():N}", "更新 テスト space"); Directory.CreateDirectory(root);
                var target = Path.Combine(root, mode == 1 ? "UniversalBridge custom.exe" : "UniversalBridge.exe"); File.Copy(Environment.ProcessPath!, target);
                Directory.CreateDirectory(Path.Combine(root, "data")); var settings = Path.Combine(root, "data", "settings-sentinel.txt");
                await File.WriteAllTextAsync(settings, "preserve-settings"); var originalHash = await SelfUpdateRunner.Hash(target);
                using var sharingLock = mode == 4 ? new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
                using var parent = SelfUpdateRunner.Start(target, "--update-fixture-parent", mode.ToString());
                await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
                if (mode == 3)
                {
                    if (parent.ExitCode != 3 || !File.Exists(Path.Combine(root, "fixture-error.txt")) || await SelfUpdateRunner.Hash(target) != originalHash)
                        throw new InvalidOperationException("Corrupt update was not rejected before replacement.");
                    outcomes.Add(new { Mode = "corrupt", Rejected = true }); continue;
                }
                if (parent.ExitCode != 0) throw new InvalidOperationException("Update fixture parent failed.");
                var jobPath = await File.ReadAllTextAsync(Path.Combine(root, "job-path.txt")); var stage = Path.GetDirectoryName(jobPath)!;
                var timer = Stopwatch.StartNew();
                while (!File.Exists(Path.Combine(stage, "result.json")))
                { if (timer.Elapsed > TimeSpan.FromSeconds(45)) throw new TimeoutException("Update fixture result timed out."); await Task.Delay(100); }
                var result = JsonSerializer.Deserialize<UpdateResult>(await File.ReadAllTextAsync(Path.Combine(stage, "result.json")))!;
                if (mode is 1 or 5 && (!result.Success || result.RolledBack || !File.Exists(Path.Combine(stage, "healthy")))) throw new InvalidOperationException("Successful update failed health confirmation.");
                if (mode is 2 or 4)
                {
                    while (!File.Exists(Path.Combine(stage, "rollback-restarted")))
                    { if (timer.Elapsed > TimeSpan.FromSeconds(45)) throw new TimeoutException("Rollback did not restart the old application."); await Task.Delay(100); }
                    if (result.Success || !result.RolledBack || await SelfUpdateRunner.Hash(target) != originalHash) throw new InvalidOperationException("Rollback did not restore the old executable.");
                }
                if (await File.ReadAllTextAsync(settings) != "preserve-settings") throw new InvalidOperationException("Update changed settings.");
                outcomes.Add(new { Mode = mode == 1 ? "success" : mode == 2 ? "rollback" : mode == 5 ? "startup-marker-lock" : "sharing-violation-recovery", result.Success, result.RolledBack, SettingsPreserved = true });
            }
            await File.WriteAllTextAsync(Path.Combine(directory, "self-update.json"), JsonSerializer.Serialize(new { Success = true, Outcomes = outcomes,
                RealProcessReplacement = true, RestartHealthConfirmed = true, RollbackConfirmed = true, HashFailureRejected = true, SpaceAndUnicodePaths = true,
                SameVersionFixture = true, RenamedExecutableSupported = true, SharingViolationRecovery = true, StartupMarkerLockTolerated = true, NormalAppTouched = false, InputCaptured = false, BluetoothAdvertised = false }, new JsonSerializerOptions { WriteIndented = true })); return 0;
        }
        catch (Exception ex) { await File.WriteAllTextAsync(Path.Combine(directory, "self-update-error.txt"), ex.ToString()); return 2; }
    }
}
