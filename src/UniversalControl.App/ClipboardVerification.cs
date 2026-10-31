using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using UniversalControl.Core;
using UniversalControl.Windows;

namespace UniversalControl.App;

internal static class ClipboardVerification
{
    public static async Task<int> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        try
        {
            using var authority = ClipboardDesktop.LoadAuthority(directory);
            using var restored = ClipboardDesktop.LoadAuthority(directory);
            if (!authority.RawData.AsSpan().SequenceEqual(restored.RawData) || !restored.HasPrivateKey) throw new InvalidOperationException("Protected certificate persistence failed.");
            var value = "private-fixture · 日本語 · 中文 · 🙂\r\nsecond line"; var expected = value;
            await using var server = new ClipboardBridgeServer(IPAddress.Loopback, authority, _ => Task.FromResult(value),
                (text, token) => { token.ThrowIfCancellationRequested(); value = text; return Task.CompletedTask; }, "ja", 0, 0);
            var policy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.NoCheck, DisableCertificateDownloads = true };
            policy.CustomTrustStore.Add(authority);
            using var http = new HttpClient(new SocketsHttpHandler { UseCookies = false, SslOptions = new SslClientAuthenticationOptions { CertificateChainPolicy = policy } }) { BaseAddress = new(server.Origin) };
            async Task<HttpResponseMessage> Post(string route, object body, string? cookie = null)
            {
                var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
                request.Content.Headers.ContentType!.CharSet = null; request.Headers.Add("Origin", server.Origin);
                if (cookie is not null) request.Headers.Add("Cookie", cookie);
                return await http.SendAsync(request);
            }
            using var denied = await Post("/api/read", new { }); if (denied.StatusCode != HttpStatusCode.Unauthorized) throw new InvalidOperationException("Unauthenticated read accepted.");
            using var pair = await Post("/api/pair", new { code = server.PairingCode }); pair.EnsureSuccessStatusCode();
            var cookie = pair.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
            using var read = await Post("/api/read", new { }, cookie); read.EnsureSuccessStatusCode();
            if ((await read.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["text"] != expected) throw new InvalidOperationException("Unicode read failed.");
            expected = "iPad fixture → Windows\n日本語🙂";
            using var write = await Post("/api/write", new { text = expected }, cookie); write.EnsureSuccessStatusCode();
            if (value != expected) throw new InvalidOperationException("Unicode write failed.");
            await server.RefreshPairingAsync();
            using var revoked = await Post("/api/read", new { }, cookie); if (revoked.StatusCode != HttpStatusCode.Unauthorized) throw new InvalidOperationException("Old session remained authorized.");
            using var bootstrap = new HttpClient();
            if (!(await bootstrap.GetByteArrayAsync(server.SetupUrl + "bridge.cer")).AsSpan().SequenceEqual(authority.RawData)) throw new InvalidOperationException("Public certificate differs.");
            await using var transport = new BleHidTransport(); await using var reports = new ReportDispatcher(transport);
            var receiver = await NativeInput.ProbeTouchpadReceiverAsync(new InputRouter(transport, reports));
            if (receiver == false) throw new InvalidOperationException("Hidden gesture receiver could not register without changing foreground.");
            var preview = Path.Combine(directory, "web-preview"); Directory.CreateDirectory(preview);
            foreach (var language in new[] { "ja", "en", "zh-Hans" })
                await File.WriteAllBytesAsync(Path.Combine(preview, language + ".html"), ClipboardWeb.Get("/", language).Body);
            await File.WriteAllBytesAsync(Path.Combine(preview, "bridge.css"), ClipboardWeb.Get("/bridge.css", "en").Body);
            await File.WriteAllBytesAsync(Path.Combine(preview, "bridge.js"), ClipboardWeb.Get("/bridge.js", "en").Body);
            await File.WriteAllTextAsync(Path.Combine(directory, "clipboard-verification.json"), JsonSerializer.Serialize(new { Success = true, RealTlsTransport = true,
                ProtectedCertificateRoundTrip = true, CustomRootValidation = true, UnicodeBidirectionalRoundTrip = true, UnauthenticatedReadRejected = true,
                SessionRevocation = true, PublicCertificateOnly = true, LoopbackOnly = true, SystemClipboardTouched = false,
                GestureReceiverSupported = receiver is not null, HiddenGestureReceiverVerified = receiver,
                InputCaptured = false, BluetoothAdvertised = false, SafariDeviceTested = false }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex) { await File.WriteAllTextAsync(Path.Combine(directory, "clipboard-verification-error.txt"), ex.ToString()); return 2; }
    }
}
