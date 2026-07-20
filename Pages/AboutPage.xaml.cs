using FileManager.Services;
using Microsoft.Extensions.Logging;

namespace FileManager.Pages;

public partial class AboutPage : ContentPage
{
    // CONFIGURACION
    private const string ContactEmail = "jsoladelarosa@gmail.com";
    private const string DonationUrl = "https://ko-fi.com/josepsola";

    private readonly ILocalizationService _l;
    private readonly ISettingsService _settings;
    private readonly ILogger<AboutPage> _logger;

    public AboutPage(ILocalizationService localization, ISettingsService settings, ILogger<AboutPage> logger)
    {
        InitializeComponent();

        _l = localization;
        _settings = settings;
        _logger = logger;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyTexts();
    }

    private void ApplyTexts()
    {
        Title = _l["AboutTitle"];

        AppNameLabel.Text = _l["AppName"];
        VersionLabel.Text = string.Format(_l.CurrentCulture, _l["AboutVersion"], AppInfo.Current.VersionString);
        DescriptionLabel.Text = _l["AppDescription"];
        CompanyLabel.Text = _l["Company"];

        ContactTitle.Text = $"📧 {_l["AboutContact"]}";
        ContactButton.Text = ContactEmail;
        ContactHint.Text = _l["AboutContactHint"];

        PrivacyTitle.Text = $"🔒 {_l["AboutPrivacy"]}";
        PrivacyText.Text = _l["AboutPrivacyText"];

        LicenseTitle.Text = $"📄 {_l["AboutLicense"]}";
        LicenseText.Text = _l["AboutLicenseText"];

        DonationTitle.Text = $"☕ {_l["AboutDonation"]}";
        DonationButton.Text = _l["AboutDonationButton"];
        DonationHint.Text = _l["AboutDonationHint"];

        LanguageTitle.Text = $"🌐 {_l["SettingsLanguage"]}";
        LanguageHint.Text = _l["AboutLanguageHint"];
        UpdateLanguageButtons();

        LegalTitle.Text = $"⚖️ {_l["AboutLegal"]}";
        LegalText1.Text = _l["AboutLegal1"];
        LegalText2.Text = _l["AboutLegal2"];
        WarningText.Text = _l["AboutWarning"];

        BackButton.Text = _l["Back"];
    }

    // Botones de idioma con bandera (constitucion, anexo A.9): el activo (es/en) usa el estilo
    // primario y el otro el de contorno. La eleccion se persiste igual que en Configuracion.
    private void UpdateLanguageButtons()
    {
        var isSpanish = _l.CurrentLanguage == "es";

        // El nombre del idioma se resuelve por localizacion (constitucion 8); la bandera es un
        // glifo decorativo (indicador regional) y se antepone como tal.
        SpanishButton.Text = $"🇪🇸 {_l["SettingsLanguageSpanish"]}";
        EnglishButton.Text = $"🇺🇸 {_l["SettingsLanguageEnglish"]}";

        SpanishButton.Style = LookupStyle(isSpanish ? "PrimaryButton" : "OutlineButton");
        EnglishButton.Style = LookupStyle(isSpanish ? "OutlineButton" : "PrimaryButton");
    }

    private static Style? LookupStyle(string key)
        => Application.Current?.Resources.TryGetValue(key, out var s) == true ? s as Style : null;

    private void OnSpanishClicked(object? sender, EventArgs e) => SetLanguage("es");

    private void OnEnglishClicked(object? sender, EventArgs e) => SetLanguage("en");

    private void SetLanguage(string code)
    {
        if (code == _l.CurrentLanguage)
            return;

        _settings.Language = code;
        _l.SetLanguage(code);
        ApplyTexts();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        // Con Shell la pagina puede haberse abierto desde el flyout (sin pila que desapilar) o
        // apilada desde el menu «⋮»/Configuracion. Si hay pila, se desapila; si no, se vuelve a Inicio.
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
        else if (Shell.Current is not null)
            await Shell.Current.GoToAsync("//MainPage");
    }

    private async void OnContactEmailClicked(object? sender, EventArgs e)
    {
        try
        {
            var message = new EmailMessage
            {
                Subject = _l["EmailSubject"],
                To = new List<string> { ContactEmail }
            };

            await Email.Default.ComposeAsync(message);
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlert(_l["Error"], _l["ErrorEmailNotAvailable"], _l["Ok"]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open the email client");
            await DisplayAlert(_l["Error"], $"{_l["ErrorEmail"]}: {ex.Message}", _l["Ok"]);
        }
    }

    private async void OnDonationClicked(object? sender, EventArgs e)
    {
        try
        {
            await Browser.Default.OpenAsync(new Uri(DonationUrl), new BrowserLaunchOptions
            {
                LaunchMode = BrowserLaunchMode.SystemPreferred,
                TitleMode = BrowserTitleMode.Show,
                PreferredToolbarColor = Color.FromArgb("#E67E22"),
                PreferredControlColor = Colors.White
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not open the browser, falling back to the clipboard");

            // Sin navegador el enlace se copia, para que el usuario pueda abrirlo donde quiera.
            try
            {
                await Clipboard.Default.SetTextAsync(DonationUrl);
                await DisplayAlert(_l["BrowserNotAvailable"], $"{_l["LinkCopied"]}:\n{DonationUrl}", _l["Ok"]);
            }
            catch (Exception clipboardEx)
            {
                _logger.LogError(clipboardEx, "The clipboard is not available either");
                await DisplayAlert(_l["Error"], $"{_l["ErrorBrowser"]}: {DonationUrl}", _l["Ok"]);
            }
        }
    }
}
