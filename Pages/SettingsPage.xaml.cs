using FileManager.Helpers;
using FileManager.Services;
using Microsoft.Extensions.Logging;

namespace FileManager.Pages;

public partial class SettingsPage : ContentPage
{
    /// <summary>Codigos de idioma en el mismo orden que las opciones del Picker.</summary>
    private static readonly string[] LanguageCodes = { LocalizationService.SystemLanguage, "en", "es" };

    private readonly ISettingsService _settings;
    private readonly ILocalizationService _l;
    private readonly IStoragePermissionService _permissions;
    private readonly ILogger<SettingsPage> _logger;

    /// <summary>Evita que rellenar los controles al cargar dispare sus eventos de cambio.</summary>
    private bool _loading;

    public SettingsPage(
        ISettingsService settings,
        ILocalizationService localization,
        IStoragePermissionService permissions,
        ILogger<SettingsPage> logger)
    {
        InitializeComponent();

        _settings = settings;
        _l = localization;
        _permissions = permissions;
        _logger = logger;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyTexts();
        LoadValues();
    }

    private void ApplyTexts()
    {
        Title = _l["SettingsTitle"];

        LanguageTitle.Text = $"🌐 {_l["SettingsLanguage"]}";
        LanguageHint.Text = _l["SettingsLanguageHint"];

        DisplayTitle.Text = $"👁️ {_l["SettingsDisplay"]}";
        ShowHiddenLabel.Text = _l["SettingsShowHidden"];
        ShowHiddenHint.Text = _l["SettingsShowHiddenHint"];
        ConfirmDeleteLabel.Text = _l["SettingsConfirmDelete"];
        ConfirmDeleteHint.Text = _l["SettingsConfirmDeleteHint"];

        StorageTitle.Text = $"💾 {_l["SettingsStorage"]}";
        PermissionStateLabel.Text = _l["SettingsPermissionState"];
        PermissionButton.Text = _l["PermissionOpenSettings"];

        AboutButton.Text = $"ℹ️ {_l["About"]}";

        var index = LanguagePicker.SelectedIndex;
        LanguagePicker.ItemsSource = new List<string>
        {
            _l["SettingsLanguageSystem"],
            _l["SettingsLanguageEnglish"],
            _l["SettingsLanguageSpanish"]
        };
        // Reasignar ItemsSource borra la seleccion: se restaura la que habia.
        LanguagePicker.SelectedIndex = index;
    }

    private void LoadValues()
    {
        _loading = true;
        try
        {
            var stored = _settings.Language;
            var index = Array.IndexOf(LanguageCodes, stored);
            LanguagePicker.SelectedIndex = index >= 0 ? index : 0;

            ShowHiddenSwitch.IsToggled = _settings.ShowHiddenFiles;
            ConfirmDeleteSwitch.IsToggled = _settings.ConfirmDelete;

            var granted = _permissions.HasFullAccess;
            PermissionStateValue.Text = granted ? _l["SettingsPermissionOk"] : _l["SettingsPermissionKo"];
            PermissionStateValue.TextColor = granted
                ? (Color)Application.Current!.Resources["Success"]
                : (Color)Application.Current!.Resources["Danger"];
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (_loading || LanguagePicker.SelectedIndex < 0)
            return;

        var code = LanguageCodes[LanguagePicker.SelectedIndex];
        _settings.Language = code;
        _l.SetLanguage(code);

        // Los textos de esta pagina tambien deben cambiar en el acto.
        ApplyTexts();
        LoadValues();
    }

    private void OnShowHiddenToggled(object? sender, ToggledEventArgs e)
    {
        if (_loading)
            return;

        _settings.ShowHiddenFiles = e.Value;
    }

    private void OnConfirmDeleteToggled(object? sender, ToggledEventArgs e)
    {
        if (_loading)
            return;

        _settings.ConfirmDelete = e.Value;
    }

    private async void OnPermissionClicked(object? sender, EventArgs e)
    {
        try
        {
            await _permissions.RequestFullAccessAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open the storage permission settings");
            await DisplayAlert(_l["Error"], ex.Message, _l["Ok"]);
        }
    }

    private async void OnAboutClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(ServiceHelper.GetRequiredService<AboutPage>());
}
