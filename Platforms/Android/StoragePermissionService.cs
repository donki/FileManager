using Android.Content;
using Android.OS;
using Android.Provider;
using FileManager.Services;
using Microsoft.Extensions.Logging;
using Application = Android.App.Application;

namespace FileManager.Platforms.Android;

/// <summary>
/// Implementacion Android del acceso al almacenamiento.
///
/// A partir de Android 11 (API 30) un gestor de ficheros necesita MANAGE_EXTERNAL_STORAGE,
/// que no se concede con el dialogo de permisos normal: hay que llevar al usuario a los
/// ajustes del sistema. Por debajo de API 30 basta con READ/WRITE_EXTERNAL_STORAGE.
/// </summary>
public class StoragePermissionService : IStoragePermissionService
{
    private readonly ILogger<StoragePermissionService> _logger;

    public StoragePermissionService(ILogger<StoragePermissionService> logger) => _logger = logger;

    public bool HasFullAccess
    {
        get
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
                return global::Android.OS.Environment.IsExternalStorageManager;

            return Permissions.CheckStatusAsync<Permissions.StorageWrite>().GetAwaiter().GetResult()
                == PermissionStatus.Granted;
        }
    }

    public async Task RequestFullAccessAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            await Permissions.RequestAsync<Permissions.StorageWrite>();
            await Permissions.RequestAsync<Permissions.StorageRead>();
            return;
        }

        var context = Application.Context;

        try
        {
            // Pantalla de ajustes filtrada por esta app.
            var intent = new Intent(Settings.ActionManageAppAllFilesAccessPermission,
                global::Android.Net.Uri.Parse("package:" + context.PackageName));
            intent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(intent);
        }
        catch (ActivityNotFoundException ex)
        {
            _logger.LogWarning(ex, "Per-app all-files settings screen not available, falling back to the global list");

            // Algunas ROMs no exponen la pantalla por app: se abre la lista completa.
            var fallback = new Intent(Settings.ActionManageAllFilesAccessPermission);
            fallback.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(fallback);
        }
    }
}
