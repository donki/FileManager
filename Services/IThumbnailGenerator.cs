namespace FileManager.Services;

/// <summary>
/// Hace la miniatura de una foto o de un video con lo que da la plataforma. Se llama siempre fuera
/// del hilo de la interfaz.
/// </summary>
public interface IThumbnailGenerator
{
    /// <summary>
    /// Escribe en <paramref name="targetPath"/> una miniatura de <paramref name="sourcePath"/> de unos
    /// <paramref name="sizePx"/> pixeles de lado. False si no se puede (formato que el sistema no
    /// decodifica, fichero roto o ilegible): la lista sigue con el icono.
    /// </summary>
    bool TryCreate(string sourcePath, bool isVideo, string targetPath, int sizePx, CancellationToken cancellationToken);
}

/// <summary>Sin plataforma que sepa hacerlas (el banco de pruebas): ninguna miniatura, todo con icono.</summary>
public sealed class NoThumbnailGenerator : IThumbnailGenerator
{
    public bool TryCreate(string sourcePath, bool isVideo, string targetPath, int sizePx, CancellationToken cancellationToken) => false;
}
