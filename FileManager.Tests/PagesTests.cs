using FileManager.Helpers;
using FileManager.Pages;
using FileManager.ViewModels;

namespace FileManager.Tests;

/// <summary>Configuracion y Acerca de, de verdad: volcado de textos y estado, y cada toque.</summary>
public sealed class SettingsPageTests : PageHost
{
    private SettingsPage Appeared()
    {
        var page = ServiceHelper.GetRequiredService<SettingsPage>();
        Invoke(page, "OnAppearing");
        return page;
    }

    [Fact]
    public void Al_aparecer_pone_titulo_textos_interruptores_y_estado_del_permiso()
    {
        Settings.ShowHiddenFiles = true;
        Settings.ConfirmDelete = false;

        var page = Appeared();

        Assert.Equal(L(SettingsViewModel.TitleKey), page.Title);
        foreach (var (name, key) in SettingsViewModel.StaticTexts)
        {
            var control = page.FindByName(name);
            var text = control switch { Label l => l.Text, Button b => b.Text, _ => null };
            Assert.Equal(L(key), text);
        }
        Assert.True(Named<Switch>(page, "ShowHiddenSwitch").IsToggled);
        Assert.False(Named<Switch>(page, "ConfirmDeleteSwitch").IsToggled);
        Assert.Equal(L("SettingsPermissionOk"), Named<Label>(page, "PermissionStateValue").Text);
        Assert.Equal((Color)Application.Current!.Resources["Success"], Named<Label>(page, "PermissionStateValue").TextColor);
        // Idioma activo (ingles) con el estilo primario.
        Assert.Same(Application.Current.Resources["PrimaryButton"], Named<Button>(page, "EnglishButton").Style);
        Assert.Same(Application.Current.Resources["OutlineButton"], Named<Button>(page, "SpanishButton").Style);
        // Volcar los interruptores no guarda nada.
        Assert.True(Settings.ShowHiddenFiles);
        Assert.False(Settings.ConfirmDelete);
    }

    [Fact]
    public void Sin_permiso_lo_dice_en_rojo()
    {
        Permissions.HasFullAccess = false;
        var page = Appeared();

        Assert.Equal(L("SettingsPermissionKo"), Named<Label>(page, "PermissionStateValue").Text);
        Assert.Equal((Color)Application.Current!.Resources["Danger"], Named<Label>(page, "PermissionStateValue").TextColor);
    }

    [Fact]
    public void Los_botones_de_idioma_cambian_el_idioma_y_los_textos_en_el_acto()
    {
        var page = Appeared();

        Click(page, "SpanishButton");

        Assert.Equal("es", Settings.Language);
        Assert.Equal("es", Loc.CurrentLanguage);
        Assert.Equal(L(SettingsViewModel.TitleKey), page.Title);
        Assert.Same(Application.Current!.Resources["PrimaryButton"], Named<Button>(page, "SpanishButton").Style);

        // El idioma que ya esta puesto no repinta nada.
        Click(page, "SpanishButton");
        Click(page, "EnglishButton");
        Assert.Equal("en", Settings.Language);
    }

    [Fact]
    public void Los_interruptores_guardan()
    {
        var page = Appeared();

        Named<Switch>(page, "ShowHiddenSwitch").IsToggled = true;
        Named<Switch>(page, "ConfirmDeleteSwitch").IsToggled = false;

        Assert.True(Settings.ShowHiddenFiles);
        Assert.False(Settings.ConfirmDelete);
    }

    [Fact]
    public async Task Permiso_pide_acceso_y_si_falla_lo_explica()
    {
        var page = Appeared();

        Click(page, "PermissionButton");
        await Until(() => Permissions.Requests == 1, "pedir el permiso");

        Permissions.RequestError = new InvalidOperationException("sin ajustes");
        Click(page, "PermissionButton");
        var text = await AnswerDialog(page, L("Ok"));
        Assert.Contains("sin ajustes", text);
    }

    [Fact]
    public async Task Acerca_de_se_apila()
    {
        var page = Appeared();
        var nav = new NavigationPage(page);

        Click(page, "AboutButton");

        await Until(() => nav.Navigation.NavigationStack.Count == 2, "abrir Acerca de");
        Assert.IsType<AboutPage>(nav.CurrentPage);
    }
}

public sealed class AboutPageTests : PageHost
{
    private AboutPage Appeared()
    {
        var page = ServiceHelper.GetRequiredService<AboutPage>();
        Invoke(page, "OnAppearing");
        return page;
    }

    [Fact]
    public void Al_aparecer_pone_titulo_textos_version_y_correo()
    {
        Environment.VersionString = "2026.11.25.0";
        var page = Appeared();

        Assert.Equal(L(AboutViewModel.TitleKey), page.Title);
        foreach (var (name, key) in AboutViewModel.StaticTexts)
        {
            var control = page.FindByName(name);
            var text = control switch { Label l => l.Text, Button b => b.Text, _ => null };
            Assert.Equal(L(key), text);
        }
        Assert.Contains("2026.11.25.0", Named<Label>(page, "VersionLabel").Text);
        Assert.Equal(AboutViewModel.ContactEmail, Named<Button>(page, "ContactButton").Text);
        Assert.Same(Application.Current!.Resources["PrimaryButton"], Named<Button>(page, "EnglishButton").Style);
    }

    [Fact]
    public void Los_botones_de_idioma_cambian_el_idioma()
    {
        var page = Appeared();

        Click(page, "SpanishButton");
        Assert.Equal("es", Settings.Language);
        Assert.Equal(L(AboutViewModel.TitleKey), page.Title);
        Assert.Same(Application.Current!.Resources["PrimaryButton"], Named<Button>(page, "SpanishButton").Style);

        Click(page, "SpanishButton");
        Click(page, "EnglishButton");
        Assert.Equal("en", Loc.CurrentLanguage);
    }

    [Fact]
    public async Task Contacto_abre_el_correo_y_sin_correo_lo_explica()
    {
        var page = Appeared();

        Click(page, "ContactButton");
        await Until(() => Environment.Emails.Count == 1, "abrir el correo");
        Assert.Equal(AboutViewModel.ContactEmail, Environment.Emails[0].To);

        Environment.EmailAvailable = false;
        Click(page, "ContactButton");
        var text = await AnswerDialog(page, L("Ok"));
        Assert.Contains(L("ErrorEmailNotAvailable"), text);
    }

    [Fact]
    public async Task Volver_desapila_si_esta_apilada()
    {
        var root = new ContentPage();
        var nav = new NavigationPage(root);
        var page = Appeared();
        await nav.PushAsync(page, false);

        Click(page, "BackButton");

        await Until(() => nav.Navigation.NavigationStack.Count == 1, "desapilar");
    }

    [Fact]
    public async Task Volver_sin_pila_ni_Shell_no_falla()
    {
        var page = Appeared();

        Click(page, "BackButton");
        await Settle();

        Assert.False(DialogOpen(page));
    }
}
