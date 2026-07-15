using FileManager.Models;

namespace FileManager.Helpers;

/// <summary>Icono mostrado en la lista segun el tipo de contenido del fichero.</summary>
public static class FileIcons
{
    public const string Folder = "📁";

    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jpg"] = "🖼️", ["jpeg"] = "🖼️", ["png"] = "🖼️", ["gif"] = "🖼️", ["bmp"] = "🖼️",
        ["webp"] = "🖼️", ["heic"] = "🖼️", ["svg"] = "🖼️",

        ["mp4"] = "🎬", ["mkv"] = "🎬", ["avi"] = "🎬", ["mov"] = "🎬", ["3gp"] = "🎬", ["webm"] = "🎬",

        ["mp3"] = "🎵", ["wav"] = "🎵", ["ogg"] = "🎵", ["opus"] = "🎵", ["m4a"] = "🎵",
        ["flac"] = "🎵", ["aac"] = "🎵",

        ["pdf"] = "📕",
        ["doc"] = "📘", ["docx"] = "📘", ["odt"] = "📘",
        ["xls"] = "📗", ["xlsx"] = "📗", ["ods"] = "📗", ["csv"] = "📗",
        ["ppt"] = "📙", ["pptx"] = "📙",
        ["epub"] = "📚",

        ["txt"] = "📄", ["log"] = "📄", ["md"] = "📄",
        ["html"] = "🌐", ["htm"] = "🌐", ["xml"] = "🌐",
        ["json"] = "⚙️", ["cs"] = "⚙️", ["js"] = "⚙️", ["css"] = "⚙️",

        ["zip"] = "🗜️", ["rar"] = "🗜️", ["7z"] = "🗜️", ["tar"] = "🗜️", ["gz"] = "🗜️",

        ["apk"] = "📦"
    };

    public static string For(FileItem item)
    {
        if (item.IsDirectory)
            return Folder;

        return ByExtension.TryGetValue(item.Extension, out var icon) ? icon : "📄";
    }
}
