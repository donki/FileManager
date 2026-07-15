namespace FileManager.Models;

/// <summary>
/// Representa una entrada del sistema de ficheros (fichero o carpeta) mostrada en la lista.
/// </summary>
public class FileItem
{
    public string Name { get; init; } = string.Empty;

    public string FullPath { get; init; } = string.Empty;

    public bool IsDirectory { get; init; }

    /// <summary>Tamano en bytes. Siempre 0 para carpetas.</summary>
    public long Size { get; init; }

    public DateTime Modified { get; init; }

    public bool IsHidden { get; init; }

    /// <summary>Icono mostrado en la lista, asignado segun el tipo de contenido.</summary>
    public string Icon { get; set; } = "📄";

    /// <summary>Linea secundaria de la fila (fecha y tamano ya formateados y localizados).</summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>Extension en minusculas y sin punto. Vacia para carpetas o ficheros sin extension.</summary>
    public string Extension =>
        IsDirectory ? string.Empty : Path.GetExtension(Name).TrimStart('.').ToLowerInvariant();
}
