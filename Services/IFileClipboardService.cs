using FileManager.Models;

namespace FileManager.Services;

/// <summary>
/// Portapapeles interno de ficheros, compartido entre carpetas mientras la app esta abierta.
/// </summary>
public interface IFileClipboardService
{
    ClipboardEntry? Current { get; }

    bool HasContent { get; }

    void Set(IReadOnlyList<string> paths, bool isMove);

    void Clear();

    event EventHandler? Changed;
}
