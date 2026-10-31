using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class UpdateTests
{
    private sealed class Server : HttpMessageHandler
    {
        public byte[] Payload = Enumerable.Range(0, 2048).Select(n => (byte)(n % 251)).ToArray();
        public JsonObject Manifest;
        public JsonObject Release;
        public string? Sums;
        public HttpStatusCode LatestStatus = HttpStatusCode.OK;
        public string? Redirect;
        public long? PayloadLength;
        public readonly List<Uri> Requests = [];
        public Server()
        {
            var hash = Convert.ToHexString(SHA256.HashData(Payload)).ToLowerInvariant();
            Manifest = JsonSerializer.SerializeToNode(new UpdateManifest(1, "1.0.4", "v1.04", "UniversalControl.exe", Payload.Length, hash))!.AsObject();
            Release = new JsonObject { ["draft"] = false, ["prerelease"] = false, ["tag_name"] = "v1.04", ["assets"] = new JsonArray() };
            foreach (var name in new[] { "update.json", "UniversalControl.exe", "SHA256SUMS.txt" })
                Release["assets"]!.AsArray().Add(new JsonObject { ["name"] = name, ["state"] = "uploaded", ["browser_download_url"] = ReleaseUpdateClient.AssetUrl("v1.04", name).AbsoluteUri,
                    ["size"] = name.EndsWith(".exe") ? Payload.Length : 1000, ["digest"] = name.EndsWith(".exe") ? "sha256:" + hash : null });
        }
        public JsonObject Exe => Release["assets"]!.AsArray()[1]!.AsObject();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Requests.Add(request.RequestUri!);
            Assert.NotEmpty(request.Headers.UserAgent);
            var url = request.RequestUri!.AbsoluteUri;
            var status = url == ReleaseUpdateClient.LatestApi ? LatestStatus : HttpStatusCode.OK;
            HttpContent content = url == ReleaseUpdateClient.LatestApi ? new StringContent(Release.ToJsonString())
                : url.EndsWith("update.json") ? new StringContent(Manifest.ToJsonString())
                : url.EndsWith("SHA256SUMS.txt") ? new StringContent(Sums ?? Manifest["Sha256"]!.GetValue<string>() + "  UniversalControl.exe\n")
                : url.EndsWith("UniversalControl.exe") ? new ByteArrayContent(Payload) : throw new InvalidOperationException("Unexpected URL");
            if (url.EndsWith(".exe") && PayloadLength.HasValue) content.Headers.ContentLength = PayloadLength;
            return Task.FromResult(new HttpResponseMessage(status) { Content = content, RequestMessage = Redirect is null ? request : new(HttpMethod.Get, Redirect) });
        }
    }
    [Fact] public async Task StableReleaseRequiresMatchingMetadataAndChecksums()
    {
        using var server = new Server(); using var http = new HttpClient(server); var update = await new ReleaseUpdateClient(http).CheckAsync(new(1, 0, 3), default);
        Assert.NotNull(update); Assert.Equal("1.0.4", update.Manifest.Version); Assert.Equal(3, server.Requests.Count);
        Assert.All(server.Requests, uri => Assert.Equal("https", uri.Scheme));
    }
    [Theory] [InlineData("1.0.4")][InlineData("1.1.0")]
    public async Task EqualOrOlderVersionNeverReplacesCurrentApp(string current)
    { using var server = new Server(); using var http = new HttpClient(server); Assert.Null(await new ReleaseUpdateClient(http).CheckAsync(Version.Parse(current), default)); }
    [Theory] [InlineData("draft")][InlineData("prerelease")]
    public async Task DraftAndPreviewReleasesAreIgnored(string property)
    { using var server = new Server(); server.Release[property] = true; using var http = new HttpClient(server); Assert.Null(await new ReleaseUpdateClient(http).CheckAsync(new(1, 0, 3), default)); Assert.Single(server.Requests); }
    [Fact] public async Task OlderReleaseWithoutUpdaterManifestIsCompatible()
    { using var server = new Server(); server.Release["assets"]!.AsArray().RemoveAt(0); using var http = new HttpClient(server); Assert.Null(await new ReleaseUpdateClient(http).CheckAsync(new(1, 0, 3), default)); }
    [Theory]
    [InlineData("Schema", "2")][InlineData("Version", "1.0.4-preview")][InlineData("Tag", "v1.05")]
    [InlineData("File", "../other.exe")][InlineData("Size", "0")][InlineData("Size", "999999999")][InlineData("Sha256", "invalid")]
    public async Task InvalidManifestCannotSupplyAnExecutable(string property, string value)
    {
        using var server = new Server(); server.Manifest[property] = property is "Schema" or "Size" ? JsonValue.Create(long.Parse(value)) : JsonValue.Create(value);
        using var http = new HttpClient(server); await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseUpdateClient(http).CheckAsync(new(1, 0, 3), default));
    }
    [Theory] [InlineData("url")][InlineData("digest")][InlineData("size")][InlineData("state")][InlineData("duplicate")][InlineData("sums")]
    public async Task MismatchedOrAmbiguousAssetsAreRejected(string mutation)
    {
        using var server = new Server();
        switch (mutation)
        {
            case "url": server.Exe["browser_download_url"] = "https://example.org/UniversalControl.exe"; break;
            case "digest": server.Exe["digest"] = "sha256:" + new string('0', 64); break;
            case "size": server.Exe["size"] = 4096; break;
            case "state": server.Exe["state"] = "new"; break;
            case "duplicate": server.Release["assets"]!.AsArray().Add(server.Exe.DeepClone()); break;
            case "sums": server.Sums = new string('0', 64) + "  UniversalControl.exe"; break;
        }
        using var http = new HttpClient(server); await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseUpdateClient(http).CheckAsync(new(1, 0, 3), default));
        Assert.DoesNotContain(server.Requests, uri => uri.Host == "example.org");
    }
    [Theory] [InlineData("v1.04/../../file")][InlineData("v1.04-preview")][InlineData("https://example.org")]
    public async Task ReleaseTagCannotEscapeRepository(string tag)
    { using var server = new Server(); server.Release["tag_name"] = tag; using var http = new HttpClient(server); await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseUpdateClient(http).CheckAsync(new(1, 0, 3), default)); Assert.Single(server.Requests); }
    [Theory] [InlineData("http://github.com/file")][InlineData("https://example.org/file")]
    public async Task RedirectMustRemainOnTrustedHttpsHosts(string redirect)
    { using var server = new Server { Redirect = redirect }; using var http = new HttpClient(server); await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseUpdateClient(http).CheckAsync(new(1, 0, 3), default)); }
    [Fact] public async Task MissingReleaseIsNotAnUpdateAndApiFailuresDoNotPretendSuccess()
    {
        using var server = new Server { LatestStatus = HttpStatusCode.NotFound }; using var http = new HttpClient(server); var client = new ReleaseUpdateClient(http);
        Assert.Null(await client.CheckAsync(new(1, 0, 3), default)); server.LatestStatus = HttpStatusCode.Forbidden;
        await Assert.ThrowsAsync<HttpRequestException>(() => client.CheckAsync(new(1, 0, 3), default));
    }
    [Fact] public async Task MetadataHasABoundedSize()
    {
        using var server = new Server(); server.Manifest["padding"] = new string('a', 17000); using var http = new HttpClient(server);
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseUpdateClient(http).CheckAsync(new(1, 0, 3), default));
    }
    [Theory] [InlineData("valid")][InlineData("hash")][InlineData("short")][InlineData("large")][InlineData("existing")]
    public async Task DownloadIsVerifiedAndNeverOverwritesAnExistingFile(string mutation)
    {
        var directory = Path.Combine(Path.GetTempPath(), "UniversalBridge-update-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "Payload.exe");
        try
        {
            using var server = new Server(); using var http = new HttpClient(server); var client = new ReleaseUpdateClient(http);
            var update = (await client.CheckAsync(new(1, 0, 3), default))!;
            if (mutation == "hash") server.Payload[10] ^= 255;
            if (mutation == "short") { server.Payload = server.Payload[..1024]; server.PayloadLength = 2048; }
            if (mutation == "large") { server.Payload = new byte[4096]; server.PayloadLength = 2048; }
            if (mutation == "existing") await File.WriteAllTextAsync(path, "preserve");
            if (mutation == "valid") { await client.DownloadAsync(update, path, null, default); Assert.Equal(server.Payload, await File.ReadAllBytesAsync(path)); }
            else
            {
                if (mutation == "existing") await Assert.ThrowsAnyAsync<IOException>(() => client.DownloadAsync(update, path, null, default));
                else await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadAsync(update, path, null, default));
                if (mutation == "existing") Assert.Equal("preserve", await File.ReadAllTextAsync(path)); else Assert.False(File.Exists(path));
            }
        }
        finally { foreach (var file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
    }
    [Fact] public void LegacySettingsEnableChecksButOptOutAndOtherPreferencesRoundTrip()
    {
        var old = JsonSerializer.Deserialize<AppSettings>("""{"MouseSensitivity":0.84}""")!; Assert.True(old.AutoCheckUpdates);
        var saved = old with { AutoCheckUpdates = false, LastUpdateCheckUtc = DateTimeOffset.Parse("2026-10-31T00:00:00Z") };
        var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(saved))!.Normalize();
        Assert.False(restored.AutoCheckUpdates); Assert.Equal(saved.LastUpdateCheckUtc, restored.LastUpdateCheckUtc); Assert.Equal(.84, restored.MouseSensitivity);
    }
}
