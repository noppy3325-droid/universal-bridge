using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using UniversalControl.Core;

namespace UniversalControl.Windows;

public sealed record UpdateJob(string Id, string Target, string DataDirectory, int ParentId, long ParentStartUtcTicks,
    string PreviousHash, UpdateManifest Manifest, int FixtureMode = 0);
public sealed record UpdateResult(bool Success, bool RolledBack, string? Error);

/// <summary>Runs from a private copy, waits for the owning process to exit, and replaces only its executable.</summary>
public static class SelfUpdateRunner
{
    public static Version CurrentVersion => VersionOf(Environment.ProcessPath!);
    public static Version VersionOf(string path) => ReleaseUpdateClient.ParseVersion(FileVersionInfo.GetVersionInfo(path).ProductVersion!.Split('+')[0]);
    private static string Full(string path) => Path.GetFullPath(path);
    private static bool Same(string a, string b) => Full(a).Equals(Full(b), StringComparison.OrdinalIgnoreCase);
    public static string CreateStage(string dataDirectory)
    {
        var path = Path.Combine(Full(dataDirectory), "updates", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path;
    }
    public static string Backup(UpdateJob job) => Path.Combine(Path.GetDirectoryName(job.Target)!, $"UniversalControl.{job.Id}.previous.exe");
    public static async Task<string> LaunchHelperAsync(string stage, string dataDirectory, UpdateManifest manifest, int fixtureMode = 0)
    {
        var target = Full(Environment.ProcessPath!);
        if (!Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase) || FileVersionInfo.GetVersionInfo(target).ProductName != "Universal Bridge")
            throw new InvalidOperationException("Self-update requires the distributed Universal Bridge executable.");
        if (!Same(stage, Path.Combine(dataDirectory, "updates", Path.GetFileName(stage))) || !Guid.TryParseExact(Path.GetFileName(stage), "N", out _))
            throw new InvalidDataException("Invalid update staging directory.");
        await ValidatePayload(stage, manifest);
        // Test write access before asking the running application to shut down.
        var probe = Path.Combine(Path.GetDirectoryName(target)!, $".UniversalBridge-write-{Guid.NewGuid():N}.tmp");
        using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        File.Delete(probe);
        using var parent = Process.GetCurrentProcess();
        var job = new UpdateJob(Path.GetFileName(stage), target, Full(dataDirectory), parent.Id, parent.StartTime.ToUniversalTime().Ticks,
            await Hash(target), manifest, fixtureMode);
        var jobPath = Path.Combine(stage, "job.json");
        await File.WriteAllTextAsync(jobPath, JsonSerializer.Serialize(job));
        var helper = Path.Combine(stage, "Updater.exe"); File.Copy(target, helper, false);
        using var process = Start(helper, "--apply-update", jobPath);
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(15))
        {
            if (File.Exists(Path.Combine(stage, "ready"))) return jobPath;
            if (process.HasExited) throw new IOException("Update helper could not prepare the replacement.");
            await Task.Delay(50);
        }
        // This is our own helper, which has not received permission to change the target yet.
        if (!process.HasExited) process.Kill();
        throw new TimeoutException("Update helper did not become ready.");
    }
    private static UpdateJob ReadJob(string jobPath)
    {
        var job = JsonSerializer.Deserialize<UpdateJob>(File.ReadAllText(jobPath)) ?? throw new InvalidDataException("Missing update job.");
        if (!Guid.TryParseExact(job.Id, "N", out _) || !Same(jobPath, Path.Combine(job.DataDirectory, "updates", job.Id, "job.json")) ||
            !Path.GetExtension(job.Target).Equals(".exe", StringComparison.OrdinalIgnoreCase) || job.FixtureMode is < 0 or > 2)
            throw new InvalidDataException("Invalid update job paths.");
        return job;
    }
    private static async Task ValidatePayload(string stage, UpdateManifest manifest)
    {
        if (manifest.Schema != 1 || manifest.File != "UniversalControl.exe") throw new InvalidDataException("Invalid update payload manifest.");
        _ = ReleaseUpdateClient.AssetUrl(manifest.Tag, manifest.File);
        await ReleaseUpdateClient.VerifyFileAsync(Path.Combine(stage, "Payload.exe"), manifest.Size, manifest.Sha256);
        var info = FileVersionInfo.GetVersionInfo(Path.Combine(stage, "Payload.exe"));
        if (info.ProductName != "Universal Bridge" || VersionOf(Path.Combine(stage, "Payload.exe")) != ReleaseUpdateClient.ParseVersion(manifest.Version))
            throw new InvalidDataException("Update executable version/product mismatch.");
    }
    public static async Task<int> ApplyAsync(string jobPath)
    {
        UpdateJob? job = null; Process? replacement = null; var swapped = false; var stage = Path.GetDirectoryName(Full(jobPath))!;
        try
        {
            job = ReadJob(jobPath);
            if (!Same(Environment.ProcessPath!, Path.Combine(stage, "Updater.exe"))) throw new InvalidDataException("Unexpected update helper location.");
            using var parent = Process.GetProcessById(job.ParentId);
            if (parent.StartTime.ToUniversalTime().Ticks != job.ParentStartUtcTicks || !Same(parent.MainModule!.FileName, job.Target) ||
                await Hash(job.Target) != job.PreviousHash || ReleaseUpdateClient.ParseVersion(job.Manifest.Version) < VersionOf(job.Target))
                throw new InvalidDataException("Update owner no longer matches.");
            await ValidatePayload(stage, job.Manifest);
            await File.WriteAllTextAsync(Path.Combine(stage, "ready"), job.Id);
            await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90));
            if (await Hash(job.Target) != job.PreviousHash) throw new InvalidDataException("Executable changed while waiting for exit.");
            await ValidatePayload(stage, job.Manifest);
            var temporary = Path.Combine(Path.GetDirectoryName(job.Target)!, $".UniversalControl-{job.Id}.tmp");
            File.Copy(Path.Combine(stage, "Payload.exe"), temporary, false);
            await ReleaseUpdateClient.VerifyFileAsync(temporary, job.Manifest.Size, job.Manifest.Sha256);
            await Retry(() => File.Replace(temporary, job.Target, Backup(job))); swapped = true;
            replacement = StartTarget(job, jobPath, rollback: false);
            var timer = Stopwatch.StartNew(); var healthy = Path.Combine(stage, "healthy");
            while (timer.Elapsed < TimeSpan.FromSeconds(30) && !replacement.HasExited)
            {
                if (File.Exists(healthy) && await File.ReadAllTextAsync(healthy) == job.Id)
                { swapped = false; await Result(stage, new(true, false, null)); return 0; }
                await Task.Delay(100);
            }
            throw new IOException("Updated application did not confirm successful startup.");
        }
        catch (Exception ex)
        {
            var rolledBack = false;
            if (swapped && job is not null)
            {
                try
                {
                    // Only the child started by this helper can be stopped for rollback.
                    if (replacement is not null && !replacement.HasExited) { replacement.Kill(); await replacement.WaitForExitAsync(); }
                    await Retry(() => File.Replace(Backup(job), job.Target, Path.Combine(stage, "Failed.exe")));
                    rolledBack = await Hash(job.Target) == job.PreviousHash;
                    if (rolledBack) using (StartTarget(job, jobPath, rollback: true)) { }
                }
                catch (Exception rollback) { ex = new AggregateException(ex, rollback); }
            }
            // Only write beside a validated job, never beside an arbitrary command-line path.
            if (job is not null) await Result(stage, new(false, rolledBack, ex.Message));
            return 2;
        }
        finally { replacement?.Dispose(); }
    }
    private static Process StartTarget(UpdateJob job, string jobPath, bool rollback)
    {
        if (job.FixtureMode != 0)
            return Start(job.Target, rollback ? "--update-fixture-rollback" : job.FixtureMode == 1 ? "--update-fixture-healthy" : "--update-fixture-fail", jobPath);
        return Start(job.Target, "--data-dir", job.DataDirectory, rollback ? "--update-rollback" : "--updated", jobPath);
    }
    public static async Task ConfirmStartupAsync(string jobPath)
    {
        var job = ReadJob(jobPath);
        if (!Same(job.Target, Environment.ProcessPath!)) throw new InvalidDataException("Unexpected updated executable.");
        await ReleaseUpdateClient.VerifyFileAsync(job.Target, job.Manifest.Size, job.Manifest.Sha256);
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(jobPath)!, "healthy"), job.Id);
    }
    public static async Task<string> Hash(string path)
    { await using var input = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(input)).ToLowerInvariant(); }
    public static Process Start(string executable, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = Path.GetDirectoryName(executable)! };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return Process.Start(start) ?? throw new IOException("Could not start the update process.");
    }
    private static Task Result(string stage, UpdateResult result) => File.WriteAllTextAsync(Path.Combine(stage, "result.json"), JsonSerializer.Serialize(result));
    private static async Task Retry(Action action)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { action(); return; }
            catch (IOException) when (attempt < 30) { await Task.Delay(100); }
        }
    }
    public static void CleanCache(string dataDirectory)
    {
        var root = Path.Combine(Full(dataDirectory), "updates"); if (!Directory.Exists(root)) return;
        var directories = Directory.GetDirectories(root).OrderByDescending(Directory.GetCreationTimeUtc).ToArray(); var keptBackup = false;
        foreach (var directory in directories)
        {
            try
            {
                if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                var job = ReadJob(Path.Combine(directory, "job.json"));
                if (!Same(job.Target, Environment.ProcessPath!) || !File.Exists(Path.Combine(directory, "result.json"))) continue;
                var result = JsonSerializer.Deserialize<UpdateResult>(File.ReadAllText(Path.Combine(directory, "result.json")));
                if (result?.Success != true) continue;
                File.Delete(Path.Combine(directory, "Payload.exe")); File.Delete(Path.Combine(directory, "Updater.exe"));
                var keep = !keptBackup; keptBackup = true;
                if (!keep) File.Delete(Backup(job));
                foreach (var name in new[] { "ready", "healthy" }) File.Delete(Path.Combine(directory, name));
                if (!keep) { File.Delete(Path.Combine(directory, "result.json")); File.Delete(Path.Combine(directory, "job.json")); Directory.Delete(directory, false); }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException) { }
        }
    }
}
