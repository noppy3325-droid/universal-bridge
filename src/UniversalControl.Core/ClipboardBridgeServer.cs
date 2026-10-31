using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace UniversalControl.Core;

/// <summary>Opt-in local HTTPS text transfer. No clipboard polling, persistent text, or network logging.</summary>
public sealed class ClipboardBridgeServer : IAsyncDisposable
{
    public const int MaximumTextBytes = 65_536;
    public const int MaximumBodyBytes = 512 * 1024;
    private readonly TcpListener _https, _setup;
    private readonly X509Certificate2 _certificate;
    private readonly byte[] _publicAuthority;
    private readonly Func<CancellationToken, Task<string>> _read;
    private readonly Func<string, CancellationToken, Task> _write;
    private readonly TimeProvider _clock;
    private readonly string _language;
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _slots = new(12), _access = new(1);
    private readonly ConcurrentDictionary<TcpClient, Task> _clients = new();
    private readonly Task[] _listeners;
    private string _code = "", _session = "";
    private DateTimeOffset _expires;
    private int _failures;
    private bool _disposed;
    public string Origin { get; }
    public string SetupUrl { get; }
    public string CertificateFingerprint { get; }
    public string AuthorityName { get; }
    public string? LastHandshakeError { get; private set; }
    public string PairingCode { get { lock (_clients) return _code; } }
    public bool Paired { get { lock (_clients) return _session.Length != 0; } }

    public ClipboardBridgeServer(IPAddress address, X509Certificate2 authority,
        Func<CancellationToken, Task<string>> read, Func<string, CancellationToken, Task> write,
        string language = "en", int httpsPort = 43183, int setupPort = 43182, TimeProvider? clock = null)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork || !ClipboardIdentity.IsLocalAddress(address))
            throw new ArgumentException("A private local IPv4 address is required.", nameof(address));
        _read = read; _write = write; _clock = clock ?? TimeProvider.System; _language = L.Resolve(language);
        _publicAuthority = authority.Export(X509ContentType.Cert);
        CertificateFingerprint = Convert.ToHexString(SHA256.HashData(_publicAuthority));
        AuthorityName = authority.GetNameInfo(X509NameType.SimpleName, false);
        _certificate = ClipboardIdentity.CreateServerCertificate(authority, address);
        _https = new(address, httpsPort); _setup = new(address, setupPort);
        try { _https.Start(12); _setup.Start(12); }
        catch { _https.Stop(); _setup.Stop(); _certificate.Dispose(); throw; }
        Origin = $"https://{address}:{((IPEndPoint)_https.LocalEndpoint).Port}";
        SetupUrl = $"http://{address}:{((IPEndPoint)_setup.LocalEndpoint).Port}/";
        ResetCredentials();
        _listeners = [AcceptAsync(_https, true), AcceptAsync(_setup, false)];
    }
    public async Task RefreshPairingAsync()
    {
        await _access.WaitAsync(_stop.Token);
        try { ResetCredentials(); } finally { _access.Release(); }
    }
    private void ResetCredentials()
    {
        lock (_clients)
        {
            _code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
            _expires = _clock.GetUtcNow().AddMinutes(5); _failures = 0; _session = "";
        }
    }
    private async Task AcceptAsync(TcpListener listener, bool secure)
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(_stop.Token);
                if (!_slots.Wait(0)) { client.Dispose(); continue; }
                // Track before starting so disposal cannot miss a freshly accepted connection.
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _clients[client] = completion.Task;
                _ = ServeTrackedAsync(client, secure, completion);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
    }
    private async Task ServeTrackedAsync(TcpClient client, bool secure, TaskCompletionSource completion)
    {
        try { await ServeAsync(client, secure); }
        catch (AuthenticationException ex) { LastHandshakeError = ex.ToString(); }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
        finally { client.Dispose(); _clients.TryRemove(client, out _); _slots.Release(); completion.SetResult(); }
    }
    private async Task ServeAsync(TcpClient client, bool secure)
    {
        if (client.Client.RemoteEndPoint is not IPEndPoint remote || !ClipboardIdentity.IsLocalAddress(remote.Address)) return;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var token = timeout.Token;
        await using var stream = secure ? new SslStream(client.GetStream(), false) : (Stream)client.GetStream();
        if (stream is SslStream tls)
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = _certificate,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13, ApplicationProtocols = [SslApplicationProtocol.Http11] }, token);
        try
        {
            var request = await ReadRequestAsync(stream, token);
            var expectedHost = new Uri(secure ? Origin : SetupUrl).Authority;
            if (!request.Headers.TryGetValue("Host", out var host) || host != expectedHost) { await ReplyAsync(stream, 403, token); return; }
            if (!secure)
            {
                // The HTTP bootstrap can serve only public setup information and the public certificate.
                if (request.Method != "GET") { await ReplyAsync(stream, 405, token); return; }
                if (request.Path == "/bridge.cer") await ReplyAsync(stream, 200, token, _publicAuthority, "application/x-x509-ca-cert", "Content-Disposition: attachment; filename=UniversalBridge-Clipboard.cer\r\n");
                else if (request.Path == "/bridge.css") await ReplyAsync(stream, 200, token, ClipboardWeb.Get("/bridge.css", _language).Body, "text/css; charset=utf-8");
                else if (request.Path == "/") await ReplyAsync(stream, 200, token, Encoding.UTF8.GetBytes(ClipboardWeb.Setup(_language, Origin, AuthorityName, CertificateFingerprint)), "text/html; charset=utf-8");
                else await ReplyAsync(stream, 404, token);
                return;
            }
            if (request.Method == "GET" && request.Path is "/" or "/bridge.css" or "/bridge.js")
            {
                var (body, type) = ClipboardWeb.Get(request.Path, _language);
                await ReplyAsync(stream, 200, token, body, type); return;
            }
            if (request.Method != "POST") { await ReplyAsync(stream, 405, token); return; }
            if (!request.Headers.TryGetValue("Origin", out var origin) || origin != Origin) { await ReplyAsync(stream, 403, token); return; }
            if (!request.Headers.TryGetValue("Content-Type", out var contentType) || !contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
            { await ReplyAsync(stream, 415, token); return; }
            using var json = JsonDocument.Parse(request.Body);
            await _access.WaitAsync(token);
            try
            {
                if (request.Path == "/api/pair")
                {
                    if (_failures >= 5) { await ReplyAsync(stream, 429, token); return; }
                    var code = json.RootElement.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                    if (_clock.GetUtcNow() >= _expires || string.IsNullOrEmpty(_code) || code != _code)
                    { _failures++; await ReplyAsync(stream, 401, token); return; }
                    lock (_clients) { _session = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); _code = ""; }
                    await ReplyAsync(stream, 204, token, extra: $"Set-Cookie: UBClipboard={_session}; Path=/api/; Secure; HttpOnly; SameSite=Strict\r\n"); return;
                }
                var session = request.Headers.GetValueOrDefault("Cookie", "").Split(';').Select(s => s.Trim()).SingleOrDefault(s => s.StartsWith("UBClipboard=", StringComparison.Ordinal));
                var supplied = session is null ? "" : session[12..];
                if (_session.Length == 0 || supplied.Length != _session.Length || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(supplied), Encoding.ASCII.GetBytes(_session)))
                { await ReplyAsync(stream, 401, token); return; }
                token.ThrowIfCancellationRequested();
                if (request.Path == "/api/status") { await ReplyAsync(stream, 204, token); return; }
                if (request.Path == "/api/read")
                {
                    var text = await _read(token); token.ThrowIfCancellationRequested();
                    if (Encoding.UTF8.GetByteCount(text) > MaximumTextBytes) { await ReplyAsync(stream, 413, token); return; }
                    await ReplyAsync(stream, 200, token, JsonSerializer.SerializeToUtf8Bytes(new { text }), "application/json; charset=utf-8"); return;
                }
                if (request.Path == "/api/write")
                {
                    if (!json.RootElement.TryGetProperty("text", out var value) || value.ValueKind != JsonValueKind.String) { await ReplyAsync(stream, 400, token); return; }
                    var text = value.GetString()!;
                    if (Encoding.UTF8.GetByteCount(text) > MaximumTextBytes || text.Contains('\0')) { await ReplyAsync(stream, 413, token); return; }
                    await _write(text, token); token.ThrowIfCancellationRequested(); await ReplyAsync(stream, 204, token); return;
                }
                await ReplyAsync(stream, 404, token);
            }
            finally { _access.Release(); }
        }
        catch (RequestException ex) { await ReplyAsync(stream, ex.Status, token); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException) { await ReplyAsync(stream, 400, token); }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException) { await ReplyAsync(stream, 503, token); }
        catch (Exception ex) when (ex is not IOException and not OperationCanceledException and not ObjectDisposedException) { await ReplyAsync(stream, 503, token); }
    }
    private sealed record Request(string Method, string Path, Dictionary<string, string> Headers, byte[] Body);
    private sealed class RequestException(int status) : Exception { public int Status { get; } = status; }
    private static async Task<Request> ReadRequestAsync(Stream stream, CancellationToken token)
    {
        var header = new List<byte>(); var single = new byte[1];
        while (header.Count < 8192)
        {
            if (await stream.ReadAsync(single, token) == 0) throw new RequestException(400);
            header.Add(single[0]);
            if (header.Count >= 4 && header[^4] == 13 && header[^3] == 10 && header[^2] == 13 && header[^1] == 10) break;
        }
        if (header.Count >= 8192) throw new RequestException(431);
        if (header.Any(b => b > 126 || b < 32 && b is not (13 or 10))) throw new RequestException(400);
        var lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n", StringSplitOptions.None);
        var first = lines[0].Split(' ');
        if (first.Length != 3 || first[2] != "HTTP/1.1" || !first[1].StartsWith('/')) throw new RequestException(400);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1).Where(l => l.Length != 0))
        {
            var colon = line.IndexOf(':');
            if (colon < 1 || line[..colon].Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') || !headers.TryAdd(line[..colon], line[(colon + 1)..].Trim())) throw new RequestException(400);
        }
        if (headers.ContainsKey("Transfer-Encoding") || headers.ContainsKey("Expect")) throw new RequestException(400);
        var length = 0;
        if (headers.TryGetValue("Content-Length", out var count) && (!int.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out length) || length < 0)) throw new RequestException(400);
        if (length > MaximumBodyBytes) throw new RequestException(413);
        var body = new byte[length]; await stream.ReadExactlyAsync(body, token);
        return new(first[0], first[1], headers, body);
    }
    private static async Task ReplyAsync(Stream stream, int status, CancellationToken token, byte[]? body = null, string type = "text/plain; charset=utf-8", string extra = "")
    {
        body ??= [];
        var headers = $"HTTP/1.1 {status} Response\r\nContent-Length: {body.Length}\r\nContent-Type: {type}\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nReferrer-Policy: no-referrer\r\nContent-Security-Policy: default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; form-action 'none'; base-uri 'none'; frame-ancestors 'none'\r\n{extra}\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), token); await stream.WriteAsync(body, token); await stream.FlushAsync(token);
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return; _disposed = true;
        _stop.Cancel(); _https.Stop(); _setup.Stop();
        await Task.WhenAll(_listeners);
        var clients = _clients.ToArray(); foreach (var client in clients) client.Key.Dispose();
        await Task.WhenAll(clients.Select(c => c.Value));
        lock (_clients) { _code = ""; _session = ""; }
        _certificate.Dispose(); _stop.Dispose(); _slots.Dispose(); _access.Dispose();
    }
}
