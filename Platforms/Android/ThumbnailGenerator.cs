using System.Runtime.Versioning;
using Android.Graphics;
using Android.Media;
using Android.OS;
using FileManager.Helpers;
using FileManager.Services;
using JFile = Java.IO.File;
using Size = Android.Util.Size;

namespace FileManager.Platforms.Android;

/// <summary>
/// Miniaturas con lo que trae Android. La tarjeta SD se lee por su ruta, igual que la memoria interna
/// (la app tiene acceso a todos los ficheros), asi que vale el mismo camino para las dos.
/// <list type="bullet">
/// <item>Android 10 (API 29) y siguientes: <c>ThumbnailUtils.CreateImageThumbnail/CreateVideoThumbnail</c>
/// con tamano y cancelacion; ya giran la foto segun su EXIF.</item>
/// <item>Antes: <c>BitmapFactory</c> leyendo solo una de cada N filas (<c>inSampleSize</c>), girada a mano
/// segun el EXIF, y para los videos <c>CreateVideoThumbnail(ruta, MiniKind)</c>.</item>
/// </list>
/// Se recorta al centro en un cuadrado del lado pedido y se guarda en JPEG (PNG si tiene transparencia).
/// </summary>
public sealed class ThumbnailGenerator : IThumbnailGenerator
{
    public bool TryCreate(string sourcePath, bool isVideo, string targetPath, int sizePx, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bitmap = OperatingSystem.IsAndroidVersionAtLeast(29)
            ? Modern(sourcePath, isVideo, sizePx, cancellationToken)
            : Legacy(sourcePath, isVideo, sizePx);
        if (bitmap is null)
            return false;

        var square = ThumbnailUtils.ExtractThumbnail(bitmap, sizePx, sizePx);
        if (!ReferenceEquals(square, bitmap))
            Free(bitmap);
        if (square is null)
            return false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = File.Create(targetPath);
            var format = square.HasAlpha ? Bitmap.CompressFormat.Png! : Bitmap.CompressFormat.Jpeg!;
            return square.Compress(format, 82, stream);
        }
        finally
        {
            Free(square);
        }
    }

    [SupportedOSPlatform("android29.0")]
    private static Bitmap? Modern(string path, bool isVideo, int sizePx, CancellationToken cancellationToken)
    {
        using var signal = new CancellationSignal();
        using var registration = cancellationToken.Register(signal.Cancel);
        using var file = new JFile(path);
        using var size = new Size(sizePx, sizePx);
        try
        {
            return isVideo
                ? ThumbnailUtils.CreateVideoThumbnail(file, size, signal)
                : ThumbnailUtils.CreateImageThumbnail(file, size, signal);
        }
        catch (global::Android.OS.OperationCanceledException)
        {
            throw new System.OperationCanceledException(cancellationToken);
        }
        catch (Java.IO.IOException)
        {
            // Formato que el sistema no decodifica o fichero roto: se queda el icono.
            return null;
        }
    }

    private static Bitmap? Legacy(string path, bool isVideo, int sizePx)
    {
        if (isVideo)
        {
#pragma warning disable CA1422, CS0618 // Obsoleta desde API 29: aqui solo se usa por debajo.
            return ThumbnailUtils.CreateVideoThumbnail(path, global::Android.Provider.ThumbnailKind.MiniKind);
#pragma warning restore CA1422, CS0618
        }

        // Primero solo las medidas; luego se lee reducida (Thumbnails.SampleSize).
        using var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeFile(path, bounds);
        if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0)
            return null;

        using var options = new BitmapFactory.Options { InSampleSize = Thumbnails.SampleSize(bounds.OutWidth, bounds.OutHeight, sizePx) };
        var bitmap = BitmapFactory.DecodeFile(path, options);
        return bitmap is null ? null : Upright(bitmap, path);
    }

    /// <summary>Gira la foto como dice su EXIF (las camaras guardan la imagen tumbada y lo apuntan ahi).</summary>
    private static Bitmap Upright(Bitmap bitmap, string path)
    {
        int degrees;
        try
        {
            using var exif = new ExifInterface(path);
            degrees = Thumbnails.ExifRotation(exif.GetAttributeInt(ExifInterface.TagOrientation, 1));
        }
        catch (Java.IO.IOException)
        {
            return bitmap;
        }

        if (degrees == 0)
            return bitmap;

        using var matrix = new Matrix();
        matrix.PostRotate(degrees);
        var rotated = Bitmap.CreateBitmap(bitmap, 0, 0, bitmap.Width, bitmap.Height, matrix, true);
        if (!ReferenceEquals(rotated, bitmap))
            Free(bitmap);
        return rotated;
    }

    // Los pixeles se sueltan ya, sin esperar al recolector: en una carpeta con cientos de fotos se notaria.
    private static void Free(Bitmap bitmap)
    {
        bitmap.Recycle();
        bitmap.Dispose();
    }
}
