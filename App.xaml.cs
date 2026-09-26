using FileManager.Helpers;
using FileManager.Pages;

namespace FileManager;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Shell con menú hamburguesa (A.9); dentro, Configuración y Acerca de se siguen apilando
        // desde el menú «⋮» de la pantalla principal.
        var window = new Window(new AppShell());
#if DEBUG
        SocShared.AuthorNotes.Attach(window);   // notas de autor: SOLO Debug, desactivado en Release/produccion
#endif
        return window;
    }
}
