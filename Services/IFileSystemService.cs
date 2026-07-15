using FileManager.Models;

namespace FileManager.Services;

public enum NameValidation
{
    Valid,
    Empty,
    InvalidCharacters,
    AlreadyExists
}

public enum ConflictResolution
{
    Replace,
    KeepBoth
}

/// <summary>Resultado de una operacion por lotes: cuantos elementos se procesaron y que fallo.</summary>
public class OperationResult
{
    public int Succeeded { get; set; }

    public List<string> Errors { get; } = new();

    public bool HasErrors => Errors.Count > 0;
}

/// <summary>
/// Operaciones sobre el sistema de ficheros. Toda la logica de negocio del gestor
/// vive aqui, no en la interfaz (constitucion 4).
/// </summary>
public interface IFileSystemService
{
    /// <summary>Raiz del almacenamiento interno del dispositivo.</summary>
    string RootPath { get; }

    Task<IReadOnlyList<FileItem>> ListAsync(string path, bool showHidden, SortMode sort, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileItem>> SearchAsync(string path, string query, bool showHidden, CancellationToken cancellationToken = default);

    /// <summary>Numero de entradas directas de una carpeta, o -1 si no se puede leer.</summary>
    int CountEntries(string directoryPath);

    NameValidation ValidateName(string? name, string parentPath, string? currentPath = null);

    Task<string> CreateDirectoryAsync(string parentPath, string name);

    Task<string> RenameAsync(string path, string newName);

    Task<OperationResult> DeleteAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default);

    /// <summary>Nombres del portapapeles que ya existen en el destino.</summary>
    IReadOnlyList<string> GetConflicts(IReadOnlyList<string> paths, string destination);

    Task<OperationResult> PasteAsync(ClipboardEntry entry, string destination, ConflictResolution resolution, CancellationToken cancellationToken = default);

    /// <summary>Comprueba si <paramref name="destination"/> esta dentro de <paramref name="path"/>.</summary>
    bool IsInside(string path, string destination);
}
