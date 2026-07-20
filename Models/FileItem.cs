using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileManager.Models;

/// <summary>
/// Representa una entrada del sistema de ficheros (fichero o carpeta) mostrada en la lista.
/// </summary>
public class FileItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool _isSelected;

    /// <summary>¿Marcado en el modo de selección múltiple? (observable para la casilla).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnChanged();
        }
    }

    /// <summary>Categoría de contenido (para el filtro por tipo). Las carpetas son <c>Folder</c>.</summary>
    public FileCategory Category =>
        IsDirectory ? FileCategory.Folder : FileCategories.Of(Extension);

    public string Name { get; init; } = string.Empty;

    public string FullPath { get; init; } = string.Empty;

    public bool IsDirectory { get; init; }

    /// <summary>Tamano en bytes. Siempre 0 para carpetas.</summary>
    public long Size { get; init; }

    public DateTime Modified { get; init; }

    public bool IsHidden { get; init; }

    /// <summary>Nombre del recurso de icono (PNG vectorial) mostrado en la lista, asignado segun el tipo de contenido.</summary>
    public string Icon { get; set; } = "ic_file_generic.png";

    /// <summary>Linea secundaria de la fila (fecha y tamano ya formateados y localizados).</summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>Extension en minusculas y sin punto. Vacia para carpetas o ficheros sin extension.</summary>
    public string Extension =>
        IsDirectory ? string.Empty : Path.GetExtension(Name).TrimStart('.').ToLowerInvariant();
}
