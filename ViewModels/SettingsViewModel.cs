using FileManager.Services;
using Microsoft.Extensions.Logging;

namespace FileManager.ViewModels;

/// <summary>Logica de Configuracion: idioma, preferencias de la lista y permiso de almacenamiento.</summary>
public class SettingsViewModel
{
    public const string TitleKey = "SettingsTitle";

    /// <summary>Control de la pagina (x:Name) y clave del texto fijo que lleva.</summary>
    public static readonly IReadOnlyDictionary<string, string> StaticTexts = new Dictionary<string, string>
    {
        ["LanguageTitle"] = "SettingsLanguage",
        ["LanguageHint"] = "SettingsLanguageHint",
        ["SpanishButton"] = "SettingsLanguageSpanish",
        ["EnglishButton"] = "SettingsLanguageEnglish",
        ["DisplayTitle"] = "SettingsDisplay",
        ["ShowHiddenLabel"] = "SettingsShowHidden",
        ["ShowHiddenHint"] = "SettingsShowHiddenHint",
        ["ConfirmDeleteLabel"] = "SettingsConfirmDelete",
        ["ConfirmDeleteHint"] = "SettingsConfirmDeleteHint",
        ["StorageTitle"] = "SettingsStorage",
        ["PermissionStateLabel"] = "SettingsPermissionState",
        ["PermissionButton"] = "PermissionOpenSettings",
        ["AboutButton"] = "About",
    };

    private readonly ISettingsService _settings;
    private readonly ILocalizationService _l;
    private readonly IStoragePermissionService _permissions;
    private readonly ILogger _logger;

    public SettingsViewModel(ISettingsService settings, ILocalizationService localization,
        IStoragePermissionService permissions, ILogger logger)
    {
        _settings = settings;
        _l = localization;
        _permissions = permissions;
        _logger = logger;
    }

    /// <summary>Mientras la pagina rellena sus controles, sus eventos de cambio no deben guardar nada.</summary>
    public bool Loading { get; set; }

    /// <summary>El castellano es el idioma activo (su boton va con el estilo primario).</summary>
    public bool IsSpanish => _l.CurrentLanguage == "es";

    public bool ShowHiddenFiles => _settings.ShowHiddenFiles;

    public bool ConfirmDelete => _settings.ConfirmDelete;

    public bool PermissionGranted => _permissions.HasFullAccess;

    public string PermissionStateText => PermissionGranted ? _l["SettingsPermissionOk"] : _l["SettingsPermissionKo"];

    /// <summary>Cambia y guarda el idioma. True si ha cambiado (la pagina vuelve a pintar sus textos).</summary>
    public bool SetLanguage(string code)
    {
        if (Loading || code == _l.CurrentLanguage)
            return false;

        _settings.Language = code;
        _l.SetLanguage(code);
        return true;
    }

    public void SetShowHiddenFiles(bool value)
    {
        if (!Loading)
            _settings.ShowHiddenFiles = value;
    }

    public void SetConfirmDelete(bool value)
    {
        if (!Loading)
            _settings.ConfirmDelete = value;
    }

    public async Task RequestPermissionAsync(IDialogService dialogs)
    {
        try
        {
            await _permissions.RequestFullAccessAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open the storage permission settings");
            await dialogs.AlertAsync(_l["Error"], ex.Message, _l["Ok"]);
        }
    }
}
