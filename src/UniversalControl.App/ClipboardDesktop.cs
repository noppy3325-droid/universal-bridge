using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using UniversalControl.Core;
using TextDataFormat = System.Windows.TextDataFormat;

namespace UniversalControl.App;

internal static class ClipboardDesktop
{
    public sealed record AddressChoice(IPAddress Address, string Name) { public override string ToString() => $"{Address} · {Name}"; }
    public static AddressChoice[] Addresses() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .SelectMany(n => n.GetIPProperties().UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && ClipboardIdentity.IsLocalAddress(a.Address))
            .Select(a => new AddressChoice(a.Address, n.Name))).DistinctBy(a => a.Address).ToArray();
    public static X509Certificate2 LoadAuthority(string dataDirectory)
    {
        var path = Path.Combine(dataDirectory, "clipboard-authority.bin");
        var entropy = Encoding.UTF8.GetBytes("UniversalBridge.ClipboardAuthority.v1");
        if (File.Exists(path))
        {
            var decoded = ProtectedData.Unprotect(File.ReadAllBytes(path), entropy, DataProtectionScope.CurrentUser);
            try
            {
                var certificate = X509CertificateLoader.LoadPkcs12(decoded, null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
                if (certificate.HasPrivateKey && certificate.NotAfter.ToUniversalTime() > DateTime.UtcNow.AddDays(1)) return certificate;
                certificate.Dispose();
            }
            finally { CryptographicOperations.ZeroMemory(decoded); }
        }
        using var authority = ClipboardIdentity.CreateAuthority();
        var exported = authority.Export(X509ContentType.Pkcs12);
        try
        {
            Directory.CreateDirectory(dataDirectory);
            var encrypted = ProtectedData.Protect(exported, entropy, DataProtectionScope.CurrentUser);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temporary, encrypted); File.Move(temporary, path, true); }
            finally { File.Delete(temporary); }
            return X509CertificateLoader.LoadPkcs12(exported, null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
        }
        finally { CryptographicOperations.ZeroMemory(exported); }
    }
    private static async Task<T> AccessAsync<T>(Func<T> action, Dispatcher dispatcher, CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { return await dispatcher.InvokeAsync(() => { token.ThrowIfCancellationRequested(); return action(); }, DispatcherPriority.Background, token); }
            catch (ExternalException) when (attempt < 4) { await Task.Delay(40, token); }
        }
    }
    public static Task<string> ReadAsync(Dispatcher dispatcher, CancellationToken token) => AccessAsync(() => System.Windows.Clipboard.ContainsText(TextDataFormat.UnicodeText)
        ? System.Windows.Clipboard.GetText(TextDataFormat.UnicodeText) : "", dispatcher, token);
    public static async Task WriteAsync(string text, Dispatcher dispatcher, CancellationToken token)
        => await AccessAsync(() => { if (text.Length == 0) System.Windows.Clipboard.Clear(); else System.Windows.Clipboard.SetText(text, TextDataFormat.UnicodeText); return true; }, dispatcher, token);
}
