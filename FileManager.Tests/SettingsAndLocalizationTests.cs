using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using FileManager.Models;
using FileManager.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileManager.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly SettingsService _settings = new(NullLogger<SettingsService>.Instance);

    public SettingsServiceTests() => Preferences.Clear();

    public void Dispose() => Preferences.Clear();

    [Fact]
    public void Defaults_WhenNothingIsStored()
    {
        Assert.Equal(LocalizationService.SystemLanguage, _settings.Language);
        Assert.False(_settings.ShowHiddenFiles);
        Assert.True(_settings.ConfirmDelete);
        Assert.Equal(SortMode.NameAscending, _settings.Sort);
    }

    [Fact]
    public void Values_RoundTrip()
    {
        _settings.Language = "es";
        _settings.ShowHiddenFiles = true;
        _settings.ConfirmDelete = false;
        _settings.Sort = SortMode.SizeDescending;

        var reloaded = new SettingsService(NullLogger<SettingsService>.Instance);
        Assert.Equal("es", reloaded.Language);
        Assert.True(reloaded.ShowHiddenFiles);
        Assert.False(reloaded.ConfirmDelete);
        Assert.Equal(SortMode.SizeDescending, reloaded.Sort);
    }

    [Fact]
    public void Language_NullMeansFollowTheSystem()
    {
        _settings.Language = "en";
        _settings.Language = null!;
        Assert.Equal(LocalizationService.SystemLanguage, _settings.Language);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(int.MaxValue)]
    public void Sort_CorruptStoredValue_FallsBackToName(int stored)
    {
        Preferences.Set("sort_mode", stored);
        Assert.Equal(SortMode.NameAscending, _settings.Sort);
    }
}

public sealed class LocalizationServiceTests : IDisposable
{
    private readonly CultureInfo _ui = CultureInfo.CurrentUICulture;
    private readonly CultureInfo? _defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? _defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _ui;
        CultureInfo.DefaultThreadCurrentCulture = _defaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _defaultUiCulture;
    }

    private sealed class FakeSettings : ISettingsService
    {
        public string Language { get; set; } = "";
        public bool ShowHiddenFiles { get; set; }
        public bool ConfirmDelete { get; set; }
        public SortMode Sort { get; set; }
    }

    private static LocalizationService Create(string language) =>
        new(new FakeSettings { Language = language }, NullLogger<LocalizationService>.Instance);

    private static Dictionary<string, string> Table(string name) =>
        (Dictionary<string, string>)typeof(LocalizationService)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

    [Fact]
    public void SpanishAndEnglish_HaveExactlyTheSameKeys()
    {
        var en = Table("English").Keys.ToHashSet();
        var es = Table("Spanish").Keys.ToHashSet();

        Assert.Empty(en.Except(es));
        Assert.Empty(es.Except(en));
        Assert.True(en.Count > 100);
    }

    [Fact]
    public void Translations_AreNotEmpty_AndKeepTheSamePlaceholders()
    {
        var en = Table("English");
        var es = Table("Spanish");
        var placeholder = new Regex(@"\{\d+(:[^}]*)?\}");

        foreach (var (key, english) in en)
        {
            var spanish = es[key];
            Assert.False(string.IsNullOrWhiteSpace(english), $"en:{key}");
            Assert.False(string.IsNullOrWhiteSpace(spanish), $"es:{key}");

            var enHoles = placeholder.Matches(english).Select(m => m.Value).Order();
            var esHoles = placeholder.Matches(spanish).Select(m => m.Value).Order();
            Assert.True(enHoles.SequenceEqual(esHoles), $"Placeholders differ for {key}");
        }
    }

    [Fact]
    public void ExplicitSpanish_TranslatesAndSetsCulture()
    {
        var l = Create("es");

        Assert.Equal("es", l.CurrentLanguage);
        Assert.Equal("es", l.CurrentCulture.Name);
        Assert.Equal(Table("Spanish")["Settings"], l["Settings"]);
        Assert.Equal("es", CultureInfo.DefaultThreadCurrentCulture!.Name);
    }

    [Fact]
    public void UnsupportedLanguage_FallsBackToEnglish()
    {
        var l = Create("fr");
        Assert.Equal("en", l.CurrentLanguage);
        Assert.Equal("Settings", l["Settings"]);
    }

    [Theory]
    [InlineData("es-ES", "es")]
    [InlineData("en-GB", "en")]
    [InlineData("de-DE", "en")]
    public void SystemLanguage_IsFollowedWhenSupported(string system, string expected)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(system);
        Assert.Equal(expected, Create(LocalizationService.SystemLanguage).CurrentLanguage);
    }

    [Fact]
    public void UnknownKey_ReturnsTheKey() =>
        Assert.Equal("NoSuchKey", Create("es")["NoSuchKey"]);

    [Fact]
    public void SetLanguage_RaisesChangedOnlyWhenItChanges()
    {
        var l = Create("en");
        var raised = 0;
        l.LanguageChanged += (_, _) => raised++;

        l.SetLanguage("en");
        l.SetLanguage("es");
        l.SetLanguage("es");

        Assert.Equal(1, raised);
        Assert.Equal("es", l.CurrentLanguage);
    }

    [Fact]
    public void MissingSpanishTranslation_FallsBackToEnglish()
    {
        var es = Table("Spanish");
        var key = Table("English").Keys.First();
        var saved = es[key];
        es.Remove(key);
        try
        {
            Assert.Equal(Table("English")[key], Create("es")[key]);
        }
        finally
        {
            es[key] = saved;
        }
    }
}
