using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class UpdateTransferTests
{
    private sealed class ChunkStream(byte[] bytes, int chunk = 4096, string? fault = null) : MemoryStream(bytes, false)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Yield(); cancellationToken.ThrowIfCancellationRequested();
            if (fault is not null && Position >= 4096)
            {
                if (fault == "stall") await Task.Delay(Timeout.Infinite, cancellationToken);
                throw new IOException("Simulated connection interruption.");
            }
            return await base.ReadAsync(buffer[..Math.Min(chunk, buffer.Length)], cancellationToken);
        }
    }
    private sealed class Source : HttpMessageHandler
    {
        public const string Tag = "v9.9.9";
        public readonly byte[] Payload;
        public byte[] Zip;
        public JsonObject Manifest, Release;
        public string Sums;
        public string? Fault;
        public bool IgnoreRange, WrongRange;
        public readonly List<(string File, long Offset)> Downloads = [];
        public Source(bool archive = true, string entry = "UniversalBridge.exe", bool extra = false, bool wrongPayload = false)
        {
            var block = new byte[16384]; new Random(42).NextBytes(block);
            Payload = Enumerable.Range(0, 16).SelectMany(_ => block).ToArray();
            using var memory = new MemoryStream();
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
            {
                using (var output = zip.CreateEntry(entry, CompressionLevel.Optimal).Open()) output.Write(wrongPayload ? Payload.Select(b => (byte)(b ^ 1)).ToArray() : Payload);
                if (extra) { using var second = zip.CreateEntry("another.exe").Open(); second.WriteByte(1); }
            }
            Zip = memory.ToArray();
            var packed = archive ? new UpdateArchive(ReleaseUpdateClient.ArchiveFile, Zip.Length, Hash(Zip)) : null;
            Manifest = JsonSerializer.SerializeToNode(new UpdateManifest(1, "9.9.9", Tag, "UniversalBridge.exe", Payload.Length, Hash(Payload), packed))!.AsObject();
            var assets = new JsonArray();
            foreach (var name in archive ? new[] { "UniversalBridge.exe", ReleaseUpdateClient.ArchiveFile, "update.json", "SHA256SUMS.txt" } : ["UniversalBridge.exe", "update.json", "SHA256SUMS.txt"])
            {
                var data = name == "UniversalBridge.exe" ? Payload : name == ReleaseUpdateClient.ArchiveFile ? Zip : new byte[1024];
                assets.Add(new JsonObject { ["name"] = name, ["state"] = "uploaded", ["size"] = data.Length, ["digest"] = "sha256:" + Hash(data), ["browser_download_url"] = ReleaseUpdateClient.AssetUrl(Tag, name).AbsoluteUri });
            }
            Release = new() { ["draft"] = false, ["prerelease"] = false, ["tag_name"] = Tag, ["assets"] = assets };
            Sums = Hash(Payload) + "  UniversalBridge.exe\n" + (archive ? Hash(Zip) + "  " + ReleaseUpdateClient.ArchiveFile + "\n" : "");
        }
        public static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!; var file = uri.Segments.Last();
            if (uri.AbsoluteUri == ReleaseUpdateClient.LatestApi) return Response(new StringContent(Release.ToJsonString()), request);
            if (file == "update.json") return Response(new StringContent(Manifest.ToJsonString()), request);
            if (file == "SHA256SUMS.txt") return Response(new StringContent(Sums), request);
            Assert.Contains("application/octet-stream", request.Headers.Accept.ToString());
            var offset = request.Headers.Range?.Ranges.Single().From ?? 0; Downloads.Add((file, offset));
            var bytes = file == ReleaseUpdateClient.ArchiveFile ? Zip : Payload;
            var resume = offset > 0 && !IgnoreRange;
            var content = new StreamContent(new ChunkStream(resume ? bytes[(int)offset..] : bytes, fault: Downloads.Count == 1 ? Fault : null));
            content.Headers.ContentLength = resume ? bytes.Length - offset : bytes.Length;
            if (resume) content.Headers.ContentRange = new ContentRangeHeaderValue(WrongRange ? offset + 1 : offset, bytes.Length - 1, bytes.Length);
            return Task.FromResult(new HttpResponseMessage(resume ? HttpStatusCode.PartialContent : HttpStatusCode.OK) { RequestMessage = request, Content = content });
        }
        private static Task<HttpResponseMessage> Response(HttpContent body, HttpRequestMessage request) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = body });
    }
    private sealed class ProgressSink : IProgress<double>
    {
        public readonly List<double> Values = [];
        public void Report(double value) => Values.Add(value);
    }
    private static async Task WithTarget(Func<string, Task> action)
    {
        var folder = Path.Combine(Path.GetTempPath(), "UniversalBridge-transfer-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try { await action(Path.Combine(folder, "Payload.exe")); }
        finally { foreach (var file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); }
    }
    [Fact] public async Task ArchiveTransfersFewerBytesAndProducesExactlyTheVerifiedExecutable()
    {
        await WithTarget(async path =>
        {
            using var source = new Source(); using var http = new HttpClient(source); var client = new ReleaseUpdateClient(http);
            var update = (await client.CheckAsync(new(0, 0, 0), default))!; var progress = new ProgressSink();
            var result = await client.DownloadAsync(update, path, progress, default);
            Assert.True(result.Compressed); Assert.Equal(source.Zip.Length, result.NetworkBytes); Assert.True(result.NetworkBytes < source.Payload.Length / 4);
            Assert.Single(source.Downloads); Assert.Equal(ReleaseUpdateClient.ArchiveFile, source.Downloads[0].File);
            Assert.Equal(source.Payload, await File.ReadAllBytesAsync(path)); Assert.Equal(1, progress.Values[^1]);
            Assert.True(progress.Values.Count < 15); Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!));
        });
    }
    [Theory] [InlineData("disconnect")][InlineData("stall")]
    public async Task InterruptedArchiveResumesFromItsVerifiedOffset(string fault)
    {
        await WithTarget(async path =>
        {
            using var source = new Source { Fault = fault }; using var http = new HttpClient(source); var client = new ReleaseUpdateClient(http, TimeSpan.FromMilliseconds(40));
            var update = (await client.CheckAsync(new(0, 0, 0), default))!; var result = await client.DownloadAsync(update, path, null, default);
            Assert.Equal(2, result.Requests); Assert.Equal(4096, source.Downloads[1].Offset);
            Assert.Equal(source.Zip.Length, result.NetworkBytes); Assert.Equal(source.Payload, await File.ReadAllBytesAsync(path));
        });
    }
    [Fact] public async Task ServerIgnoringRangesRestartsSafelyAndInvalidRangesAreRejected()
    {
        await WithTarget(async path =>
        {
            using var source = new Source { Fault = "disconnect", IgnoreRange = true }; using var http = new HttpClient(source); var client = new ReleaseUpdateClient(http);
            var update = (await client.CheckAsync(new(0, 0, 0), default))!; var result = await client.DownloadAsync(update, path, null, default);
            Assert.True(result.NetworkBytes > source.Zip.Length); Assert.Equal(source.Payload, await File.ReadAllBytesAsync(path));
        });
        await WithTarget(async path =>
        {
            using var source = new Source { Fault = "disconnect", WrongRange = true }; using var http = new HttpClient(source); var client = new ReleaseUpdateClient(http);
            var update = (await client.CheckAsync(new(0, 0, 0), default))!;
            await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadAsync(update, path, null, default)); Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!));
        });
    }
    [Theory] [InlineData("corrupt")][InlineData("path")][InlineData("extra")][InlineData("payload")]
    public async Task BrokenOrUnexpectedZipContentsNeverProduceAnInstallableFile(string mutation)
    {
        await WithTarget(async path =>
        {
            using var source = new Source(entry: mutation == "path" ? "../UniversalBridge.exe" : "UniversalBridge.exe", extra: mutation == "extra", wrongPayload: mutation == "payload");
            using var http = new HttpClient(source); var client = new ReleaseUpdateClient(http); var update = (await client.CheckAsync(new(0, 0, 0), default))!;
            if (mutation == "corrupt") source.Zip[100] ^= 1;
            await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadAsync(update, path, null, default));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!));
        });
    }
    [Theory] [InlineData("file")][InlineData("size")][InlineData("digest")][InlineData("sums")][InlineData("missing")]
    public async Task InvalidArchiveMetadataCannotSupplyAnUpdate(string mutation)
    {
        using var source = new Source();
        var archive = source.Manifest["Archive"]!.AsObject();
        switch (mutation)
        {
            case "file": archive["File"] = "../other.zip"; break;
            case "size": archive["Size"] = 1; break;
            case "digest": source.Release["assets"]!.AsArray()[1]!["digest"] = "sha256:" + new string('0', 64); break;
            case "sums": source.Sums = source.Sums.Replace(Source.Hash(source.Zip), new string('0', 64), StringComparison.Ordinal); break;
            case "missing": source.Release["assets"]!.AsArray().RemoveAt(1); break;
        }
        using var http = new HttpClient(source);
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseUpdateClient(http).CheckAsync(new(0, 0, 0), default));
    }
    [Fact] public async Task LegacyRawReleasesRemainSupportedAndExistingTargetsArePreserved()
    {
        await WithTarget(async path =>
        {
            using var source = new Source(false); using var http = new HttpClient(source); var client = new ReleaseUpdateClient(http);
            var update = (await client.CheckAsync(new(0, 0, 0), default))!; var result = await client.DownloadAsync(update, path, null, default);
            Assert.False(result.Compressed); Assert.Equal(source.Payload, await File.ReadAllBytesAsync(path));
        });
        await WithTarget(async path =>
        {
            using var source = new Source(); using var http = new HttpClient(source); var client = new ReleaseUpdateClient(http); var update = (await client.CheckAsync(new(0, 0, 0), default))!;
            await File.WriteAllTextAsync(path, "preserve");
            await Assert.ThrowsAsync<IOException>(() => client.DownloadAsync(update, path, null, default));
            Assert.Equal("preserve", await File.ReadAllTextAsync(path)); Assert.Empty(source.Downloads);
        });
    }
    [Fact] public async Task CancellationDoesNotLeaveUnverifiedFilesOrRetryInTheBackground()
    {
        await WithTarget(async path =>
        {
            using var source = new Source { Fault = "stall" }; using var http = new HttpClient(source); var client = new ReleaseUpdateClient(http);
            var update = (await client.CheckAsync(new(0, 0, 0), default))!; using var stop = new CancellationTokenSource(80);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.DownloadAsync(update, path, null, stop.Token));
            Assert.Single(source.Downloads); Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!));
        });
    }
}
