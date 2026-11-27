using FileManager.Helpers;
using FileManager.Models;
using FileManager.Services;
using FileManager.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileManager.Tests;

/// <summary>Que ficheros llevan miniatura y con que clave se guardan.</summary>
public sealed class ThumbnailRulesTests
{
    private static FileItem File(string name, bool dir = false) =>
        new() { Name = name, FullPath = "/storage/emulated/0/DCIM/" + name, IsDirectory = dir, Size = 10, Modified = new DateTime(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc) };

    [Theory]
    [InlineData("foto.jpg", true, false)]
    [InlineData("FOTO.JPEG", true, false)]
    [InlineData("captura.png", true, false)]
    [InlineData("animada.gif", true, false)]
    [InlineData("imagen.webp", true, false)]
    [InlineData("movil.heic", true, false)]
    [InlineData("movil.heif", true, false)]
    [InlineData("viejo.bmp", true, false)]
    [InlineData("clip.mp4", true, true)]
    [InlineData("clip.MKV", true, true)]
    [InlineData("clip.3gp", true, true)]
    [InlineData("clip.webm", true, true)]
    [InlineData("clip.mov", true, true)]
    [InlineData("clip.m4v", true, true)]
    [InlineData("clip.avi", true, true)]
    // Imagenes que Android no decodifica y el resto de tipos: icono.
    [InlineData("dibujo.svg", false, false)]
    [InlineData("escaneo.tiff", false, false)]
    [InlineData("favicon.ico", false, false)]
    [InlineData("cancion.mp3", false, false)]
    [InlineData("informe.pdf", false, false)]
    [InlineData("sin-extension", false, false)]
    public void Solo_fotos_y_videos_decodificables_llevan_miniatura(string name, bool supported, bool video)
    {
        Assert.Equal(supported, Thumbnails.Supports(File(name)));
        Assert.Equal(video, Thumbnails.IsVideo(File(name)));
    }

    [Fact]
    public void Una_carpeta_con_nombre_de_foto_no_lleva_miniatura()
    {
        Assert.False(Thumbnails.Supports(File("Vacaciones.jpg", dir: true)));
        Assert.False(Thumbnails.IsVideo(File("Viaje.mp4", dir: true)));
    }

    [Fact]
    public void La_clave_es_estable_y_sirve_de_nombre_de_fichero()
    {
        var when = new DateTime(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc);
        var key = Thumbnails.CacheKey("/sdcard/DCIM/a.jpg", 1234, when);

        Assert.Equal(key, Thumbnails.CacheKey("/sdcard/DCIM/a.jpg", 1234, when));
        Assert.Equal(40, key.Length);
        Assert.Matches("^[0-9a-f]+$", key);
    }

    [Fact]
    public void La_clave_cambia_con_la_ruta_el_tamano_la_fecha_o_el_lado()
    {
        var when = new DateTime(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc);
        var key = Thumbnails.CacheKey("/sdcard/a.jpg", 1234, when);

        Assert.NotEqual(key, Thumbnails.CacheKey("/sdcard/b.jpg", 1234, when));
        Assert.NotEqual(key, Thumbnails.CacheKey("/sdcard/a.jpg", 1235, when));
        Assert.NotEqual(key, Thumbnails.CacheKey("/sdcard/a.jpg", 1234, when.AddSeconds(1)));
        Assert.NotEqual(key, Thumbnails.CacheKey("/sdcard/a.jpg", 1234, when, 256));
        // La misma hora en otra zona es el mismo instante: misma clave.
        Assert.Equal(key, Thumbnails.CacheKey("/sdcard/a.jpg", 1234, new DateTimeOffset(when).ToOffset(TimeSpan.FromHours(2)).UtcDateTime));
    }

    [Fact]
    public void La_clave_de_un_elemento_usa_su_ruta_tamano_y_fecha()
    {
        var item = File("a.jpg");
        Assert.Equal(Thumbnails.CacheKey(item.FullPath, item.Size, item.Modified), Thumbnails.CacheKey(item));
    }

    [Theory]
    [InlineData(4000, 3000, 128, 16)] // 12 Mpx: se lee a 250x187
    [InlineData(3000, 4000, 128, 16)]
    [InlineData(256, 256, 128, 2)]
    [InlineData(255, 1000, 128, 1)]
    [InlineData(100, 100, 128, 1)] // mas pequena que la miniatura: entera
    [InlineData(0, 0, 128, 1)]
    public void Se_decodifica_reducida_sin_quedarse_por_debajo_del_lado_pedido(int w, int h, int size, int expected)
    {
        var sample = Thumbnails.SampleSize(w, h, size);
        Assert.Equal(expected, sample);
        if (Math.Min(w, h) >= size)
            Assert.True(Math.Min(w, h) / sample >= size);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(6, 90)]
    [InlineData(3, 180)]
    [InlineData(8, 270)]
    [InlineData(0, 0)]
    [InlineData(2, 0)] // espejos: no se giran
    public void La_orientacion_EXIF_dice_cuanto_girar(int orientation, int degrees) =>
        Assert.Equal(degrees, Thumbnails.ExifRotation(orientation));

    [Fact]
    public void Sin_plataforma_no_hay_miniaturas()
    {
        Assert.False(new NoThumbnailGenerator().TryCreate("/a.jpg", false, "/t", 128, CancellationToken.None));
    }

    [Fact]
    public void El_elemento_ensena_el_icono_hasta_que_llega_su_miniatura()
    {
        var photo = File("foto.jpg");
        var video = File("clip.mp4");
        var changed = new List<string?>();
        photo.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.True(photo.ShowIcon);
        Assert.False(photo.HasThumbnail);
        Assert.Null(photo.Thumbnail);
        Assert.False(video.ShowVideoBadge);

        photo.ThumbnailPath = "/cache/a.thumb";
        video.ThumbnailPath = "/cache/b.thumb";

        Assert.False(photo.ShowIcon);
        Assert.True(photo.HasThumbnail);
        Assert.Equal("/cache/a.thumb", Assert.IsType<FileImageSource>(photo.Thumbnail).File);
        Assert.False(photo.ShowVideoBadge);
        Assert.True(video.ShowVideoBadge);
        Assert.Equal(new[] { "ThumbnailPath", "Thumbnail", "HasThumbnail", "ShowIcon", "ShowVideoBadge" }, changed);

        // El mismo valor no avisa; quitarla vuelve al icono.
        photo.ThumbnailPath = "/cache/a.thumb";
        Assert.Equal(5, changed.Count);
        photo.ThumbnailPath = null;
        Assert.True(photo.ShowIcon);
        Assert.Null(photo.Thumbnail);
    }
}

/// <summary>El servicio de miniaturas con un generador doble y una cache en carpeta temporal.</summary>
public sealed class ThumbnailServiceTests : IDisposable
{
    private readonly TempFolder _files = new();
    private readonly TempFolder _cache = new();
    private readonly MemorySettings _settings = new();
    private readonly FakeThumbnailGenerator _generator = new();
    private readonly ThumbnailService _service;

    public ThumbnailServiceTests() => _service = NewService();

    private ThumbnailService NewService() =>
        new(_settings, _generator, _cache.Path, NullLogger<ThumbnailService>.Instance);

    public void Dispose()
    {
        _generator.Gate?.Set();
        _files.Dispose();
        _cache.Dispose();
    }

    private FileItem Media(string name)
    {
        var path = _files.File(name);
        var info = new FileInfo(path);
        return new FileItem { Name = name, FullPath = path, Size = info.Length, Modified = info.LastWriteTime };
    }

    private static async Task Until(Func<bool> condition, string what)
    {
        for (var i = 0; i < 500; i++)
        {
            if (condition())
                return;
            await Task.Delay(10);
        }
        throw new TimeoutException("No llego: " + what);
    }

    private string[] CacheFiles(string pattern = "*.thumb") =>
        Directory.Exists(_cache.Path) ? Directory.GetFiles(_cache.Path, pattern) : Array.Empty<string>();

    [Fact]
    public async Task Hace_la_miniatura_en_segundo_plano_y_la_deja_en_la_cache_de_disco()
    {
        var photo = Media("foto.jpg");
        var video = Media("clip.mp4");

        _service.Request(photo);
        _service.Request(video);
        await Until(() => photo.HasThumbnail && video.HasThumbnail, "las dos miniaturas");

        Assert.Contains("foto.jpg:image:128", _generator.Made);
        Assert.Contains("clip.mp4:video:128", _generator.Made);
        Assert.Equal(Path.Combine(_cache.Path, Thumbnails.CacheKey(photo) + ".thumb"), photo.ThumbnailPath);
        Assert.True(System.IO.File.Exists(photo.ThumbnailPath));
        Assert.Empty(CacheFiles("*.tmp"));
    }

    [Fact]
    public async Task El_resultado_se_lleva_por_Post()
    {
        var posted = 0;
        _service.Post = action => { posted++; action(); };
        var photo = Media("foto.jpg");

        _service.Request(photo);
        await Until(() => photo.HasThumbnail, "la miniatura");

        Assert.Equal(1, posted);
    }

    [Fact]
    public async Task Apagadas_en_Ajustes_no_se_hace_ninguna()
    {
        _settings.ShowThumbnails = false;
        var photo = Media("foto.jpg");

        _service.Request(photo);
        await Task.Delay(50);

        Assert.Equal(0, _generator.Started);
        Assert.True(photo.ShowIcon);
    }

    [Fact]
    public async Task Carpetas_y_otros_tipos_siguen_con_su_icono()
    {
        var doc = Media("informe.pdf");
        var folder = new FileItem { Name = "Fotos.jpg", FullPath = _files.Dir("Fotos.jpg"), IsDirectory = true };

        _service.Request(doc);
        _service.Request(folder);
        await Task.Delay(50);

        Assert.Equal(0, _generator.Started);
        Assert.Null(doc.ThumbnailPath);
        Assert.Null(folder.ThumbnailPath);
    }

    [Fact]
    public void Si_ya_la_tiene_no_la_vuelve_a_pedir()
    {
        var photo = Media("foto.jpg");
        photo.ThumbnailPath = "/ya/esta.thumb";

        _service.Request(photo);

        Assert.Equal(0, _service.PendingCount);
        Assert.Equal("/ya/esta.thumb", photo.ThumbnailPath);
    }

    [Fact]
    public async Task La_cache_en_memoria_la_pone_en_el_acto_al_volver_a_la_carpeta()
    {
        var first = Media("foto.jpg");
        _service.Request(first);
        await Until(() => first.HasThumbnail, "la primera");

        // Al volver a la carpeta, el listado trae elementos nuevos del mismo fichero.
        var again = new FileItem { Name = first.Name, FullPath = first.FullPath, Size = first.Size, Modified = first.Modified };
        _service.Request(again);

        Assert.Equal(first.ThumbnailPath, again.ThumbnailPath);
        Assert.Single(_generator.Made);
    }

    [Fact]
    public async Task La_cache_en_disco_sobrevive_a_otra_sesion()
    {
        var photo = Media("foto.jpg");
        _service.Request(photo);
        await Until(() => photo.HasThumbnail, "la miniatura");

        var nextSession = NewService();
        var again = new FileItem { Name = photo.Name, FullPath = photo.FullPath, Size = photo.Size, Modified = photo.Modified };
        nextSession.Request(again);
        await Until(() => again.HasThumbnail, "la de la cache");

        Assert.Single(_generator.Made);
        Assert.Equal(photo.ThumbnailPath, again.ThumbnailPath);
    }

    [Fact]
    public async Task Un_fichero_cambiado_tiene_miniatura_nueva()
    {
        var photo = Media("foto.jpg");
        _service.Request(photo);
        await Until(() => photo.HasThumbnail, "la primera");

        var edited = new FileItem { Name = photo.Name, FullPath = photo.FullPath, Size = photo.Size + 5, Modified = photo.Modified.AddMinutes(1) };
        _service.Request(edited);
        await Until(() => edited.HasThumbnail, "la nueva");

        Assert.Equal(2, _generator.Made.Count);
        Assert.NotEqual(photo.ThumbnailPath, edited.ThumbnailPath);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("nothing")]
    [InlineData("throws")]
    public async Task Si_no_se_puede_hacer_se_queda_el_icono_y_no_se_reintenta(string how)
    {
        _generator.Result = how != "false";
        _generator.WriteNothing = how == "nothing";
        _generator.Error = how == "throws" ? new IOException("roto") : null;
        var photo = Media("rota.jpg");

        _service.Request(photo);
        await Until(() => _generator.Made.Count == 1, "el intento");
        await Until(() => _service.PendingCount == 0, "que acabe");
        await Task.Delay(30);

        Assert.True(photo.ShowIcon);
        Assert.Empty(CacheFiles("*"));

        // Otra vez que se vea: no se vuelve a intentar.
        var again = new FileItem { Name = photo.Name, FullPath = photo.FullPath, Size = photo.Size, Modified = photo.Modified };
        _service.Request(again);
        await Task.Delay(50);
        Assert.Single(_generator.Made);
        Assert.True(again.ShowIcon);
    }

    [Fact]
    public async Task Lo_que_deja_a_medias_un_generador_que_falla_no_llega_a_la_cache()
    {
        var photo = Media("foto.jpg");
        var gen = new HalfWriter();
        var service = new ThumbnailService(_settings, gen, _cache.Path, NullLogger<ThumbnailService>.Instance);

        service.Request(photo);
        await Until(() => gen.Calls == 1, "el intento");
        await Task.Delay(50);

        Assert.True(photo.ShowIcon);
        Assert.Empty(CacheFiles("*"));
    }

    private sealed class HalfWriter : IThumbnailGenerator
    {
        public int Calls;

        public bool TryCreate(string sourcePath, bool isVideo, string targetPath, int sizePx, CancellationToken cancellationToken)
        {
            System.IO.File.WriteAllBytes(targetPath, new byte[] { 1 });
            Interlocked.Increment(ref Calls);
            return false;
        }
    }

    [Fact]
    public async Task Dos_elementos_del_mismo_fichero_a_la_vez_salen_los_dos()
    {
        _generator.Gate = new ManualResetEventSlim(false);
        var photo = Media("foto.jpg");
        var twin = new FileItem { Name = photo.Name, FullPath = photo.FullPath, Size = photo.Size, Modified = photo.Modified };

        _service.Request(photo); // en la lista
        _service.Request(twin);  // y en la franja de recientes
        await Until(() => _generator.Started == 2, "los dos a la vez");
        _generator.Gate.Set();

        await Until(() => photo.HasThumbnail && twin.HasThumbnail, "las dos");
        Assert.Equal(photo.ThumbnailPath, twin.ThumbnailPath);
    }

    [Fact]
    public async Task Si_no_se_puede_limpiar_la_cache_se_sigue()
    {
        _service.MaxDiskFiles = 0;
        var locked = Path.Combine(_cache.Path, "en-uso.thumb.tmp");
        // Abierto sin compartir: en Windows no se puede borrar.
        using (System.IO.File.Create(locked))
        {
            var photo = Media("foto.jpg");
            _service.Request(photo);
            await Until(() => photo.HasThumbnail, "la miniatura");
        }
    }

    [Fact]
    public async Task Sin_sitio_para_la_cache_se_queda_el_icono()
    {
        // La carpeta de la cache es un fichero: no se puede crear.
        var blocked = _files.File("no-es-carpeta");
        var service = new ThumbnailService(_settings, _generator, blocked, NullLogger<ThumbnailService>.Instance);
        var photo = Media("foto.jpg");

        service.Request(photo);
        await Task.Delay(100);

        Assert.True(photo.ShowIcon);
        Assert.Equal(0, _generator.Started);
    }

    [Fact]
    public async Task Soltar_la_que_se_esta_haciendo_la_cancela_y_se_puede_volver_a_pedir()
    {
        _generator.Gate = new ManualResetEventSlim(false);
        var photo = Media("foto.jpg");

        _service.Request(photo);
        await Until(() => _generator.Started == 1, "que empiece");
        _service.Release(photo);
        await Task.Delay(50);

        Assert.True(photo.ShowIcon);
        Assert.Empty(_generator.Made);

        // Cancelada no es "imposible": al volver a verse se pide de nuevo.
        _generator.Gate.Set();
        _service.Request(photo);
        await Until(() => photo.HasThumbnail, "la segunda vez");
        Assert.Single(_generator.Made);
    }

    [Fact]
    public async Task La_cola_tiene_tope_sirve_primero_la_mas_nueva_y_suelta_las_que_ya_no_se_ven()
    {
        _generator.Gate = new ManualResetEventSlim(false);
        _service.MaxPending = 3;
        _service.Workers = 1; // de uno en uno, para ver el orden
        var busy = new[] { Media("ocupa.jpg") };
        foreach (var b in busy)
            _service.Request(b);
        await Until(() => _generator.Started == _service.Workers, "los dos trabajadores ocupados");

        var waiting = Enumerable.Range(1, 5).Select(i => Media($"f{i}.jpg")).ToArray();
        foreach (var w in waiting)
            _service.Request(w);

        // Las dos mas viejas se descartan; de las que quedan, se suelta una (la fila se recicla) y otra
        // se vuelve a ver (pasa delante).
        Assert.Equal(3, _service.PendingCount);
        _service.Release(waiting[3]);
        Assert.Equal(2, _service.PendingCount);
        _service.Request(waiting[2]);
        _service.Request(waiting[2]);
        Assert.Equal(2, _service.PendingCount);

        _generator.Gate.Set();
        await Until(() => waiting[2].HasThumbnail && waiting[4].HasThumbnail, "las que quedan");
        await Until(() => _service.PendingCount == 0 && _generator.Made.Count == 3, "que acabe");

        var made = _generator.Made.Skip(1).ToList();
        Assert.Equal(new[] { "f3.jpg:image:128", "f5.jpg:image:128" }, made);
        Assert.All(waiting.Take(2).Append(waiting[3]), w => Assert.True(w.ShowIcon));
    }

    [Fact]
    public async Task Bind_suelta_lo_que_ensenaba_la_vista_y_pide_lo_nuevo()
    {
        _generator.Gate = new ManualResetEventSlim(false);
        var busy = new[] { Media("ocupa1.jpg"), Media("ocupa2.jpg") };
        foreach (var b in busy)
            _service.Request(b);
        await Until(() => _generator.Started == _service.Workers, "los dos trabajadores ocupados");

        var row = new object();
        var a = Media("a.jpg");
        var b2 = Media("b.jpg");

        _service.Bind(row, a);
        _service.Bind(row, a); // la misma: nada
        Assert.Equal(1, _service.PendingCount);

        _service.Bind(row, b2); // la fila se recicla: suelta a y pide b
        Assert.Equal(1, _service.PendingCount);

        _service.Bind(row, null); // la fila se queda sin elemento
        Assert.Equal(0, _service.PendingCount);

        _service.Bind(row, a);
        _generator.Gate.Set();
        await Until(() => a.HasThumbnail, "la de a");
        await Until(() => _generator.Made.Count == 3, "que acabe");
        Assert.True(b2.ShowIcon);
    }

    [Fact]
    public async Task La_cache_en_memoria_tiene_tope()
    {
        _service.MemoryEntries = 1;
        var first = Media("uno.jpg");
        var second = Media("dos.jpg");
        _service.Request(first);
        await Until(() => first.HasThumbnail, "la primera");
        _service.Request(second);
        await Until(() => second.HasThumbnail, "la segunda");

        // La primera ya no esta en memoria: se busca en disco (sin generarla otra vez) y llega despues.
        var again = new FileItem { Name = first.Name, FullPath = first.FullPath, Size = first.Size, Modified = first.Modified };
        _service.Request(again);
        await Until(() => again.HasThumbnail, "la de disco");
        Assert.Equal(2, _generator.Made.Count);
    }

    [Fact]
    public async Task Al_empezar_limpia_restos_y_recorta_la_cache_de_disco_si_se_ha_pasado()
    {
        _service.MaxDiskFiles = 4;
        var start = DateTime.UtcNow.AddDays(-10);
        for (var i = 0; i < 6; i++)
        {
            var path = Path.Combine(_cache.Path, $"viejo{i}.thumb");
            System.IO.File.WriteAllBytes(path, new byte[] { 1 });
            System.IO.File.SetLastWriteTimeUtc(path, start.AddHours(i));
        }
        System.IO.File.WriteAllBytes(Path.Combine(_cache.Path, "a-medias.thumb.tmp"), new byte[] { 1 });

        var photo = Media("foto.jpg");
        _service.Request(photo);
        await Until(() => photo.HasThumbnail, "la miniatura");

        var left = CacheFiles().Select(Path.GetFileName).OrderBy(n => n).ToList();
        // Se queda en tres cuartos del tope (las 3 mas nuevas) mas la recien hecha.
        Assert.Equal(new[] { "viejo3.thumb", "viejo4.thumb", "viejo5.thumb", Path.GetFileName(photo.ThumbnailPath)! }.OrderBy(n => n), left);
        Assert.Empty(CacheFiles("*.tmp"));
    }
}
