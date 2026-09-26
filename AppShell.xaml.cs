using FileManager.Helpers;
using FileManager.Pages;
using FileManager.Services;

namespace FileManager;

/// <summary>
/// Shell con el menú hamburguesa (A.9): Inicio, Configuración y Acerca de. Hasta la 2026.09.26.0 la
/// aplicación arrancaba con un NavigationPage y el botón de menú de la barra no tenía nada que abrir.
/// </summary>
public partial class AppShell : Shell
{
    private readonly ILocalizationService _l;

    public AppShell()
    {
        InitializeComponent();
        _l = ServiceHelper.GetRequiredService<ILocalizationService>();

        HomeContent.ContentTemplate = new DataTemplate(() => ServiceHelper.GetRequiredService<MainPage>());
        SettingsContent.ContentTemplate = new DataTemplate(() => ServiceHelper.GetRequiredService<SettingsPage>());
        AboutContent.ContentTemplate = new DataTemplate(() => ServiceHelper.GetRequiredService<AboutPage>());

        _l.LanguageChanged += (_, _) => UpdateMenuTexts();
        UpdateMenuTexts();
    }

    private void UpdateMenuTexts()
    {
        HomeFlyoutItem.Title = _l["Home"];
        SettingsFlyoutItem.Title = _l["Settings"];
        AboutFlyoutItem.Title = _l["About"];
        VersionLabel.Text = $"v{AppInfo.Current.VersionString}";
    }
}
