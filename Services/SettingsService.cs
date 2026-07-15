using FileManager.Models;
using Microsoft.Extensions.Logging;

namespace FileManager.Services;

/// <inheritdoc cref="ISettingsService"/>
public class SettingsService : ISettingsService
{
    private const string LanguageKey = "language";
    private const string ShowHiddenKey = "show_hidden";
    private const string ConfirmDeleteKey = "confirm_delete";
    private const string SortKey = "sort_mode";

    private readonly ILogger<SettingsService> _logger;

    public SettingsService(ILogger<SettingsService> logger) => _logger = logger;

    public string Language
    {
        get => Preferences.Get(LanguageKey, LocalizationService.SystemLanguage);
        set => Preferences.Set(LanguageKey, value ?? LocalizationService.SystemLanguage);
    }

    public bool ShowHiddenFiles
    {
        get => Preferences.Get(ShowHiddenKey, false);
        set => Preferences.Set(ShowHiddenKey, value);
    }

    public bool ConfirmDelete
    {
        get => Preferences.Get(ConfirmDeleteKey, true);
        set => Preferences.Set(ConfirmDeleteKey, value);
    }

    public SortMode Sort
    {
        get
        {
            var stored = Preferences.Get(SortKey, (int)SortMode.NameAscending);
            // Un valor fuera de rango (dato corrupto o de una version anterior) no debe
            // romper la app: se vuelve al orden por defecto (constitucion 16).
            if (!Enum.IsDefined(typeof(SortMode), stored))
            {
                _logger.LogWarning("Discarding unknown stored sort mode {Value}", stored);
                return SortMode.NameAscending;
            }

            return (SortMode)stored;
        }
        set => Preferences.Set(SortKey, (int)value);
    }
}
