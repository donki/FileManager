using FileManager.Models;

namespace FileManager.Helpers;

/// <summary>
/// Icono mostrado en la lista segun el tipo de contenido del fichero. Devuelve el nombre del
/// recurso PNG generado a partir de los SVG de contorno de <c>Resources\Images</c>
/// (constitucion A.9: iconos vectoriales, nunca emoji).
/// </summary>
public static class FileIcons
{
    public const string Folder = "ic_file_folder.png";
    public const string Generic = "ic_file_generic.png";

    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jpg"] = "ic_file_image.png", ["jpeg"] = "ic_file_image.png", ["png"] = "ic_file_image.png",
        ["gif"] = "ic_file_image.png", ["bmp"] = "ic_file_image.png", ["webp"] = "ic_file_image.png",
        ["heic"] = "ic_file_image.png", ["svg"] = "ic_file_image.png",

        ["mp4"] = "ic_file_video.png", ["mkv"] = "ic_file_video.png", ["avi"] = "ic_file_video.png",
        ["mov"] = "ic_file_video.png", ["3gp"] = "ic_file_video.png", ["webm"] = "ic_file_video.png",

        ["mp3"] = "ic_file_audio.png", ["wav"] = "ic_file_audio.png", ["ogg"] = "ic_file_audio.png",
        ["opus"] = "ic_file_audio.png", ["m4a"] = "ic_file_audio.png", ["flac"] = "ic_file_audio.png",
        ["aac"] = "ic_file_audio.png",

        ["pdf"] = "ic_file_pdf.png",
        ["doc"] = "ic_file_document.png", ["docx"] = "ic_file_document.png", ["odt"] = "ic_file_document.png",
        ["xls"] = "ic_file_sheet.png", ["xlsx"] = "ic_file_sheet.png", ["ods"] = "ic_file_sheet.png",
        ["csv"] = "ic_file_sheet.png",
        ["ppt"] = "ic_file_slides.png", ["pptx"] = "ic_file_slides.png",
        ["epub"] = "ic_file_ebook.png",

        ["txt"] = "ic_file_document.png", ["log"] = "ic_file_document.png", ["md"] = "ic_file_document.png",
        ["html"] = "ic_file_web.png", ["htm"] = "ic_file_web.png", ["xml"] = "ic_file_web.png",
        ["json"] = "ic_file_code.png", ["cs"] = "ic_file_code.png", ["js"] = "ic_file_code.png",
        ["css"] = "ic_file_code.png",

        ["zip"] = "ic_file_archive.png", ["rar"] = "ic_file_archive.png", ["7z"] = "ic_file_archive.png",
        ["tar"] = "ic_file_archive.png", ["gz"] = "ic_file_archive.png",

        ["apk"] = "ic_file_apk.png"
    };

    public static string For(FileItem item)
    {
        if (item.IsDirectory)
            return Folder;

        return ByExtension.TryGetValue(item.Extension, out var icon) ? icon : Generic;
    }
}
