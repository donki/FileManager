using FileManager.Services;
using Microsoft.Extensions.Logging;

namespace FileManager.ViewModels;

/// <summary>Logica de Acerca de: version, idioma y contacto por correo.</summary>
public class AboutViewModel
{
    public const string TitleKey = "AboutTitle";

    // CONFIGURACION
    public const string ContactEmail = "jsoladelarosa@gmail.com";

    /// <summary>Control de la pagina (x:Name) y clave del texto fijo que lleva.</summary>
    public static readonly IReadOnlyDictionary<string, string> StaticTexts = new Dictionary<string, string>
    {
        ["AppNameLabel"] = "AppName",
        ["DescriptionLabel"] = "AppDescription",
        ["CompanyLabel"] = "Company",
        ["ContactTitle"] = "AboutContact",
        ["ContactHint"] = "AboutContactHint",
        ["PrivacyTitle"] = "AboutPrivacy",
        ["PrivacyText"] = "AboutPrivacyText",
        ["LicenseTitle"] = "AboutLicense",
        ["LicenseText"] = "AboutLicenseText",
        ["LanguageTitle"] = "SettingsLanguage",
        ["LanguageHint"] = "AboutLanguageHint",
        ["SpanishButton"] = "SettingsLanguageSpanish",
        ["EnglishButton"] = "SettingsLanguageEnglish",
        ["LegalTitle"] = "AboutLegal",
        ["LegalText1"] = "AboutLegal1",
        ["LegalText2"] = "AboutLegal2",
        ["WarningText"] = "AboutWarning",
        ["BackButton"] = "Back",
    };

    private readonly ILocalizationService _l;
    private readonly ISettingsService _settings;
    private readonly IAppEnvironment _environment;
    private readonly ILogger _logger;

    public AboutViewModel(ILocalizationService localization, ISettingsService settings,
        IAppEnvironment environment, ILogger logger)
    {
        _l = localization;
        _settings = settings;
        _environment = environment;
        _logger = logger;
    }

    public bool IsSpanish => _l.CurrentLanguage == "es";

    public string VersionText => string.Format(_l.CurrentCulture, _l["AboutVersion"], _environment.VersionString);

    /// <summary>Cambia y guarda el idioma. True si ha cambiado.</summary>
    public bool SetLanguage(string code)
    {
        if (code == _l.CurrentLanguage)
            return false;

        _settings.Language = code;
        _l.SetLanguage(code);
        return true;
    }

    /// <summary>Abre el correo al autor; si no se puede, lo explica.</summary>
    public async Task ContactAsync(IDialogService dialogs)
    {
        try
        {
            if (!await _environment.ComposeEmailAsync(_l["EmailSubject"], ContactEmail))
                await dialogs.AlertAsync(_l["Error"], _l["ErrorEmailNotAvailable"], _l["Ok"]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open the email client");
            await dialogs.AlertAsync(_l["Error"], $"{_l["ErrorEmail"]}: {ex.Message}", _l["Ok"]);
        }
    }
}
