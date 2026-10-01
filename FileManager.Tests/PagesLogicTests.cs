using System.Globalization;
using System.Net;
using FileManager.Services;
using FileManager.Tests.Fakes;
using FileManager.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileManager.Tests;

/// <summary>Restaura la cultura por defecto que LocalizationService cambia al elegir idioma.</summary>
public abstract class CultureSafeTests : IDisposable
{
    private readonly CultureInfo? _culture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? _uiCulture = CultureInfo.DefaultThreadCurrentUICulture;

    protected readonly MemorySettings Settings = new();
    protected readonly LocalizationService L;
    protected readonly ScriptedDialogs Dialogs = new();

    protected CultureSafeTests() => L = new LocalizationService(Settings, NullLogger<LocalizationService>.Instance);

    public void Dispose()
    {
        CultureInfo.DefaultThreadCurrentCulture = _culture;
        CultureInfo.DefaultThreadCurrentUICulture = _uiCulture;
    }
}

public sealed class SettingsViewModelTests : CultureSafeTests
{
    private readonly FakePermissions _permissions = new();
    private readonly SettingsViewModel _vm;

    public SettingsViewModelTests() =>
        _vm = new SettingsViewModel(Settings, L, _permissions, NullLogger.Instance);

    [Fact]
    public void ReflectsTheStoredPreferences()
    {
        Settings.ShowHiddenFiles = true;
        Settings.ConfirmDelete = false;

        Assert.True(_vm.ShowHiddenFiles);
        Assert.False(_vm.ConfirmDelete);
        Assert.False(_vm.IsSpanish);
    }

    [Fact]
    public void Toggles_Save_ExceptWhileLoading()
    {
        _vm.Loading = true;
        _vm.SetShowHiddenFiles(true);
        _vm.SetConfirmDelete(false);
        Assert.False(Settings.ShowHiddenFiles);
        Assert.True(Settings.ConfirmDelete);

        _vm.Loading = false;
        _vm.SetShowHiddenFiles(true);
        _vm.SetConfirmDelete(false);
        Assert.True(Settings.ShowHiddenFiles);
        Assert.False(Settings.ConfirmDelete);
    }

    [Fact]
    public void SetLanguage_ChangesAndSaves_OnlyWhenDifferent()
    {
        Assert.False(_vm.SetLanguage("en"));

        Assert.True(_vm.SetLanguage("es"));
        Assert.Equal("es", Settings.Language);
        Assert.Equal("es", L.CurrentLanguage);
        Assert.True(_vm.IsSpanish);

        _vm.Loading = true;
        Assert.False(_vm.SetLanguage("en"));
        Assert.Equal("es", L.CurrentLanguage);
    }

    [Fact]
    public void PermissionState_FollowsTheSystem()
    {
        Assert.True(_vm.PermissionGranted);
        Assert.Equal(L["SettingsPermissionOk"], _vm.PermissionStateText);

        _permissions.HasFullAccess = false;
        Assert.False(_vm.PermissionGranted);
        Assert.Equal(L["SettingsPermissionKo"], _vm.PermissionStateText);
    }

    [Fact]
    public async Task RequestPermission_OpensTheSettings_OrShowsTheError()
    {
        await _vm.RequestPermissionAsync(Dialogs);
        Assert.Equal(1, _permissions.Requests);
        Assert.Empty(Dialogs.Calls);

        _permissions.RequestError = new InvalidOperationException("blocked");
        await _vm.RequestPermissionAsync(Dialogs);
        Assert.Equal("blocked", Dialogs.Last.Message);
        Assert.Equal(L["Error"], Dialogs.Last.Title);
    }
}

public sealed class AboutViewModelTests : CultureSafeTests
{
    private readonly FakeEnvironment _env = new() { VersionString = "2026.10.01.0" };
    private readonly AboutViewModel _vm;

    public AboutViewModelTests() => _vm = new AboutViewModel(L, Settings, _env, NullLogger.Instance);

    [Fact]
    public void VersionText_ShowsTheInstalledVersion() =>
        Assert.Equal(string.Format(L["AboutVersion"], "2026.10.01.0"), _vm.VersionText);

    [Fact]
    public void SetLanguage_ChangesAndSaves_OnlyWhenDifferent()
    {
        Assert.False(_vm.SetLanguage("en"));
        Assert.True(_vm.SetLanguage("es"));
        Assert.True(_vm.IsSpanish);
        Assert.Equal("es", Settings.Language);
    }

    [Fact]
    public async Task Contact_ComposesAnEmailToTheAuthor()
    {
        await _vm.ContactAsync(Dialogs);

        Assert.Equal((L["EmailSubject"], AboutViewModel.ContactEmail), Assert.Single(_env.Emails));
        Assert.Empty(Dialogs.Calls);
    }

    [Fact]
    public async Task Contact_WithoutAnEmailApp_Explains()
    {
        _env.EmailAvailable = false;
        await _vm.ContactAsync(Dialogs);
        Assert.Equal(L["ErrorEmailNotAvailable"], Dialogs.Last.Message);
    }

    [Fact]
    public async Task Contact_Failing_ShowsTheError()
    {
        _env.EmailError = new InvalidOperationException("crash");
        await _vm.ContactAsync(Dialogs);
        Assert.Equal($"{L["ErrorEmail"]}: crash", Dialogs.Last.Message);
    }
}

public sealed class UpdateServiceTests : CultureSafeTests
{
    private readonly FakeEnvironment _env = new() { VersionString = "2026.09.30.0" };

    private UpdateService Create(StubHttpHandler handler) => new(L, _env, new HttpClient(handler));

    [Theory]
    [InlineData("2026.10.01.0", "2026.09.30.0", 1)]
    [InlineData("2026.09.30.0", "2026.09.30.0", 0)]
    [InlineData("2026.09.30", "2026.09.30.0", 0)]
    [InlineData("2026.9.30.1", "2026.09.30.0", 1)]
    [InlineData("2025.12.31.9", "2026.01.01.0", -1)]
    [InlineData("x.1", "0.1", 0)]
    public void CompareVersions_ByNumericParts(string a, string b, int sign) =>
        Assert.Equal(sign, Math.Sign(UpdateService.CompareVersions(a, b)));

    [Fact]
    public async Task NewerVersion_Accepted_OpensTheLink()
    {
        var handler = StubHttpHandler.Json("""{"version":"2026.10.01.0","url":"https://example.org/fm"}""");
        Dialogs.Answer(true);

        await Create(handler).CheckAndPromptAsync(Dialogs);

        Assert.Equal(new Uri(UpdateService.AppcastUrl), Assert.Single(handler.Requests));
        Assert.Equal(string.Format(L["UpdateAvailableMessage"], "2026.10.01.0", "2026.09.30.0"), Dialogs.Last.Message);
        Assert.Equal(new Uri("https://example.org/fm"), Assert.Single(_env.Opened));
    }

    [Fact]
    public async Task NewerVersion_Declined_OrWithoutUrl_OpensNothing()
    {
        Dialogs.Answer(false);
        await Create(StubHttpHandler.Json("""{"version":"2027.1.1.0","url":"https://example.org"}""")).CheckAndPromptAsync(Dialogs);

        Dialogs.Answer(true);
        await Create(StubHttpHandler.Json("""{"version":"2027.1.1.0","url":" "}""")).CheckAndPromptAsync(Dialogs);

        Assert.Equal(2, Dialogs.Calls.Count);
        Assert.Empty(_env.Opened);
    }

    [Theory]
    [InlineData("""{"version":"2026.09.30.0"}""")]
    [InlineData("""{"version":"2026.01.01.0","url":"https://example.org"}""")]
    [InlineData("""{"url":"https://example.org"}""")]
    [InlineData("null")]
    [InlineData("not json")]
    public async Task SameOlderOrBrokenManifest_SaysNothing(string json)
    {
        await Create(StubHttpHandler.Json(json)).CheckAndPromptAsync(Dialogs);
        Assert.Empty(Dialogs.Calls);
    }

    [Fact]
    public async Task NetworkErrors_AreSilent()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        await Create(handler).CheckAndPromptAsync(Dialogs);
        Assert.Empty(Dialogs.Calls);
    }

    [Fact]
    public async Task ChecksOnlyOncePerSession()
    {
        var handler = StubHttpHandler.Json("""{"version":"2026.09.30.0"}""");
        var service = Create(handler);

        await service.CheckAndPromptAsync(Dialogs);
        await service.CheckAndPromptAsync(Dialogs);

        Assert.Single(handler.Requests);
    }

    [Fact]
    public void PublicConstructor_UsesTheSharedClient() =>
        Assert.NotNull(new UpdateService(L, _env));
}
