using System.Net;
using System.Text;

namespace UniversalControl.Core;

public static class ClipboardWeb
{
    private static string Read(string name)
    {
        using var stream = typeof(ClipboardWeb).Assembly.GetManifestResourceStream("UniversalControl.Core.ClipboardWeb." + name)!;
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
    public static (byte[] Body, string Type) Get(string path, string language) => path switch
    {
        "/bridge.js" => (Encoding.UTF8.GetBytes(Read("bridge.js")), "text/javascript; charset=utf-8"),
        "/bridge.css" => (Encoding.UTF8.GetBytes(Read("bridge.css")), "text/css; charset=utf-8"),
        _ => (Encoding.UTF8.GetBytes(Translate(Read("index.html"), language)), "text/html; charset=utf-8")
    };
    private static string Translate(string html, string language)
        => System.Text.RegularExpressions.Regex.Replace(html, @"\[\[([^\]]+)\]\]", match => WebUtility.HtmlEncode(L.Get(match.Groups[1].Value, language)));
    public static string Setup(string language, string origin, string authority, string fingerprint)
        => Translate(Read("setup.html"), language).Replace("{{origin}}", WebUtility.HtmlEncode(origin), StringComparison.Ordinal)
            .Replace("{{authority}}", WebUtility.HtmlEncode(authority), StringComparison.Ordinal).Replace("{{fingerprint}}", fingerprint, StringComparison.Ordinal);
}
