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
        return new Window(root);
    }
}
