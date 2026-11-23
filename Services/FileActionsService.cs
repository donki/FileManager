using FileManager.Helpers;
using Microsoft.Extensions.Logging;

namespace FileManager.Services;

/// <inheritdoc cref="IFileActionsService"/>
public class FileActionsService : IFileActionsService
{
    private readonly ILogger<FileActionsService> _logger;

    public FileActionsService(ILogger<FileActionsService> logger) => _logger = logger;

    public async Task<bool> OpenAsync(string path)
    {
        var request = new OpenFileRequest
        {
            File = new ReadOnlyFile(path, MimeTypes.ForPath(path))
        };

        try
        {
            await Launcher.Default.OpenAsync(request);
            return true;
        }
        catch (Exception ex)
        {
            // Android lanza ActivityNotFoundException cuando no hay ninguna app registrada
            // para el tipo de fichero; el llamante lo traduce a un mensaje para el usuario.
            _logger.LogInformation("No app available to open the file: {Message}", ex.Message);
            return false;
        }
    }

    public async Task ShareAsync(IReadOnlyList<string> paths, string title)
    {
        // Las carpetas se comprimen fuera del hilo de la interfaz: pueden tardar.
        var workDir = Path.Combine(FileSystem.CacheDirectory, "share");
        var files = await Task.Run(() => SharePreparer.Prepare(paths, workDir));
        var mime = MimeTypes.Common(files);

#if ANDROID
        if (ShareNative(files, mime, title))
            return;
#endif

        // Respaldo (p. ej. una tarjeta SD fuera de las rutas del FileProvider): MAUI copia el
        // fichero a su cache y lo comparte desde alli.
        if (files.Count == 1)
        {
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = title,
                File = new ShareFile(files[0], MimeTypes.ForPath(files[0]))
            });
        }
        else
        {
            await Share.Default.RequestAsync(new ShareMultipleFilesRequest
            {
                Title = title,
                Files = files.Select(f => new ShareFile(f, MimeTypes.ForPath(f))).ToList()
            });
        }
    }

#if ANDROID
    /// <summary>
    /// ACTION_SEND / ACTION_SEND_MULTIPLE con URIs content:// del FileProvider de MAUI
    /// (autoridad «paquete.fileProvider», que cubre todo el almacenamiento externo), sin copiar
    /// el fichero, y con el tipo MIME comun de todos.
    /// </summary>
    private bool ShareNative(IReadOnlyList<string> files, string mime, string title)
    {
        try
        {
            var activity = Platform.CurrentActivity;
            var context = (Android.Content.Context?)activity ?? Android.App.Application.Context;
            var authority = context.PackageName + ".fileProvider";
            var uris = files
                .Select(f => AndroidX.Core.Content.FileProvider.GetUriForFile(context, authority, new Java.IO.File(f))!)
                .ToList();

            Android.Content.Intent intent;
            if (uris.Count == 1)
            {
                intent = new Android.Content.Intent(Android.Content.Intent.ActionSend);
                intent.PutExtra(Android.Content.Intent.ExtraStream, uris[0]);
            }
            else
            {
                intent = new Android.Content.Intent(Android.Content.Intent.ActionSendMultiple);
                intent.PutParcelableArrayListExtra(
                    Android.Content.Intent.ExtraStream,
                    uris.Cast<Android.OS.IParcelable>().ToList());
            }

            intent.SetType(mime);

            // Con ClipData el permiso de lectura llega tambien a traves del selector.
            var clip = Android.Content.ClipData.NewRawUri(title, uris[0])!;
            foreach (var uri in uris.Skip(1))
                clip.AddItem(new Android.Content.ClipData.Item(uri));
            intent.ClipData = clip;
            intent.AddFlags(Android.Content.ActivityFlags.GrantReadUriPermission);

            var chooser = Android.Content.Intent.CreateChooser(intent, title)!;
            chooser.AddFlags(Android.Content.ActivityFlags.GrantReadUriPermission);
            if (activity is null)
                chooser.AddFlags(Android.Content.ActivityFlags.NewTask);

            context.StartActivity(chooser);
            return true;
        }
        catch (Java.Lang.IllegalArgumentException ex)
        {
            // El fichero esta fuera de las rutas del FileProvider: se usa el respaldo de MAUI.
            _logger.LogInformation("Native share not possible, using the fallback: {Message}", ex.Message);
            return false;
        }
    }
#endif
}
