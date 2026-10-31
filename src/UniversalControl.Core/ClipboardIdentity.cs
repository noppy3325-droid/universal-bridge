using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace UniversalControl.Core;

/// <summary>The private authority stays on Windows. Only its public certificate is exported to iPad.</summary>
public static class ClipboardIdentity
{
    public static X509Certificate2 CreateAuthority()
    {
        using var key = RSA.Create(2048);
        var name = "Universal Bridge Clipboard " + Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
        var request = new CertificateRequest("CN=" + name, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(5));
    }
    public static X509Certificate2 CreateServerCertificate(X509Certificate2 authority, IPAddress address)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Universal Bridge Clipboard", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        var san = new SubjectAlternativeNameBuilder(); san.AddIpAddress(address); request.CertificateExtensions.Add(san.Build());
        var until = DateTimeOffset.UtcNow.AddDays(365);
        if (until > authority.NotAfter.ToUniversalTime()) until = authority.NotAfter.ToUniversalTime();
        using var signed = request.Create(authority, DateTimeOffset.UtcNow.AddMinutes(-5), until, RandomNumberGenerator.GetBytes(16));
        using var leaf = signed.CopyWithPrivateKey(key);
        var exported = leaf.Export(X509ContentType.Pkcs12);
        try
        {
            // Schannel needs an OS-managed temporary key container. No certificate is trusted or installed.
            // Omitting PersistKeySet lets the runtime delete the container when this certificate is disposed.
            return X509CertificateLoader.LoadPkcs12(exported, null, X509KeyStorageFlags.UserKeySet);
        }
        finally { CryptographicOperations.ZeroMemory(exported); }
    }
    public static bool IsLocalAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return true;
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && (bytes[0] == 10 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
            bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 169 && bytes[1] == 254);
    }
}
