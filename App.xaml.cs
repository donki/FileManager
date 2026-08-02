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
        var root = new NavigationPage(ServiceHelper.GetRequiredService<MainPage>());
        var window = new Window(root);
#if DEBUG
        SocShared.AuthorNotes.Attach(window);   // notas de autor: SOLO Debug, desactivado en Release/produccion
#endif
        return window;
    }
}
