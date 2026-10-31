using System.Diagnostics;
using System.IO.Compression;
using System.Net;

namespace UniversalControl.Core;

public sealed partial class ReleaseUpdateClient
{
    public async Task<UpdateDownloadResult> DownloadAsync(ReleaseUpdate update, string path, IProgress<double>? progress,
        CancellationToken cancellationToken, IProgress<UpdateTransferProgress>? transferProgress = null)
    {
        if (update.DownloadUrl != AssetUrl(update.Manifest.Tag, "UniversalBridge.exe")) throw new InvalidDataException("Unexpected update URL.");
        if (update.Manifest.Size is < 1024 or > MaximumExeBytes) throw new InvalidDataException("Invalid update size.");
        if (File.Exists(path)) throw new IOException("Update destination already exists.");
        var compressed = update.Manifest.Archive is not null;
        if (!compressed && update.ArchiveUrl is not null) throw new InvalidDataException("Unexpected archive without a manifest.");
        if (compressed && (update.ArchiveUrl != AssetUrl(update.Manifest.Tag, ArchiveFile) || update.Manifest.Archive!.File != ArchiveFile))
            throw new InvalidDataException("Unexpected update archive URL.");
        var archivePath = path + "." + Guid.NewGuid().ToString("N") + ".zip.part";
        var targetCreated = false; var archiveCreated = false;
        var watch = Stopwatch.StartNew(); var lastProgress = -1000L; long networkBytes = 0; var requests = 0;
        void Report(long received, long total, bool complete = false)
        {
            if (!complete && watch.ElapsedMilliseconds - lastProgress < 100) return;
            lastProgress = watch.ElapsedMilliseconds;
            var fraction = complete ? 1 : Math.Min(.98, received / (double)total * .98);
            progress?.Report(fraction);
            transferProgress?.Report(new(fraction, received, total, networkBytes / Math.Max(.001, watch.Elapsed.TotalSeconds), compressed));
        }
        try
        {
            var size = update.Manifest.Archive?.Size ?? update.Manifest.Size;
            var hash = update.Manifest.Archive?.Sha256 ?? update.Manifest.Sha256;
            if (size is < 1024 or > MaximumExeBytes) throw new InvalidDataException("Invalid update transfer size.");
            var downloadPath = compressed ? archivePath : path;
            // CreateNew owns exactly one staging file. Existing files are never overwritten or removed.
            await using (var output = new FileStream(downloadPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 262144, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                if (compressed) archiveCreated = true; else targetCreated = true;
                var buffer = new byte[262144]; var failures = 0;
                while (output.Position < size)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var offset = output.Position;
                    try
                    {
                        using var headersTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        headersTimeout.CancelAfter(TimeSpan.FromSeconds(20));
                        requests++;
                        using var response = await GetAsync(update.ArchiveUrl ?? update.DownloadUrl, headersTimeout.Token, offset, true).ConfigureAwait(false);
                        headersTimeout.CancelAfter(Timeout.InfiniteTimeSpan);
                        response.EnsureSuccessStatusCode();
                        if (offset > 0 && response.StatusCode == HttpStatusCode.OK)
                        { output.SetLength(0); output.Position = offset = 0; }
                        if (offset > 0)
                        {
                            var range = response.Content.Headers.ContentRange;
                            if (response.StatusCode != HttpStatusCode.PartialContent || range?.Unit != "bytes" || range.From != offset || range.To != size - 1 || range.Length != size)
                                throw new InvalidDataException("Invalid resumed update range.");
                        }
                        else if (response.StatusCode != HttpStatusCode.OK) throw new InvalidDataException("Unexpected partial update response.");
                        if (response.Content.Headers.ContentLength is { } length && length != size - offset) throw new InvalidDataException("Update length mismatch.");
                        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                        while (true)
                        {
                            using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                            readTimeout.CancelAfter(_readTimeout);
                            var count = await input.ReadAsync(buffer, readTimeout.Token).ConfigureAwait(false);
                            if (count == 0) break;
                            networkBytes += count;
                            if (output.Position + count > size) throw new InvalidDataException("Update is too large.");
                            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                            Report(output.Position, size);
                        }
                        if (output.Position != size)
                        {
                            if (failures >= 2) throw new InvalidDataException("Update is truncated.");
                            throw new IOException("Update transfer ended early.");
                        }
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested && failures < 2 &&
                        (ex is IOException or OperationCanceledException || ex is HttpRequestException request &&
                            (request.StatusCode is null or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout or HttpStatusCode.TooManyRequests)))
                    {
                        failures++;
                        await Task.Delay(150 * failures, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            await VerifyFileAsync(downloadPath, size, hash, cancellationToken).ConfigureAwait(false);
            if (compressed)
            {
                using var archive = ZipFile.OpenRead(archivePath);
                if (archive.Entries.Count != 1 || archive.Entries[0].FullName != "UniversalBridge.exe" || archive.Entries[0].Length != update.Manifest.Size)
                    throw new InvalidDataException("Unexpected update archive contents.");
                await using var input = archive.Entries[0].Open();
                await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 262144, true))
                {
                    targetCreated = true; var buffer = new byte[262144]; long total = 0; int count;
                    while ((count = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
                    {
                        total += count;
                        if (total > update.Manifest.Size) throw new InvalidDataException("Expanded update is too large.");
                        await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                    }
                    if (total != update.Manifest.Size) throw new InvalidDataException("Expanded update is incomplete.");
                }
                await VerifyFileAsync(path, update.Manifest.Size, update.Manifest.Sha256, cancellationToken).ConfigureAwait(false);
            }
            Report(size, size, true);
            return new(compressed, networkBytes, requests, watch.Elapsed);
        }
        catch { if (targetCreated && File.Exists(path)) File.Delete(path); throw; }
        finally { if (archiveCreated && File.Exists(archivePath)) File.Delete(archivePath); }
    }
}
