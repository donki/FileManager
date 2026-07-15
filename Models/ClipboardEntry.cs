namespace FileManager.Models;

/// <summary>
/// Portapapeles interno de ficheros: rutas pendientes de pegar y si la operacion es mover o copiar.
/// </summary>
public class ClipboardEntry
{
    public IReadOnlyList<string> Paths { get; init; } = Array.Empty<string>();

    public bool IsMove { get; init; }
}
