using System.IO.Compression;

namespace FileManager.Helpers;

/// <summary>
/// Prepara lo que se va a compartir: los ficheros van tal cual y cada carpeta se comprime en un
/// .zip temporal (las aplicaciones de destino solo reciben ficheros, no carpetas). Los .zip se
/// dejan en <c>workDir</c> y se borran al preparar la siguiente vez: la aplicacion que los recibe
/// puede leerlos despues de que el selector se cierre.
/// </summary>
public static class SharePreparer
{
    public static IReadOnlyList<string> Prepare(IReadOnlyList<string> paths, string workDir, CancellationToken token = default)
    {
        if (paths.Count == 0)
            throw new ArgumentException("Nothing to share", nameof(paths));

        Clean(workDir);
        var result = new List<string>(paths.Count);

        foreach (var path in paths)
        {
            token.ThrowIfCancellationRequested();
            if (Directory.Exists(path))
            {
                Directory.CreateDirectory(workDir);
                var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
                if (string.IsNullOrEmpty(name))
                    name = "folder";
                var zip = UniquePath(workDir, name, ".zip");
                ZipFile.CreateFromDirectory(path, zip, CompressionLevel.Fastest, includeBaseDirectory: true);
                result.Add(zip);
            }
            else if (File.Exists(path))
            {
                result.Add(path);
            }
            else
            {
                throw new FileNotFoundException(Path.GetFileName(path), path);
            }
        }

        return result;
    }

    /// <summary>Borra lo que quedo de la vez anterior; si algo sigue en uso, se queda.</summary>
    private static void Clean(string workDir)
    {
        if (!Directory.Exists(workDir))
            return;
        foreach (var file in Directory.EnumerateFiles(workDir))
        {
            try { File.Delete(file); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static string UniquePath(string dir, string name, string extension)
    {
        var candidate = Path.Combine(dir, name + extension);
        for (var i = 2; File.Exists(candidate); i++)
            candidate = Path.Combine(dir, $"{name} ({i}){extension}");
        return candidate;
    }
}
