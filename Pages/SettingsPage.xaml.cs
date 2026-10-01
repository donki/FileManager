using FileManager.Helpers;
using FileManager.Services;
using FileManager.ViewModels;
using Microsoft.Extensions.Logging;

namespace FileManager.Pages;

/// <summary>Configuracion: enlace fino con <see cref="SettingsViewModel"/>.</summary>
public partial class SettingsPage : ContentPage
{
    private readonly ILocalizationService _l;
    private readonly SettingsViewModel _vm;
    private readonly IDialogService _dialogs;

    public SettingsPage(
        ISettingsService settings,
        ILocalizationService localization,
        IStoragePermissionService permissions,
        ILogger<SettingsPage> logger)
    {
        InitializeComponent();
        _l = localization;
        _vm = new SettingsViewModel(settings, localization, permissions, logger);
        _dialogs = new PageDialogService(this);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    private void Refresh()
    {
        Title = _l[SettingsViewModel.TitleKey];
        PageTexts.Apply(this, SettingsViewModel.StaticTexts, _l);

        _vm.Loading = true;
        try
        {
            // Botones de idioma con bandera (constitucion, anexo A.9): el activo usa el estilo primario.
            SpanishButton.Style = LookupStyle(_vm.IsSpanish ? "PrimaryButton" : "OutlineButton");
            EnglishButton.Style = LookupStyle(_vm.IsSpanish ? "OutlineButton" : "PrimaryButton");
            ShowHiddenSwitch.IsToggled = _vm.ShowHiddenFiles;
            ConfirmDeleteSwitch.IsToggled = _vm.ConfirmDelete;
            PermissionStateValue.Text = _vm.PermissionStateText;
            PermissionStateValue.TextColor = (Color)Application.Current!.Resources[_vm.PermissionGranted ? "Success" : "Danger"];
        }
        finally
        {
            _vm.Loading = false;
        }
    }

    private static Style? LookupStyle(string key)
        => Application.Current?.Resources.TryGetValue(key, out var s) == true ? s as Style : null;

    private void OnSpanishClicked(object? sender, EventArgs e) => SetLanguage("es");

    private void OnEnglishClicked(object? sender, EventArgs e) => SetLanguage("en");

    private void SetLanguage(string code)
    {
        // Los textos de esta pagina tambien deben cambiar en el acto.
        if (_vm.SetLanguage(code))
            Refresh();
    }

    private void OnShowHiddenToggled(object? sender, ToggledEventArgs e) => _vm.SetShowHiddenFiles(e.Value);

    private void OnConfirmDeleteToggled(object? sender, ToggledEventArgs e) => _vm.SetConfirmDelete(e.Value);

    private async void OnPermissionClicked(object? sender, EventArgs e) => await _vm.RequestPermissionAsync(_dialogs);

    private async void OnAboutClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(ServiceHelper.GetRequiredService<AboutPage>());
}
