using FileManager.Services;

namespace FileManager;

public partial class AppShell : Shell
{
    private readonly ILocalizationService _l;

    public AppShell(ILocalizationService localization)
    {
        InitializeComponent();

        _l = localization;
        ApplyTexts();
        _l.LanguageChanged += (_, _) => MainThread.BeginInvokeOnMainThread(ApplyTexts);
    }

    // Los textos del menu se resuelven con el servicio de localizacion (constitucion 8): nunca
    // literales en el XAML. Se refrescan al cambiar el idioma.
    private void ApplyTexts()
    {
        HeaderTitle.Text = _l["AppName"];
        HomeItem.Title = _l["Home"];
        SettingsItem.Title = _l["Settings"];
        AboutItem.Title = _l["About"];
    }
}
