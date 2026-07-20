using FileManager.Helpers;
using FileManager.Services;

namespace FileManager;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Shell con menu hamburguesa (constitucion A.9): la navegacion de primer nivel
        // (Inicio, Configuracion, Acerca de) vive en el flyout.
        var shell = new AppShell(ServiceHelper.GetRequiredService<ILocalizationService>());
        return new Window(shell);
    }
}
