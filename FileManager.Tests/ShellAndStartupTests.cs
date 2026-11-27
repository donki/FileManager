using System.Reflection;
using FileManager.Helpers;
using FileManager.Pages;
using FileManager.Services;

namespace FileManager.Tests;

/// <summary>El Shell (menu lateral, textos, atras), la ventana de la App y el arranque de MAUI.</summary>
public sealed class ShellAndStartupTests : PageHost
{
    private static FlyoutItem Item(AppShell shell, string name) => Named<FlyoutItem>(shell, name);

    [Fact]
    public void Shell_menu_y_version_en_el_idioma_elegido()
    {
        Environment.VersionString = "2026.11.25.0";
        var shell = new AppShell();

        Assert.Equal(L("Home"), Item(shell, "HomeFlyoutItem").Title);
        Assert.Equal(L("Settings"), Item(shell, "SettingsFlyoutItem").Title);
        Assert.Equal(L("About"), Item(shell, "AboutFlyoutItem").Title);
        Assert.Equal("v2026.11.25.0", Named<Label>(shell, "VersionLabel").Text);

        Loc.SetLanguage("es");

        Assert.Equal(L("Home"), Item(shell, "HomeFlyoutItem").Title);
        Assert.Equal(L("Settings"), Item(shell, "SettingsFlyoutItem").Title);
        Assert.NotEqual("Settings", Item(shell, "SettingsFlyoutItem").Title);
    }

    [Fact]
    public void Shell_cada_entrada_crea_su_pagina_desde_el_contenedor()
    {
        var shell = new AppShell();

        Assert.IsType<MainPage>(Named<ShellContent>(shell, "HomeContent").ContentTemplate.CreateContent());
        Assert.IsType<SettingsPage>(Named<ShellContent>(shell, "SettingsContent").ContentTemplate.CreateContent());
        Assert.IsType<AboutPage>(Named<ShellContent>(shell, "AboutContent").ContentTemplate.CreateContent());
    }

    [Fact]
    public void Shell_atras_desde_otra_entrada_del_menu_vuelve_a_Inicio()
    {
        var shell = new AppShell();
        shell.CurrentItem = Item(shell, "SettingsFlyoutItem");

        Assert.True((bool)Invoke(shell, "OnBackButtonPressed")!);
        Assert.Same(Item(shell, "HomeFlyoutItem"), shell.CurrentItem);
    }

    [Fact]
    public void Shell_atras_en_Inicio_lo_decide_la_pagina()
    {
        var shell = new AppShell();
        _ = new Window(shell);
        shell.CurrentItem = Item(shell, "HomeFlyoutItem");
        // Sin plataforma nadie crea la pagina de la entrada: se crea como lo haria el handler.
        var home = (MainPage)((IShellContentController)Named<ShellContent>(shell, "HomeContent")).GetOrCreateContent();
        Assert.Same(home, shell.CurrentPage);

        // En la raiz de Inicio la pagina no lo consume: la app se oculta.
        Assert.False((bool)Invoke(shell, "OnBackButtonPressed")!);
        Assert.Same(Item(shell, "HomeFlyoutItem"), shell.CurrentItem);
    }

    [Fact]
    public void La_app_crea_su_ventana_con_el_shell()
    {
        var window = (Window)typeof(App).GetMethod("CreateWindow", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Application.Current, [null])!;

        Assert.IsType<AppShell>(window.Page);
    }

    [Fact]
    public void El_arranque_registra_todos_los_servicios_y_paginas()
    {
        var app = MauiProgram.CreateMauiApp();
        try
        {
            Assert.IsType<SettingsService>(app.Services.GetService<ISettingsService>());
            Assert.IsType<LocalizationService>(app.Services.GetService<ILocalizationService>());
            Assert.IsType<FileSystemService>(app.Services.GetService<IFileSystemService>());
            Assert.IsType<FileClipboardService>(app.Services.GetService<IFileClipboardService>());
            Assert.IsType<FileActionsService>(app.Services.GetService<IFileActionsService>());
            Assert.NotNull(app.Services.GetService<UpdateService>());
            Assert.IsType<MauiAppEnvironment>(app.Services.GetService<IAppEnvironment>());
            // Fuera de Android (el banco) no hay quien haga miniaturas: todo con icono.
            Assert.IsType<NoThumbnailGenerator>(app.Services.GetService<IThumbnailGenerator>());
            // ServiceHelper queda apuntando al contenedor de la app.
            Assert.Same(app.Services.GetService<ISettingsService>(), ServiceHelper.GetRequiredService<ISettingsService>());
        }
        finally
        {
            app.Dispose();
        }
    }

    [Fact]
    public void ServiceHelper_sin_inicializar_lo_dice()
    {
        var field = typeof(ServiceHelper).GetField("_services", BindingFlags.Static | BindingFlags.NonPublic)!;
        var saved = field.GetValue(null);
        field.SetValue(null, null);
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => ServiceHelper.GetRequiredService<ISettingsService>());
            Assert.Contains("MauiProgram", ex.Message);
        }
        finally
        {
            field.SetValue(null, saved);
        }
    }

    [Fact]
    public void Textos_de_pagina_con_un_control_que_no_es_de_texto_avisan()
    {
        var page = new ContentPage { Content = new Entry() };
        var scope = new Microsoft.Maui.Controls.Internals.NameScope();
        Microsoft.Maui.Controls.Internals.NameScope.SetNameScope(page, scope);
        ((Microsoft.Maui.Controls.Internals.INameScope)scope).RegisterName("Campo", page.Content);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PageTexts.Apply(page, new Dictionary<string, string> { ["Campo"] = "About" }, Loc));
        Assert.Contains("Campo", ex.Message);
    }
}
