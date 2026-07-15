using FileManager.Models;
using Microsoft.Extensions.Logging;

namespace FileManager.Services;

/// <inheritdoc cref="IFileSystemService"/>
public class FileSystemService : IFileSystemService
{
    private static readonly char[] InvalidNameChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

    private const int MaxSearchResults = 500;

    private readonly ILogger<FileSystemService> _logger;

    public FileSystemService(ILogger<FileSystemService> logger) => _logger = logger;

    public string RootPath
    {
        get
        {
#if ANDROID
            var external = Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath;
            if (!string.IsNullOrEmpty(external) && Directory.Exists(external))
                return external;
#endif
            return "/storage/emulated/0";
        }
    }

    public Task<IReadOnlyList<FileItem>> ListAsync(string path, bool showHidden, SortMode sort, CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<FileItem>>(() =>
        {
            var directory = new DirectoryInfo(path);
            var items = new List<FileItem>();

            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var item = ToItem(entry);
                if (item is null || (item.IsHidden && !showHidden))
                    continue;

                items.Add(item);
            }

            return Sort(items, sort);
        }, cancellationToken);

    public Task<IReadOnlyList<FileItem>> SearchAsync(string path, string query, bool showHidden, CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<FileItem>>(() =>
        {
            var results = new List<FileItem>();
            var pending = new Queue<string>();
            pending.Enqueue(path);

            // Recorrido en anchura propio: EnumerateFileSystemInfos con SearchOption.AllDirectories
            // aborta el recorrido entero en cuanto una subcarpeta no es accesible, y en Android
            // hay varias (por ejemplo /storage/emulated/0/Android/data).
            while (pending.Count > 0 && results.Count < MaxSearchResults)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = pending.Dequeue();

                IEnumerable<FileSystemInfo> entries;
                try
                {
                    entries = new DirectoryInfo(current).EnumerateFileSystemInfos().ToList();
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    _logger.LogDebug("Skipping unreadable folder {Path} during search: {Message}", current, ex.Message);
                    continue;
                }

                foreach (var entry in entries)
                {
                    var item = ToItem(entry);
                    if (item is null || (item.IsHidden && !showHidden))
                        continue;

                    if (item.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(item);
                        if (results.Count >= MaxSearchResults)
                        {
                            _logger.LogInformation("Search truncated at {Max} results", MaxSearchResults);
                            break;
                        }
                    }

                    if (item.IsDirectory)
                        pending.Enqueue(item.FullPath);
                }
            }

            return Sort(results, SortMode.NameAscending);
        }, cancellationToken);

    public int CountEntries(string directoryPath)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(directoryPath).Count();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.LogDebug("Cannot count entries of {Path}: {Message}", directoryPath, ex.Message);
            return -1;
        }
    }

    public NameValidation ValidateName(string? name, string parentPath, string? currentPath = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return NameValidation.Empty;

        name = name.Trim();

        if (name.IndexOfAny(InvalidNameChars) >= 0 || name is "." or "..")
            return NameValidation.InvalidCharacters;

        var target = Path.Combine(parentPath, name);

        // Renombrar un elemento con su propio nombre (o cambiando solo mayusculas) no es un conflicto.
        if (currentPath is not null && string.Equals(target, currentPath, StringComparison.OrdinalIgnoreCase))
            return NameValidation.Valid;

        if (File.Exists(target) || Directory.Exists(target))
            return NameValidation.AlreadyExists;

        return NameValidation.Valid;
    }

    public Task<string> CreateDirectoryAsync(string parentPath, string name) =>
        Task.Run(() =>
        {
            var target = Path.Combine(parentPath, name.Trim());
            Directory.CreateDirectory(target);
            _logger.LogInformation("Directory created");
            return target;
        });

    public Task<string> RenameAsync(string path, string newName) =>
        Task.Run(() =>
        {
            var parent = Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException("The item has no parent folder.");
            var target = Path.Combine(parent, newName.Trim());

            if (Directory.Exists(path))
                Directory.Move(path, target);
            else
                File.Move(path, target);

            _logger.LogInformation("Item renamed");
            return target;
        });

    public Task<OperationResult> DeleteAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var result = new OperationResult();

            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (Directory.Exists(path))
                        Directory.Delete(path, recursive: true);
                    else if (File.Exists(path))
                        File.Delete(path);
                    else
                        continue;

                    result.Succeeded++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Un fallo en un elemento no aborta el lote, pero se informa (constitucion 17).
                    _logger.LogWarning(ex, "Delete failed for one item");
                    result.Errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
                }
            }

            return result;
        }, cancellationToken);

    public IReadOnlyList<string> GetConflicts(IReadOnlyList<string> paths, string destination)
    {
        var conflicts = new List<string>();

        foreach (var path in paths)
        {
            var name = Path.GetFileName(path);
            var target = Path.Combine(destination, name);

            // Pegar en la carpeta de origen siempre genera copias con nombre nuevo,
            // no un conflicto que el usuario deba resolver.
            if (string.Equals(target, path, StringComparison.OrdinalIgnoreCase))
                continue;

            if (File.Exists(target) || Directory.Exists(target))
                conflicts.Add(name);
        }

        return conflicts;
    }

    public Task<OperationResult> PasteAsync(ClipboardEntry entry, string destination, ConflictResolution resolution, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var result = new OperationResult();

            foreach (var source in entry.Paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var isDirectory = Directory.Exists(source);
                    if (!isDirectory && !File.Exists(source))
                    {
                        result.Errors.Add(Path.GetFileName(source));
                        continue;
                    }

                    if (isDirectory && IsInside(source, destination))
                        throw new InvalidOperationException("A folder cannot be pasted inside itself.");

                    var name = Path.GetFileName(source);
                    var target = Path.Combine(destination, name);
                    var samePlace = string.Equals(target, source, StringComparison.OrdinalIgnoreCase);

                    if (samePlace)
                    {
                        // Mover a la misma carpeta no hace nada; copiar genera "nombre (2)".
                        if (entry.IsMove)
                            continue;

                        target = GetUniquePath(target);
                    }
                    else if (File.Exists(target) || Directory.Exists(target))
                    {
                        if (resolution == ConflictResolution.KeepBoth)
                            target = GetUniquePath(target);
                        else
                            DeleteTarget(target);
                    }

                    if (isDirectory)
                        MoveOrCopyDirectory(source, target, entry.IsMove, cancellationToken);
                    else
                        MoveOrCopyFile(source, target, entry.IsMove);

                    result.Succeeded++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    _logger.LogWarning(ex, "Paste failed for one item");
                    result.Errors.Add($"{Path.GetFileName(source)}: {ex.Message}");
                }
            }

            return result;
        }, cancellationToken);

    public bool IsInside(string path, string destination)
    {
        var normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var normalizedDestination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));

        return normalizedDestination.StartsWith(normalizedPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedDestination, normalizedPath, StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteTarget(string target)
    {
        if (Directory.Exists(target))
            Directory.Delete(target, recursive: true);
        else if (File.Exists(target))
            File.Delete(target);
    }

    private static void MoveOrCopyFile(string source, string target, bool move)
    {
        if (move)
        {
            try
            {
                File.Move(source, target);
                return;
            }
            catch (IOException)
            {
                // Mover entre volumenes distintos (memoria interna y tarjeta SD) no lo soporta
                // el sistema: se copia y despues se borra el origen.
                File.Copy(source, target, overwrite: true);
                File.Delete(source);
                return;
            }
        }

        File.Copy(source, target, overwrite: true);
    }

    private static void MoveOrCopyDirectory(string source, string target, bool move, CancellationToken cancellationToken)
    {
        if (move)
        {
            try
            {
                Directory.Move(source, target);
                return;
            }
            catch (IOException)
            {
                CopyDirectory(source, target, cancellationToken);
                Directory.Delete(source, recursive: true);
                return;
            }
        }

        CopyDirectory(source, target, cancellationToken);
    }

    private static void CopyDirectory(string source, string target, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(target);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)), cancellationToken);
        }
    }

    /// <summary>Devuelve una ruta libre anadiendo " (2)", " (3)"… al nombre.</summary>
    private static string GetUniquePath(string target)
    {
        var directory = Path.GetDirectoryName(target) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(target);
        var extension = Path.GetExtension(target);

        for (var index = 2; index < int.MaxValue; index++)
        {
            var candidate = Path.Combine(directory, $"{name} ({index}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;
        }

        throw new IOException("No free name is available for this item.");
    }

    private FileItem? ToItem(FileSystemInfo entry)
    {
        try
        {
            var isDirectory = (entry.Attributes & FileAttributes.Directory) == FileAttributes.Directory;

            return new FileItem
            {
                Name = entry.Name,
                FullPath = entry.FullName,
                IsDirectory = isDirectory,
                Size = isDirectory ? 0 : ((FileInfo)entry).Length,
                Modified = entry.LastWriteTime,
                IsHidden = entry.Name.StartsWith('.')
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Una entrada ilegible (enlace roto, permisos) no debe tumbar el listado completo.
            _logger.LogDebug("Skipping unreadable entry: {Message}", ex.Message);
            return null;
        }
    }

    private static IReadOnlyList<FileItem> Sort(List<FileItem> items, SortMode sort)
    {
        // Las carpetas siempre van antes que los ficheros, sea cual sea el criterio.
        IOrderedEnumerable<FileItem> ordered = items.OrderByDescending(i => i.IsDirectory);

        ordered = sort switch
        {
            SortMode.NameDescending => ordered.ThenByDescending(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
            SortMode.DateDescending => ordered.ThenByDescending(i => i.Modified),
            SortMode.DateAscending => ordered.ThenBy(i => i.Modified),
            SortMode.SizeDescending => ordered.ThenByDescending(i => i.Size),
            SortMode.SizeAscending => ordered.ThenBy(i => i.Size),
            _ => ordered.ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
        };

        return ordered.ToList();
    }
}
