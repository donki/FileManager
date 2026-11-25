using System.Reflection;
using FileManager.Models;
using FileManager.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileManager.Tests;

/// <summary>
/// Doble de cualquier interfaz de MAUI (ILauncher, IShare, IBrowser, IEmail, IAppInfo): apunta cada
/// llamada y contesta con lo que diga <see cref="Respond"/>.
/// </summary>
public class Recorder<T> : DispatchProxy where T : class
{
    public List<(string Method, object?[] Args)> Calls { get; } = new();

    public Func<MethodInfo, object?[], object?> Respond { get; set; } = (_, _) => null;

    public static (T Proxy, Recorder<T> Recorder) Create()
    {
        var proxy = DispatchProxy.Create<T, Recorder<T>>();
        return (proxy, (Recorder<T>)(object)proxy);
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        args ??= Array.Empty<object?>();
        Calls.Add((targetMethod!.Name, args));
        return Respond(targetMethod, args);
    }
}

public sealed class FileActionsServiceTests : IDisposable
{
    private readonly TempFolder _tmp = new();
    private readonly (ILauncher Proxy, Recorder<ILauncher> Recorder) _launcher = Recorder<ILauncher>.Create();
    private readonly (IShare Proxy, Recorder<IShare> Recorder) _share = Recorder<IShare>.Create();
    private readonly FileActionsService _service;

    public FileActionsServiceTests()
    {
        _launcher.Recorder.Respond = (_, _) => Task.FromResult(true);
        _share.Recorder.Respond = (_, _) => Task.CompletedTask;
        _service = new FileActionsService(NullLogger<FileActionsService>.Instance, _launcher.Proxy, _share.Proxy, () => _tmp.Combine("cache"));
    }

    public void Dispose() => _tmp.Dispose();

    [Fact]
    public async Task Abrir_pide_al_sistema_el_fichero_con_su_tipo()
    {
        var file = _tmp.File("foto.jpg");

        Assert.True(await _service.OpenAsync(file));

        var request = Assert.IsType<OpenFileRequest>(_launcher.Recorder.Calls.Single().Args[0]);
        Assert.Equal(file, request.File!.FullPath);
        Assert.Equal("image/jpeg", request.File.ContentType);
    }

    [Fact]
    public async Task Abrir_sin_app_para_ese_tipo_devuelve_false()
    {
        _launcher.Recorder.Respond = (_, _) => Task.FromException<bool>(new InvalidOperationException("ninguna app"));

        Assert.False(await _service.OpenAsync(_tmp.File("raro.xyz")));
    }

    [Fact]
    public async Task Compartir_un_fichero_usa_la_peticion_de_uno()
    {
        var file = _tmp.File("a.pdf");

        await _service.ShareAsync(new[] { file }, "Compartir");

        var request = Assert.IsType<ShareFileRequest>(_share.Recorder.Calls.Single().Args[0]);
        Assert.Equal("Compartir", request.Title);
        Assert.Equal(file, request.File!.FullPath);
        Assert.Equal("application/pdf", request.File.ContentType);
    }

    [Fact]
    public async Task Compartir_varios_y_una_carpeta_va_comprimida_en_la_cache()
    {
        var file = _tmp.File("a.txt");
        _tmp.File("Fotos/b.jpg");

        await _service.ShareAsync(new[] { file, _tmp.Combine("Fotos") }, "Compartir");

        var request = Assert.IsType<ShareMultipleFilesRequest>(_share.Recorder.Calls.Single().Args[0]);
        Assert.Equal(2, request.Files!.Count);
        Assert.Equal(file, request.Files[0].FullPath);
        Assert.StartsWith(_tmp.Combine("cache", "share"), request.Files[1].FullPath);
        Assert.EndsWith(".zip", request.Files[1].FullPath);
        Assert.True(File.Exists(request.Files[1].FullPath));
    }
}

public sealed class MauiAppEnvironmentTests
{
    private readonly (IAppInfo Proxy, Recorder<IAppInfo> Recorder) _info = Recorder<IAppInfo>.Create();
    private readonly (IBrowser Proxy, Recorder<IBrowser> Recorder) _browser = Recorder<IBrowser>.Create();
    private readonly (IEmail Proxy, Recorder<IEmail> Recorder) _email = Recorder<IEmail>.Create();
    private readonly MauiAppEnvironment _env;

    public MauiAppEnvironmentTests()
    {
        _info.Recorder.Respond = (m, _) => m.Name == "get_VersionString" ? "2026.11.25.0" : null;
        _browser.Recorder.Respond = (_, _) => Task.FromResult(true);
        _email.Recorder.Respond = (_, _) => Task.CompletedTask;
        _env = new MauiAppEnvironment(_info.Proxy, _browser.Proxy, _email.Proxy);
    }

    [Fact]
    public void La_version_es_la_de_la_app()
    {
        Assert.Equal("2026.11.25.0", _env.VersionString);
    }

    [Fact]
    public async Task Abrir_una_web_va_al_navegador_preferido_del_sistema()
    {
        var uri = new Uri("https://example.org/");

        await _env.OpenUrlAsync(uri);

        var (method, args) = _browser.Recorder.Calls.Single();
        Assert.Equal("OpenAsync", method);
        Assert.Equal(uri, args[0]);
        Assert.Equal(BrowserLaunchMode.SystemPreferred, ((BrowserLaunchOptions)args[1]!).LaunchMode);
    }

    [Fact]
    public async Task Correo_con_asunto_y_destinatario()
    {
        Assert.True(await _env.ComposeEmailAsync("Asunto", "a@b.c"));

        var message = (EmailMessage)_email.Recorder.Calls.Single().Args[0]!;
        Assert.Equal("Asunto", message.Subject);
        Assert.Equal(new[] { "a@b.c" }, message.To);
    }

    [Fact]
    public async Task Sin_correo_en_el_dispositivo_devuelve_false()
    {
        _email.Recorder.Respond = (_, _) => Task.FromException(new FeatureNotSupportedException());

        Assert.False(await _env.ComposeEmailAsync("Asunto", "a@b.c"));
    }
}

public sealed class StorageVolumesTests : IDisposable
{
    private readonly TempFolder _tmp = new();
    private readonly FileSystemService _fs = new(NullLogger<FileSystemService>.Instance);

    public void Dispose() => _tmp.Dispose();

    [Fact]
    public void Sin_Android_solo_esta_la_memoria_interna()
    {
        var volumes = _fs.GetVolumes();

        Assert.Equal(new[] { new StorageVolume("/storage/emulated/0", false) }, volumes);
    }

    [Fact]
    public void La_raiz_de_cada_memoria_es_lo_que_va_antes_de_Android()
    {
        var existing = new HashSet<string> { "/storage/1234-ABCD", "/storage/emulated/0" };

        var volumes = _fs.BuildVolumes("/storage/emulated/0", () => new string?[]
        {
            "/storage/emulated/0/Android/data/com.socratic.filemanager/files",   // la interna: repetida
            "/storage/1234-ABCD/Android/data/com.socratic.filemanager/files",    // la tarjeta SD
            "/storage/1234-abcd/Android/data/com.socratic.filemanager/files",    // la misma, en otra caja
            null,                                                                 // memoria no montada
            "/Android/data/x",                                                    // sin raiz delante
            "/mnt/sin-carpeta-de-android",
            "/storage/9999-0000/Android/data/com.socratic.filemanager/files",    // ya no existe
        }, existing.Contains);

        Assert.Equal(new[]
        {
            new StorageVolume("/storage/emulated/0", false),
            new StorageVolume("/storage/1234-ABCD", true),
        }, volumes);
    }

    [Fact]
    public void Si_Android_falla_al_listar_queda_la_interna()
    {
        var volumes = _fs.BuildVolumes("/storage/emulated/0",
            () => throw new InvalidOperationException("sin contexto"), _ => true);

        Assert.Single(volumes);
        Assert.False(volumes[0].IsRemovable);
    }

    [Fact]
    public void Una_lista_que_falla_a_medias_conserva_lo_leido()
    {
        static IEnumerable<string?> Dirs()
        {
            yield return "/storage/SD/Android/data/p/files";
            throw new IOException("tarjeta retirada");
        }

        var volumes = _fs.BuildVolumes("/storage/emulated/0", Dirs, _ => true);

        Assert.Equal(2, volumes.Count);
        Assert.Equal("/storage/SD", volumes[1].RootPath);
    }
}
