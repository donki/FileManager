namespace FileManager.Models;

/// <summary>Categoria de contenido de un fichero, para filtrar la lista por tipo.</summary>
public enum FileCategory
{
    Folder,
    Image,
    Video,
    Audio,
    Document,
    Apk,
    Archive,
    Other
}

/// <summary>Clasifica una extension (sin punto, minusculas) en una <see cref="FileCategory"/>.</summary>
public static class FileCategories
{
    private static readonly HashSet<string> Images = new()
        { "jpg", "jpeg", "png", "gif", "bmp", "webp", "heic", "heif", "svg", "ico", "tif", "tiff" };

    private static readonly HashSet<string> Videos = new()
        { "mp4", "mkv", "avi", "mov", "wmv", "webm", "3gp", "flv", "m4v", "mpg", "mpeg", "ts" };

    private static readonly HashSet<string> Audios = new()
        { "mp3", "wav", "ogg", "flac", "aac", "m4a", "wma", "opus", "amr", "mid" };

    private static readonly HashSet<string> Documents = new()
        { "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt", "rtf",
          "odt", "ods", "odp", "md", "csv", "epub", "log", "json", "xml" };

    private static readonly HashSet<string> Apks = new()
        { "apk", "apks", "xapk", "aab" };

    private static readonly HashSet<string> Archives = new()
        { "zip", "rar", "7z", "tar", "gz", "bz2", "xz", "tgz", "iso" };

    public static FileCategory Of(string extension)
    {
        if (Images.Contains(extension)) return FileCategory.Image;
        if (Videos.Contains(extension)) return FileCategory.Video;
        if (Audios.Contains(extension)) return FileCategory.Audio;
        if (Documents.Contains(extension)) return FileCategory.Document;
        if (Apks.Contains(extension)) return FileCategory.Apk;
        if (Archives.Contains(extension)) return FileCategory.Archive;
        return FileCategory.Other;
    }
}
