using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class LocalizationTests
{
    [Theory]
    [InlineData("ja-JP", "ja")]
    [InlineData("en-US", "en")]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("zh-TW", "zh-Hans")]
    [InlineData("de-DE", "en")]
    public void SystemLanguageFallsBackToSupportedCatalog(string system, string expected)
        => Assert.Equal(expected, L.Resolve("auto", CultureInfo.GetCultureInfo(system)));

    [Theory]
    [InlineData("ja")][InlineData("en")][InlineData("zh-Hans")]
    public void CatalogsHaveCompleteKeysAndCompatibleFormatArguments(string language)
    {
        var source = L.Catalog("ja"); var translated = L.Catalog(language);
        Assert.True(source.Count >= 247);
        Assert.Equal(source.Keys.Order(), translated.Keys.Order());
        foreach (var pair in source)
        {
            var text = translated[pair.Key]; Assert.False(string.IsNullOrWhiteSpace(text));
            var format = CompositeFormat.Parse(pair.Value); var localized = CompositeFormat.Parse(text);
            Assert.Equal(format.MinimumArgumentCount, localized.MinimumArgumentCount);
            Assert.Equal(Regex.Matches(pair.Value, @"\{[0-9]+(?::[^}]+)?\}").Select(m => m.Value).Order(), Regex.Matches(text, @"\{[0-9]+(?::[^}]+)?\}").Select(m => m.Value).Order());
            _ = string.Format(CultureInfo.GetCultureInfo(language), text, Enumerable.Repeat<object>(1.25, format.MinimumArgumentCount).ToArray());
            if (language == "en") Assert.DoesNotMatch("[一-龯ぁ-ゟァ-ヿ]", text);
            if (language == "zh-Hans") Assert.DoesNotMatch("[ぁ-ゟァ-ヿ]", text);
        }
        foreach (var sourceText in L.UiSources.Values) Assert.Contains(sourceText, translated.Keys);
    }
    [Fact]
    public void UnknownSourceFallsBackWithoutChangingExternalText()
    { Assert.Equal("OS error 0x1234", L.Get("OS error 0x1234", "en")); Assert.Equal("iPad", L.Get("iPad", "zh-Hans")); }

    [Theory]
    [InlineData("auto")][InlineData("ja")][InlineData("en")][InlineData("zh-Hans")]
    public void LanguagePreferenceRoundTripsAndPreservesShortcuts(string language)
    {
        var folder = Path.Combine(Path.GetTempPath(), "UniversalBridge-language-" + Guid.NewGuid());
        try
        {
            var store = new SettingsStore(folder); store.Save(new AppSettings { Language = language, MouseSensitivity = .84 });
            var settings = store.Load(); Assert.Equal(language, settings.Language); Assert.Equal(.84, settings.MouseSensitivity);
            Assert.Equal(ShortcutBinding.DefaultActivate, settings.ActivateShortcut); Assert.Equal(ShortcutBinding.DefaultReturn, settings.ReturnShortcut);
            File.WriteAllText(Path.Combine(folder, "settings.json"), "{\"SchemaVersion\":1,\"MouseSensitivity\":0.84}");
            Assert.Equal("auto", store.Load().Language); Assert.Equal(.84, store.Load().MouseSensitivity);
        }
        finally { Directory.Delete(folder, true); }
    }
    [Fact] public void InvalidLanguageUsesSystemPreference() => Assert.Equal("auto", new AppSettings { Language = "unsupported" }.Normalize().Language);
}
