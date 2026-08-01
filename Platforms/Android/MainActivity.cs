using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;
using AndroidView = Android.Views.View;

namespace FileManager;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ApplySystemBarInsets();
    }

    /// <summary>
    /// Desde Android 15 el sistema dibuja la aplicacion de borde a borde y las barras de
    /// estado y navegacion quedan por encima del contenido. Se separa el contenido con el
    /// tamano real de esas barras y se pinta el hueco con el azul de la marca.
    /// </summary>
    private void ApplySystemBarInsets()
    {
        var content = FindViewById(global::Android.Resource.Id.Content);
        if (content is null)
            return;

        content.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#2A1CB8"));

        ViewCompat.SetOnApplyWindowInsetsListener(content, new SystemBarInsetsListener());

        // Iconos claros sobre el azul de la marca.
        var controller = Window is not null
            ? WindowCompat.GetInsetsController(Window, Window.DecorView)
            : null;

        if (controller is not null)
        {
            controller.AppearanceLightStatusBars = false;
            controller.AppearanceLightNavigationBars = false;
        }
    }

    private class SystemBarInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat OnApplyWindowInsets(AndroidView? view, WindowInsetsCompat? insets)
        {
            // Los insets se consumen siempre: ninguna vista hija debe volver a aplicarlos.
            var consumed = WindowInsetsCompat.Consumed!;

            if (view is null || insets is null)
                return consumed;

            var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout());
            if (bars is not null)
                view.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);

            return consumed;
        }
    }
}
