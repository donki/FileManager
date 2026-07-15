using Android.Widget;
using FileManager.Services;
using Application = Android.App.Application;

namespace FileManager.Platforms.Android;

/// <inheritdoc cref="IToastService"/>
public class ToastService : IToastService
{
    public void Show(string message) =>
        // Los toasts de Android solo pueden crearse desde el hilo de interfaz.
        MainThread.BeginInvokeOnMainThread(() =>
            Toast.MakeText(Application.Context, message, ToastLength.Short)?.Show());
}
