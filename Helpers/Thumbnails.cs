using System.Security.Cryptography;
using System.Text;
using FileManager.Models;

namespace FileManager.Helpers;

/// <summary>
/// Que ficheros llevan miniatura en la lista (fotos y videos que Android sabe decodificar) y con que
/// clave se guarda cada una en la cache.
/// </summary>
public static class Thumbnails
{
    /// <summary>Lado de la miniatura en pixeles: la pastilla mide 44 dp, unos 132 px en una pantalla xxhdpi.</summary>
    public const int SizePx = 128;

    // Solo lo que decodifican BitmapFactory/ImageDecoder: SVG, TIFF o ICO se quedan con su icono.
    private static readonly HashSet<string> Images = new(StringComparer.OrdinalIgnoreCase)
        { "jpg", "jpeg", "png", "gif", "bmp", "webp", "heic", "heif" };

    // Lo que abre MediaMetadataRetriever con los decodificadores del sistema.
    private static readonly HashSet<string> Videos = new(StringComparer.OrdinalIgnoreCase)
        { "mp4", "m4v", "mkv", "webm", "3gp", "mov", "avi", "ts" };

    /// <summary>¿Este elemento lleva miniatura? Las carpetas y el resto de tipos, no.</summary>
    public static bool Supports(FileItem item) =>
        !item.IsDirectory && (Images.Contains(item.Extension) || Videos.Contains(item.Extension));

    /// <summary>¿Es un video (se saca un fotograma) o una imagen?</summary>
    public static bool IsVideo(FileItem item) => !item.IsDirectory && Videos.Contains(item.Extension);

    /// <summary>
    /// Clave de la cache: ruta, tamano y fecha de modificacion del fichero, y lado de la miniatura. Si
    /// el fichero se cambia o se sustituye por otro con el mismo nombre, la clave cambia y no se ensena
    /// una miniatura vieja. Es un hash para que valga como nombre de fichero.
    /// </summary>
    public static string CacheKey(string path, long length, DateTime modified, int sizePx = SizePx)
    {
        var text = $"{path}|{length}|{modified.ToUniversalTime().Ticks}|{sizePx}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0, 20).ToLowerInvariant();
    }

    public static string CacheKey(FileItem item, int sizePx = SizePx) =>
        CacheKey(item.FullPath, item.Size, item.Modified, sizePx);

    /// <summary>
    /// Reduccion al decodificar (<c>inSampleSize</c>): la mayor potencia de dos que deja el lado corto de
    /// la foto por encima del lado pedido. Una foto de 12 Mpx no llega nunca entera a memoria.
    /// </summary>
    public static int SampleSize(int width, int height, int sizePx = SizePx)
    {
        var sample = 1;
        while (Math.Min(width, height) / (sample * 2) >= sizePx)
            sample *= 2;
        return sample;
    }

    /// <summary>Grados que hay que girar la foto segun la orientacion EXIF (las camaras la guardan tumbada y lo apuntan ahi).</summary>
    public static int ExifRotation(int orientation) => orientation switch
    {
        6 => 90,
        3 => 180,
        8 => 270,
        _ => 0
    };
}
