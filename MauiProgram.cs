using FileManager.Helpers;
using FileManager.Pages;
using FileManager.Services;
using Microsoft.Extensions.Logging;

namespace FileManager;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // Sin fuentes propias: se usa la del sistema, que ya cubre los idiomas soportados.
        builder.UseMauiApp<App>();

#if DEBUG
        // Trazas de depuracion solo en Debug (constitucion 17).
        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#else
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
#endif

        // Servicios (constitucion 4: inyeccion de dependencias para todos los servicios).
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<ILocalizationService, LocalizationService>();
        builder.Services.AddSingleton<IFileSystemService, FileSystemService>();
        builder.Services.AddSingleton<IFileClipboardService, FileClipboardService>();
        builder.Services.AddSingleton<IFileActionsService, FileActionsService>();
        // El registro se perdio en el incidente de reorganizacion y la app abortaba al arrancar:
        // App.CreateWindow resuelve MainPage, que pide UpdateService por constructor.
        builder.Services.AddSingleton<UpdateService>();
#if ANDROID
        builder.Services.AddSingleton<IStoragePermissionService, Platforms.Android.StoragePermissionService>();
        builder.Services.AddSingleton<IToastService, Platforms.Android.ToastService>();
#endif

        // Paginas (carpeta Pages, sin ViewModels: ver README, seccion Arquitectura).
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<AboutPage>();

        var app = builder.Build();
        ServiceHelper.Initialize(app.Services);
        return app;
    }
}
