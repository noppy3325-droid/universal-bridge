using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UniversalControl.Core;

public sealed record UpdateArchive(string File, long Size, string Sha256);
public sealed record UpdateManifest(int Schema, string Version, string Tag, string File, long Size, string Sha256, UpdateArchive? Archive = null);
public sealed record ReleaseUpdate(UpdateManifest Manifest, Uri DownloadUrl, Uri? ArchiveUrl = null);
public sealed record UpdateTransferProgress(double Fraction, long ReceivedBytes, long TotalBytes, double BytesPerSecond, bool Compressed);
public sealed record UpdateDownloadResult(bool Compressed, long NetworkBytes, int Requests, TimeSpan Elapsed);

/// <summary>Only this repository's published stable releases may supply executable updates.</summary>
public sealed partial class ReleaseUpdateClient(HttpClient http, TimeSpan? readTimeout = null)
{
    private readonly TimeSpan _readTimeout = readTimeout ?? TimeSpan.FromSeconds(15);
    public const string Repository = "https://github.com/noppy3325-droid/universal-bridge";
    public const string LatestApi = "https://api.github.com/repos/noppy3325-droid/universal-bridge/releases/latest";
    public const long MaximumExeBytes = 256 * 1024 * 1024;
    public const string ArchiveFile = "UniversalBridge-update.zip";
    public static Version ParseVersion(string value)
        => Regex.IsMatch(value ?? "", @"^\d+\.\d+\.\d+$") && Version.TryParse(value, out var version) ? version : throw new InvalidDataException("Invalid update version.");
    public static Uri AssetUrl(string tag, string name)
    {
        if (!Regex.IsMatch(tag ?? "", @"^v\d+\.\d+(?:\.\d+)?$") || name is not ("update.json" or "UniversalBridge.exe" or "SHA256SUMS.txt" or ArchiveFile))
            throw new InvalidDataException("Unexpected release asset.");
        return new($"{Repository}/releases/download/{tag}/{name}");
    }
    public async Task<ReleaseUpdate?> CheckAsync(Version current, CancellationToken cancellationToken)
    {
        using var response = await GetAsync(new(LatestApi), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        using var release = JsonDocument.Parse(await ReadLimitedAsync(response, 2 * 1024 * 1024, cancellationToken));
        var root = release.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        _ = AssetUrl(tag, "update.json");
        var assets = root.GetProperty("assets").EnumerateArray().ToArray();
        JsonElement Asset(string name)
        {
            var matches = assets.Where(a => a.GetProperty("name").GetString() == name).ToArray();
            if (matches.Length != 1 || matches[0].GetProperty("state").GetString() != "uploaded" ||
                matches[0].GetProperty("browser_download_url").GetString() != AssetUrl(tag, name).AbsoluteUri)
                throw new InvalidDataException("Update assets are incomplete or invalid.");
            return matches[0];
        }
        // Older regular releases do not have a manifest. They cannot be installed by this updater.
        if (!assets.Any(a => a.GetProperty("name").GetString() == "update.json")) return null;
        _ = Asset("update.json");
        using var manifestResponse = await GetAsync(AssetUrl(tag, "update.json"), cancellationToken);
        manifestResponse.EnsureSuccessStatusCode();
        var manifest = JsonSerializer.Deserialize<UpdateManifest>(await ReadLimitedAsync(manifestResponse, 16384, cancellationToken))
            ?? throw new InvalidDataException("Missing update manifest.");
        var version = ParseVersion(manifest.Version);
        if (manifest.Schema != 1 || manifest.Tag != tag || manifest.File != "UniversalBridge.exe" || manifest.Size is < 1024 or > MaximumExeBytes ||
            !Regex.IsMatch(manifest.Sha256 ?? "", "^[a-fA-F0-9]{64}$")) throw new InvalidDataException("Invalid update manifest.");
        if (version <= current) return null;
        var executable = Asset("UniversalBridge.exe"); _ = Asset("SHA256SUMS.txt");
        if (executable.GetProperty("size").GetInt64() != manifest.Size) throw new InvalidDataException("Update size mismatch.");
        if (executable.TryGetProperty("digest", out var digest) && digest.ValueKind == JsonValueKind.String &&
            !string.Equals(digest.GetString(), "sha256:" + manifest.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update digest mismatch.");
        using var sumsResponse = await GetAsync(AssetUrl(tag, "SHA256SUMS.txt"), cancellationToken);
        sumsResponse.EnsureSuccessStatusCode();
        var sums = System.Text.Encoding.UTF8.GetString(await ReadLimitedAsync(sumsResponse, 65536, cancellationToken));
        var hashes = sums.Split('\n').Select(line => Regex.Match(line.Trim().TrimStart('\uFEFF'), @"^([a-fA-F0-9]{64})\s+UniversalBridge\.exe$")).Where(m => m.Success).ToArray();
        if (hashes.Length != 1 || !hashes[0].Groups[1].Value.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update checksum manifest mismatch.");
        Uri? archiveUrl = null;
        if (manifest.Archive is { } archive)
        {
            if (archive.File != ArchiveFile || archive.Size is < 1024 or > MaximumExeBytes || !Regex.IsMatch(archive.Sha256 ?? "", "^[a-fA-F0-9]{64}$"))
                throw new InvalidDataException("Invalid update archive manifest.");
            var asset = Asset(ArchiveFile);
            if (asset.GetProperty("size").GetInt64() != archive.Size || asset.TryGetProperty("digest", out var archiveDigest) && archiveDigest.ValueKind == JsonValueKind.String &&
                !string.Equals(archiveDigest.GetString(), "sha256:" + archive.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update archive asset mismatch.");
            var archiveHashes = sums.Split('\n').Select(line => Regex.Match(line.Trim().TrimStart('\uFEFF'), @"^([a-fA-F0-9]{64})\s+UniversalBridge-update\.zip$")).Where(m => m.Success).ToArray();
            if (archiveHashes.Length != 1 || !archiveHashes[0].Groups[1].Value.Equals(archive.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update archive checksum mismatch.");
            archiveUrl = AssetUrl(tag, ArchiveFile);
        }
        return new(manifest, AssetUrl(tag, "UniversalBridge.exe"), archiveUrl);
    }
    public static async Task VerifyFileAsync(string path, long size, string hash, CancellationToken cancellationToken = default)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        if (input.Length != size || size is < 1024 or > MaximumExeBytes) throw new InvalidDataException("Update file size mismatch.");
        var actual = await SHA256.HashDataAsync(input, cancellationToken);
        if (!Convert.ToHexString(actual).Equals(hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update checksum mismatch.");
    }
    private async Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken cancellationToken, long offset = 0, bool binary = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("UniversalBridge-Updater/1.0");
        request.Headers.Accept.ParseAdd(binary ? "application/octet-stream" : "application/vnd.github+json");
        if (offset > 0) { request.Headers.Range = new(offset, null); request.Headers.CacheControl = new() { NoCache = true }; }
        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        var final = response.RequestMessage?.RequestUri ?? uri;
        if (final.Scheme != "https" || final.Host is not ("api.github.com" or "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com"))
        { response.Dispose(); throw new InvalidDataException("Unexpected update redirect."); }
        return response;
    }
    private static async Task<byte[]> ReadLimitedAsync(HttpResponseMessage response, int limit, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Update metadata is too large.");
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken); using var output = new MemoryStream();
        var buffer = new byte[8192]; int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken)) != 0)
        { if (output.Length + count > limit) throw new InvalidDataException("Update metadata is too large."); await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken); }
        return output.ToArray();
    }
}
