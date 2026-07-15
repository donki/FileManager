namespace FileManager.Helpers;

/// <summary>
/// Tipo MIME a partir de la extension. Android lo necesita para elegir la aplicacion
/// con la que abrir o compartir un fichero.
/// </summary>
public static class MimeTypes
{
    public const string Default = "application/octet-stream";

    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // Imagen
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["png"] = "image/png",
        ["gif"] = "image/gif",
        ["bmp"] = "image/bmp",
        ["webp"] = "image/webp",
        ["heic"] = "image/heic",
        ["svg"] = "image/svg+xml",
        // Video
        ["mp4"] = "video/mp4",
        ["mkv"] = "video/x-matroska",
        ["avi"] = "video/x-msvideo",
        ["mov"] = "video/quicktime",
        ["3gp"] = "video/3gpp",
        ["webm"] = "video/webm",
        // Audio
        ["mp3"] = "audio/mpeg",
        ["wav"] = "audio/wav",
        ["ogg"] = "audio/ogg",
        ["opus"] = "audio/opus",
        ["m4a"] = "audio/mp4",
        ["flac"] = "audio/flac",
        ["aac"] = "audio/aac",
        // Documento
        ["pdf"] = "application/pdf",
        ["doc"] = "application/msword",
        ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["xls"] = "application/vnd.ms-excel",
        ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ["ppt"] = "application/vnd.ms-powerpoint",
        ["pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ["odt"] = "application/vnd.oasis.opendocument.text",
        ["ods"] = "application/vnd.oasis.opendocument.spreadsheet",
        ["epub"] = "application/epub+zip",
        // Texto y codigo
        ["txt"] = "text/plain",
        ["log"] = "text/plain",
        ["md"] = "text/markdown",
        ["csv"] = "text/csv",
        ["html"] = "text/html",
        ["htm"] = "text/html",
        ["xml"] = "text/xml",
        ["json"] = "application/json",
        ["cs"] = "text/plain",
        ["js"] = "text/javascript",
        ["css"] = "text/css",
        // Comprimido
        ["zip"] = "application/zip",
        ["rar"] = "application/vnd.rar",
        ["7z"] = "application/x-7z-compressed",
        ["tar"] = "application/x-tar",
        ["gz"] = "application/gzip",
        // Aplicacion
        ["apk"] = "application/vnd.android.package-archive"
    };

    public static string ForPath(string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.');
        return Map.TryGetValue(extension, out var mime) ? mime : Default;
    }
}
