using System.Net;
using System.Net.Http.Json;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class ClipboardBridgeTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public readonly X509Certificate2 Authority = ClipboardIdentity.CreateAuthority();
        public readonly ClipboardBridgeServer Server;
        public readonly HttpClient Client;
        public readonly Clock Clock = new();
        public string Text = "Windows · 日本語 · 中文 · 🙂\r\nline 2";
        public int Reads, Writes;
        public Fixture()
        {
            Server = new(IPAddress.Loopback, Authority, token => { token.ThrowIfCancellationRequested(); Reads++; return Task.FromResult(Text); },
                (text, token) => { token.ThrowIfCancellationRequested(); Writes++; Text = text; return Task.CompletedTask; }, "en", 0, 0, Clock);
            Client = new(new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false, SslOptions = Options() }) { BaseAddress = new(Server.Origin), Timeout = TimeSpan.FromSeconds(15) };
        }
        public SslClientAuthenticationOptions Options()
        {
            var policy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.NoCheck, DisableCertificateDownloads = true };
            policy.CustomTrustStore.Add(Authority);
            return new() { TargetHost = "127.0.0.1", CertificateChainPolicy = policy };
        }
        public Task<HttpResponseMessage> Post(string path, object? body = null, string? cookie = null, string? origin = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(JsonSerializer.Serialize(body ?? new { }), Encoding.UTF8, "application/json") };
            // Browsers send this exact media type. The server intentionally accepts no form or text/plain requests.
            request.Content.Headers.ContentType!.CharSet = null;
            request.Headers.Add("Origin", origin ?? Server.Origin);
            if (cookie is not null) request.Headers.Add("Cookie", cookie);
            return Client.SendAsync(request);
        }
        public async Task<string> Pair()
        {
            using var response = await PairResponse();
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
            Assert.Contains("Secure", cookie); Assert.Contains("HttpOnly", cookie); Assert.Contains("SameSite=Strict", cookie); Assert.Contains("Path=/api/", cookie);
            return cookie.Split(';')[0];
        }
        private async Task<HttpResponseMessage> PairResponse()
        {
            try { return await Post("/api/pair", new { code = Server.PairingCode }); }
            catch (HttpRequestException ex) { throw new InvalidOperationException(Server.LastHandshakeError ?? "TLS handshake failed without server details.", ex); }
        }
        public async Task<string> Raw(string header)
        {
            using var socket = new TcpClient(); await socket.ConnectAsync(IPAddress.Loopback, new Uri(Server.Origin).Port);
            await using var tls = new SslStream(socket.GetStream()); await tls.AuthenticateAsClientAsync(Options());
            await tls.WriteAsync(Encoding.ASCII.GetBytes(header)); await tls.FlushAsync();
            using var reader = new StreamReader(tls, Encoding.ASCII); return (await reader.ReadLineAsync())!;
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await Server.DisposeAsync(); Authority.Dispose(); }
    }
    [Fact] public async Task ValidatedTlsAndOneTimePairingPermitUnicodeTransferWithoutPolling()
    {
        await using var fixture = new Fixture(); Assert.Equal(0, fixture.Reads); Assert.Equal(0, fixture.Writes);
        using var page = await fixture.Client.GetAsync("/"); Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync(); Assert.Contains("Clipboard sharing", html); Assert.DoesNotContain("クリップボード共有", html);
        Assert.Contains("no-store", page.Headers.CacheControl!.ToString()); Assert.True(page.Headers.Contains("Content-Security-Policy"));
        var code = fixture.Server.PairingCode; var cookie = await fixture.Pair(); Assert.True(fixture.Server.Paired); Assert.Empty(fixture.Server.PairingCode);
        using var status = await fixture.Post("/api/status", cookie: cookie); Assert.Equal(HttpStatusCode.NoContent, status.StatusCode); Assert.Equal(0, fixture.Reads);
        using var read = await fixture.Post("/api/read", cookie: cookie); Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var payload = await read.Content.ReadFromJsonAsync<Dictionary<string, string>>(); Assert.Equal(fixture.Text, payload!["text"]); Assert.Equal(1, fixture.Reads);
        using var reused = await fixture.Post("/api/pair", new { code }); Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }
    [Theory] [InlineData("")][InlineData("日本語\n中文🙂\r\nhttps://example.org")][InlineData("<script>alert('x')</script> & <img src=x>")]
    public async Task OnlyAnExplicitAuthorizedWriteChangesClipboard(string text)
    {
        await using var fixture = new Fixture(); var cookie = await fixture.Pair();
        Assert.Equal(0, fixture.Writes);
        using var response = await fixture.Post("/api/write", new { text }, cookie); Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(text, fixture.Text); Assert.Equal(1, fixture.Writes); Assert.Equal(0, fixture.Reads);
    }
    [Theory] [InlineData("missing")][InlineData("invalid")][InlineData("origin")][InlineData("host")][InlineData("duplicate-cookie")]
    public async Task UnauthorizedOrCrossSiteRequestsNeverAccessClipboard(string mutation)
    {
        await using var fixture = new Fixture(); var cookie = await fixture.Pair();
        if (mutation == "host")
        {
            var responseLine = await fixture.Raw($"POST /api/read HTTP/1.1\r\nHost: untrusted.example\r\nOrigin: {fixture.Server.Origin}\r\nCookie: {cookie}\r\nContent-Type: application/json\r\nContent-Length: 2\r\n\r\n{{}}");
            Assert.Contains("403", responseLine); Assert.Equal(0, fixture.Reads); Assert.Equal(0, fixture.Writes); return;
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/read") { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        request.Content.Headers.ContentType!.CharSet = null;
        request.Headers.Add("Origin", mutation == "origin" ? "https://untrusted.example" : fixture.Server.Origin);
        if (mutation == "host") request.Headers.Host = "untrusted.example";
        if (mutation != "missing") request.Headers.Add("Cookie", mutation == "invalid" ? "UBClipboard=wrong" : mutation == "duplicate-cookie" ? cookie + "; " + cookie : cookie);
        using var response = await fixture.Client.SendAsync(request); Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(0, fixture.Reads); Assert.Equal(0, fixture.Writes);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
    [Fact] public async Task WrongCodesAreRateLimitedAndExpiredCodesCannotPair()
    {
        await using var fixture = new Fixture(); var wrong = fixture.Server.PairingCode == "000000" ? "999999" : "000000";
        for (var i = 0; i < 5; i++) { using var rejected = await fixture.Post("/api/pair", new { code = wrong }); Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode); }
        using var limited = await fixture.Post("/api/pair", new { code = fixture.Server.PairingCode }); Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        await fixture.Server.RefreshPairingAsync(); fixture.Clock.Now = fixture.Clock.Now.AddMinutes(6);
        using var expired = await fixture.Post("/api/pair", new { code = fixture.Server.PairingCode }); Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.False(fixture.Server.Paired); Assert.Equal(0, fixture.Reads);
    }
    [Fact] public async Task RefreshingCodeRevokesAlreadyAuthorizedSessions()
    {
        await using var fixture = new Fixture(); var previous = await fixture.Pair(); await fixture.Server.RefreshPairingAsync();
        using var old = await fixture.Post("/api/read", cookie: previous); Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        var current = await fixture.Pair(); Assert.NotEqual(current, previous);
        using var read = await fixture.Post("/api/read", cookie: current); Assert.Equal(HttpStatusCode.OK, read.StatusCode); Assert.Equal(1, fixture.Reads);
    }
    [Fact] public async Task PlainHttpServesOnlyPublicSetupAndCertificate()
    {
        await using var fixture = new Fixture(); using var http = new HttpClient();
        using var response = await http.GetAsync(fixture.Server.SetupUrl); var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(fixture.Server.CertificateFingerprint, html); Assert.Contains(fixture.Server.Origin, html); Assert.DoesNotContain("UBClipboard=", html);
        var certificate = await http.GetByteArrayAsync(fixture.Server.SetupUrl + "bridge.cer"); Assert.Equal(fixture.Authority.RawData, certificate);
        using var denied = await http.GetAsync(fixture.Server.SetupUrl + "api/read"); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(0, fixture.Reads); Assert.Equal(0, fixture.Writes);
    }
    [Theory] [InlineData("read")][InlineData("write")][InlineData("pair")]
    public async Task GetRequestsCannotReadWriteOrPair(string endpoint)
    {
        await using var fixture = new Fixture(); using var response = await fixture.Client.GetAsync("/api/" + endpoint);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode); Assert.Equal(0, fixture.Reads); Assert.Equal(0, fixture.Writes);
    }
    [Theory] [InlineData("large-text")][InlineData("utf8")][InlineData("null")][InlineData("wrong-type")]
    public async Task UnsupportedOrOversizeTextCannotOverwriteClipboard(string mutation)
    {
        await using var fixture = new Fixture(); var original = fixture.Text; var cookie = await fixture.Pair();
        var text = mutation == "utf8" ? new string('日', ClipboardBridgeServer.MaximumTextBytes / 3 + 1) : mutation == "null" ? "abc\0def" : new string('a', ClipboardBridgeServer.MaximumTextBytes + 1);
        using var response = await fixture.Post("/api/write", mutation == "wrong-type" ? new { text = 42 } : (object)new { text }, cookie);
        Assert.False(response.IsSuccessStatusCode); Assert.Equal(0, fixture.Writes); Assert.Equal(original, fixture.Text);
    }
    [Fact] public async Task TextSizeLimitAppliesToBothDirectionsAndBoundaryIsAllowed()
    {
        await using var fixture = new Fixture(); var cookie = await fixture.Pair(); fixture.Text = new string('a', ClipboardBridgeServer.MaximumTextBytes);
        using var valid = await fixture.Post("/api/read", cookie: cookie); Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        fixture.Text += "b"; using var large = await fixture.Post("/api/read", cookie: cookie); Assert.Equal(HttpStatusCode.RequestEntityTooLarge, large.StatusCode);
    }
    [Theory] [InlineData("duplicate")][InlineData("chunked")][InlineData("body")][InlineData("header")]
    public async Task BoundedParserRejectsAmbiguousFramingBeforeClipboardAccess(string mutation)
    {
        await using var fixture = new Fixture();
        var extra = mutation switch { "duplicate" => "Content-Length: 2\r\nContent-Length: 2\r\n", "chunked" => "Transfer-Encoding: chunked\r\n", "body" => $"Content-Length: {ClipboardBridgeServer.MaximumBodyBytes + 1}\r\n", _ => "X-Fill: " + new string('a', 8300) + "\r\n" };
        var response = await fixture.Raw($"POST /api/write HTTP/1.1\r\nHost: {new Uri(fixture.Server.Origin).Authority}\r\n{extra}\r\n");
        Assert.Contains(mutation == "body" ? "413" : mutation == "header" ? "431" : "400", response);
        Assert.Equal(0, fixture.Reads); Assert.Equal(0, fixture.Writes);
    }
    [Theory] [InlineData("0.0.0.0")][InlineData("8.8.8.8")][InlineData("::")][InlineData("224.0.0.1")]
    public void ListenerCannotBindToPublicOrWildcardAddresses(string value)
    {
        using var authority = ClipboardIdentity.CreateAuthority();
        Assert.Throws<ArgumentException>(() => new ClipboardBridgeServer(IPAddress.Parse(value), authority, _ => Task.FromResult(""), (_, _) => Task.CompletedTask));
    }
    [Fact] public async Task StopClosesBothListenersAndDoesNotReadClipboard()
    {
        await using var fixture = new Fixture(); await fixture.Server.DisposeAsync();
        await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Client.GetAsync("/"));
        using var http = new HttpClient(); await Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync(fixture.Server.SetupUrl));
        Assert.Equal(0, fixture.Reads); Assert.Equal(0, fixture.Writes);
    }
}
