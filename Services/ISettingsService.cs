using FileManager.Models;

namespace FileManager.Services;

/// <summary>
/// Preferencias ligeras del usuario, almacenadas en el dispositivo (constitucion 16).
/// </summary>
public interface ISettingsService
{
    /// <summary>Idioma elegido: "es", "en" o cadena vacia para seguir al sistema.</summary>
    string Language { get; set; }

    bool ShowHiddenFiles { get; set; }

    bool ConfirmDelete { get; set; }

    SortMode Sort { get; set; }
}
