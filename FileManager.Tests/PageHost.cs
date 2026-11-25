using System.Globalization;
using System.Reflection;
using FileManager.Helpers;
using FileManager.Services;
using FileManager.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.Dispatching;

namespace FileManager.Tests;

/// <summary>
/// Las paginas de verdad (XAML compilado y code-behind) sin plataforma debajo: la App con sus estilos,
/// los servicios dobles en el contenedor de <see cref="ServiceHelper"/> y un Dispatcher que ejecuta
/// en el acto. Las pruebas pulsan los controles como lo haria el usuario. Receta de
/// Uninstaller.Tests (2026-11-24).
/// </summary>
public abstract class PageHost : IDisposable
{
    private readonly CultureInfo? _culture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? _uiCulture = CultureInfo.DefaultThreadCurrentUICulture;

    protected readonly TempFolder Tmp = new();
    protected readonly MemorySettings Settings = new();
    protected readonly LocalizationService Loc;
    protected readonly RootedFileSystem Files;
    protected readonly FileClipboardService Clipboard = new();
    protected readonly FakeActions Actions = new();
    protected readonly FakePermissions Permissions = new();
    protected readonly FakeToast Toast = new();
    protected readonly FakeEnvironment Environment = new();
    protected readonly TestDispatcher Dispatcher = new();
    protected readonly ServiceProvider Services;

    protected PageHost()
    {
        Preferences.Clear();
        Files = new RootedFileSystem(Tmp.Path);
        Loc = new LocalizationService(Settings, NullLogger<LocalizationService>.Instance);
        Services = new ServiceCollection()
            .AddSingleton<ISettingsService>(Settings)
            .AddSingleton<ILocalizationService>(Loc)
            .AddSingleton<IFileSystemService>(Files)
            .AddSingleton<IFileClipboardService>(Clipboard)
            .AddSingleton<IFileActionsService>(Actions)
            .AddSingleton<IStoragePermissionService>(Permissions)
            .AddSingleton<IToastService>(Toast)
            .AddSingleton<IAppEnvironment>(Environment)
            // Sin red: la comprobacion de version recibe siempre «estas al dia».
            .AddSingleton(new UpdateService(Loc, Environment, new HttpClient(StubHttpHandler.Json("{\"version\":\"2000.1.1.0\"}"))))
            .AddSingleton<Pages.MainPage>()
            .AddTransient<Pages.SettingsPage>()
            .AddTransient<Pages.AboutPage>()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .BuildServiceProvider();
        ServiceHelper.Initialize(Services);
        DispatcherProvider.SetCurrent(new TestDispatcherProvider(Dispatcher));
        Application.Current = new App();
        // Las animaciones (dialogos) buscan su gestor en la App: uno real, mil veces mas rapido, para
        // que terminen en el primer golpe de reloj.
        var context = new MauiContext(new ServiceCollection()
            .AddSingleton<Microsoft.Maui.Animations.IAnimationManager>(
                new Microsoft.Maui.Animations.AnimationManager(new Microsoft.Maui.Animations.Ticker()) { SpeedModifier = 1000 })
            .BuildServiceProvider());
        var handler = new TestAppHandler();
        handler.SetMauiContext(context);
        Application.Current.Handler = handler;
    }

    protected string L(string key) => Loc[key];

    /// <summary>Un control de la pagina por su x:Name.</summary>
    protected static T Named<T>(Element page, string name) where T : class =>
        page.FindByName(name) as T ?? throw new InvalidOperationException($"No hay {typeof(T).Name} '{name}'.");

    /// <summary>Pulsa un boton como el usuario: dispara su Clicked.</summary>
    protected static void Click(Element page, string name)
    {
        switch (page.FindByName(name))
        {
            case Button b: ((IButtonController)b).SendClicked(); break;
            case ImageButton i: ((IButtonController)i).SendClicked(); break;
            default: throw new InvalidOperationException($"'{name}' no es un boton.");
        }
    }

    /// <summary>Llama a un metodo no publico (OnAppearing, OnBackButtonPressed...).</summary>
    protected static object? Invoke(object target, string method, params object?[] args)
    {
        for (var type = target.GetType(); type is not null; type = type.BaseType)
        {
            var m = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .FirstOrDefault(x => x.Name == method && x.GetParameters().Length == args.Length);
            if (m is not null)
                return m.Invoke(target, args);
        }
        throw new MissingMethodException(target.GetType().Name, method);
    }

    /// <summary>
    /// Contesta el dialogo que la pagina tiene abierto (los de SocShared.ModernDialog son una capa
    /// dentro de la propia pagina) pulsando el boton con ese texto. Devuelve los textos del dialogo.
    /// </summary>
    protected static async Task<string> AnswerDialog(ContentPage page, string option)
    {
        for (var i = 0; i < 200; i++)
        {
            var overlay = Overlay(page);
            if (overlay?.GetVisualTreeDescendants().OfType<Button>().FirstOrDefault(b => b.Text == option) is { } button)
            {
                var text = string.Join(" | ", overlay.GetVisualTreeDescendants().OfType<Label>().Select(l => l.Text));
                ((IButtonController)button).SendClicked();
                await Settle();
                return text;
            }
            await Task.Delay(10);
        }
        throw new InvalidOperationException($"No hay dialogo con la opcion '{option}'.");
    }

    /// <summary>Escribe en el campo del dialogo de texto abierto y pulsa el boton dado.</summary>
    protected static async Task AnswerPrompt(ContentPage page, string text, string option)
    {
        for (var i = 0; i < 200 && Overlay(page)?.GetVisualTreeDescendants().OfType<Entry>().FirstOrDefault() is null; i++)
            await Task.Delay(10);
        var entry = Overlay(page)?.GetVisualTreeDescendants().OfType<Entry>().FirstOrDefault()
            ?? throw new InvalidOperationException("No hay dialogo de texto.");
        entry.Text = text;
        await AnswerDialog(page, option);
    }

    private static Grid? Overlay(ContentPage page) =>
        (page.Content as Grid)?.Children.OfType<Grid>().FirstOrDefault(g => g.StyleId == "__modernDialogOverlay");

    /// <summary>Hay un dialogo abierto en la pagina.</summary>
    protected static bool DialogOpen(ContentPage page) => Overlay(page) is not null;

    /// <summary>Deja correr lo que el code-behind lanzo sin esperar (async void).</summary>
    protected static async Task Settle(int rounds = 5)
    {
        for (var i = 0; i < rounds; i++)
            await Task.Delay(10);
    }

    /// <summary>Espera a que se cumpla una condicion (lo lanzado en async void acaba cuando acaba).</summary>
    protected static async Task Until(Func<bool> condition, string what)
    {
        for (var i = 0; i < 300; i++)
        {
            if (condition())
                return;
            await Task.Delay(10);
        }
        throw new TimeoutException("No llego: " + what);
    }

    public virtual void Dispose()
    {
        Application.Current = null;
        Services.Dispose();
        Tmp.Dispose();
        CultureInfo.DefaultThreadCurrentCulture = _culture;
        CultureInfo.DefaultThreadCurrentUICulture = _uiCulture;
        GC.SuppressFinalize(this);
    }
}

/// <summary>Dispatcher de prueba: ejecuta en el acto.</summary>
public sealed class TestDispatcher : IDispatcher
{
    public int Dispatched { get; private set; }

    public bool IsDispatchRequired => false;

    public bool Dispatch(Action action)
    {
        Dispatched++;
        action();
        return true;
    }

    public bool DispatchDelayed(TimeSpan delay, Action action) => Dispatch(action);

    public IDispatcherTimer CreateTimer() => new TestTimer();
}

public sealed class TestTimer : IDispatcherTimer
{
    public TimeSpan Interval { get; set; }
    public bool IsRepeating { get; set; } = true;
    public bool IsRunning { get; private set; }
#pragma warning disable CS0067 // sin reloj: nadie lo dispara
    public event EventHandler? Tick;
#pragma warning restore CS0067
    public void Start() => IsRunning = true;
    public void Stop() => IsRunning = false;
}

/// <summary>Manejador de la App sin plataforma: solo aporta el MauiContext con el gestor de animaciones.</summary>
public sealed class TestAppHandler() : Microsoft.Maui.Handlers.ElementHandler<IApplication, object>(new PropertyMapper<IApplication, TestAppHandler>())
{
    protected override object CreatePlatformElement() => new();
}

public sealed class TestDispatcherProvider(IDispatcher dispatcher) : IDispatcherProvider
{
    public IDispatcher? GetForCurrentThread() => dispatcher;
}
