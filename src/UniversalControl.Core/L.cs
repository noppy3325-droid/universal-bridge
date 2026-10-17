using System.Globalization;
using System.Text.Json;

namespace UniversalControl.Core;

/// <summary>Gettext-style source messages, embedded for fully offline use.</summary>
public static class L
{
    private static readonly IReadOnlyDictionary<string, Dictionary<string, string>> Catalogs = Load();
    private static string _language = Resolve("auto");
    public static string Language => Volatile.Read(ref _language);
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Language);
    public static string Resolve(string? language, CultureInfo? systemCulture = null)
    {
        if (language is "ja" or "en" or "zh-Hans") return language;
        var system = (systemCulture ?? CultureInfo.InstalledUICulture).TwoLetterISOLanguageName;
        return system == "ja" ? "ja" : system == "zh" ? "zh-Hans" : "en";
    }
    public static void SetLanguage(string? language) => Volatile.Write(ref _language, Resolve(language));
    public static string T(string source) => Get(source, Language);
    public static string F(string source, params object?[] arguments) => string.Format(Culture, T(source), arguments);
    public static string Get(string source, string language) => Catalogs.TryGetValue(language, out var catalog) && catalog.TryGetValue(source, out var value) ? value : source;
    public static IReadOnlyDictionary<string, string> Catalog(string language) => Catalogs[language];
    public static IReadOnlyDictionary<string, string> UiSources { get; } = ReadCatalog("ui");
    private static Dictionary<string, string> ReadCatalog(string language)
    {
        using var stream = typeof(L).Assembly.GetManifestResourceStream($"UniversalControl.Core.Localization.{language}.json")
            ?? throw new InvalidOperationException("Missing embedded language catalog: " + language);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? throw new InvalidDataException(language);
    }
    private static IReadOnlyDictionary<string, Dictionary<string, string>> Load()
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var language in new[] { "ja", "en", "zh-Hans" })
        {
            result[language] = ReadCatalog(language);
        }
        return result;
    }
}
