using FileManager.Helpers;
using FileManager.Services;
using FileManager.ViewModels;
using Microsoft.Extensions.Logging;

namespace FileManager.Pages;

/// <summary>Acerca de: enlace fino con <see cref="AboutViewModel"/>.</summary>
public partial class AboutPage : ContentPage
{
    private readonly ILocalizationService _l;
    private readonly AboutViewModel _vm;
    private readonly IDialogService _dialogs;

    public AboutPage(ILocalizationService localization, ISettingsService settings, IAppEnvironment environment, ILogger<AboutPage> logger)
    {
        InitializeComponent();
        _l = localization;
        _vm = new AboutViewModel(localization, settings, environment, logger);
        _dialogs = new PageDialogService(this);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyTexts();
    }

    private void ApplyTexts()
    {
        Title = _l[AboutViewModel.TitleKey];
        PageTexts.Apply(this, AboutViewModel.StaticTexts, _l);
        VersionLabel.Text = _vm.VersionText;
        ContactButton.Text = AboutViewModel.ContactEmail;

        // Botones de idioma con bandera (constitucion, anexo A.9): el activo usa el estilo primario.
        SpanishButton.Style = LookupStyle(_vm.IsSpanish ? "PrimaryButton" : "OutlineButton");
        EnglishButton.Style = LookupStyle(_vm.IsSpanish ? "OutlineButton" : "PrimaryButton");
    }

    private static Style? LookupStyle(string key)
        => Application.Current?.Resources.TryGetValue(key, out var s) == true ? s as Style : null;

    private void OnSpanishClicked(object? sender, EventArgs e) => SetLanguage("es");

    private void OnEnglishClicked(object? sender, EventArgs e) => SetLanguage("en");

    private void SetLanguage(string code)
    {
        if (_vm.SetLanguage(code))
            ApplyTexts();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        // Con Shell la pagina puede haberse abierto desde el flyout (sin pila que desapilar) o apilada
        // desde el menu «⋮»/Configuracion. Si hay pila, se desapila; si no, se vuelve a Inicio.
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
        else if (Shell.Current is not null)
            await Shell.Current.GoToAsync("//MainPage");
    }

    private async void OnContactEmailClicked(object? sender, EventArgs e) => await _vm.ContactAsync(_dialogs);
}
