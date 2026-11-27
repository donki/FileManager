using System.Runtime.CompilerServices;
using FileManager.Helpers;
using FileManager.Models;
using Microsoft.Extensions.Logging;

namespace FileManager.Services;

/// <summary>
/// Miniaturas de fotos y videos para la lista y la franja de recientes.
/// <list type="bullet">
/// <item>Se piden cuando una fila se enlaza a un elemento (<see cref="Bind"/>) y se sueltan cuando la
/// fila se recicla para otro: al desplazar deprisa solo se hacen las que se ven.</item>
/// <item>Se hacen fuera del hilo de la interfaz, de dos en dos, la mas nueva primero; las que esperan
/// tienen un tope y las mas viejas se descartan.</item>
/// <item>Cache en disco (un fichero pequeno por miniatura, con un tope de ficheros) y en memoria solo
/// la ruta de cada una, con un tope de entradas; los bytes los lee y cachea el control de imagen.</item>
/// <item>Mientras no esta, o si no se puede hacer, la fila ensena el icono del tipo.</item>
/// </list>
/// </summary>
public sealed class ThumbnailService
{
    private readonly ISettingsService _settings;
    private readonly IThumbnailGenerator _generator;
    private readonly string _cacheDir;
    private readonly ILogger _logger;
    private readonly object _gate = new();
    private readonly object _trimGate = new();

    // Peticiones vivas (en cola o haciendose) y la cola en orden de llegada: la ultima es la mas nueva.
    private readonly Dictionary<FileItem, Pending> _requests = new();
    private readonly LinkedList<Pending> _queue = new();

    // Cache en memoria: clave -> ruta de la miniatura, o null si ese fichero no tiene (no se reintenta).
    private readonly Dictionary<string, LinkedListNode<KeyValuePair<string, string?>>> _memory = new();
    private readonly LinkedList<KeyValuePair<string, string?>> _memoryOrder = new();

    // Que elemento ensena ahora cada vista (fila o tarjeta de la franja).
    private readonly ConditionalWeakTable<object, FileItem> _bound = new();

    private int _running;
    private bool _diskTrimmed;

    public ThumbnailService(ISettingsService settings, IThumbnailGenerator generator, string cacheDirectory, ILogger<ThumbnailService> logger)
    {
        _settings = settings;
        _generator = generator;
        _cacheDir = cacheDirectory;
        _logger = logger;
    }

    /// <summary>Cuantas se hacen a la vez (de dos en dos: mas no va mas rapido y frena el desplazamiento).</summary>
    public int Workers { get; set; } = 2;

    /// <summary>Cuantas peticiones pueden esperar; al pasarse se tira la mas vieja.</summary>
    public int MaxPending { get; set; } = 64;

    /// <summary>Cuantas claves recuerda la cache en memoria.</summary>
    public int MemoryEntries { get; set; } = 512;

    /// <summary>Cuantas miniaturas guarda la cache en disco; al pasarse se borran las mas viejas.</summary>
    public int MaxDiskFiles { get; set; } = 2000;

    /// <summary>
    /// Como se lleva el resultado al hilo de la interfaz. La pagina pone el suyo (su Dispatcher); por
    /// defecto se aplica en el acto.
    /// </summary>
    public Action<Action> Post { get; set; } = action => action();

    /// <summary>Peticiones que esperan su turno (para las pruebas y el registro).</summary>
    public int PendingCount
    {
        get { lock (_gate) return _queue.Count; }
    }

    /// <summary>
    /// La vista <paramref name="view"/> pasa a ensenar <paramref name="item"/> (o nada): suelta la
    /// peticion del que ensenaba antes y pide la del nuevo.
    /// </summary>
    public void Bind(object view, FileItem? item)
    {
        if (_bound.TryGetValue(view, out var previous))
        {
            if (ReferenceEquals(previous, item))
                return;
            _bound.Remove(view);
            Release(previous);
        }

        if (item is not null)
        {
            _bound.AddOrUpdate(view, item);
            Request(item);
        }
    }

    /// <summary>Pide la miniatura de un elemento. No hace nada si no lleva, si ya la tiene o si estan apagadas.</summary>
    public void Request(FileItem item)
    {
        if (!_settings.ShowThumbnails || item.ThumbnailPath is not null || !Thumbnails.Supports(item))
            return;

        var key = Thumbnails.CacheKey(item);
        var start = false;
        string? known;
        lock (_gate)
        {
            if (_memory.TryGetValue(key, out var node))
            {
                _memoryOrder.Remove(node);
                _memoryOrder.AddFirst(node);
                known = node.Value.Value;
            }
            else
            {
                known = null;
                if (_requests.TryGetValue(item, out var existing))
                {
                    // Ya pedida: si espera, pasa delante, que se acaba de volver a ver.
                    if (existing.Node is { } waiting)
                    {
                        _queue.Remove(waiting);
                        _queue.AddLast(waiting);
                    }
                    return;
                }

                var pending = new Pending(item, key);
                pending.Node = _queue.AddLast(pending);
                _requests[item] = pending;
                while (_queue.Count > MaxPending)
                    Drop(_queue.First!.Value);

                if (_running < Workers)
                {
                    _running++;
                    start = true;
                }
            }
        }

        if (known is not null)
            item.ThumbnailPath = known;
        if (start)
            _ = Task.Run(Work);
    }

    /// <summary>El elemento ya no se ve: si su miniatura espera, se quita de la cola; si se esta haciendo, se cancela.</summary>
    public void Release(FileItem item)
    {
        lock (_gate)
        {
            if (_requests.TryGetValue(item, out var pending))
                Drop(pending);
        }
    }

    // Con el cerrojo tomado.
    private void Drop(Pending pending)
    {
        _requests.Remove(pending.Item);
        if (pending.Node is { } node)
        {
            _queue.Remove(node);
            pending.Node = null;
        }
        pending.Cancel.Cancel();
    }

    private void Work()
    {
        while (true)
        {
            Pending next;
            lock (_gate)
            {
                if (_queue.Last is not { } node)
                {
                    _running--;
                    return;
                }
                next = node.Value;
                _queue.RemoveLast();
                next.Node = null;
            }

            string? path = null;
            var failed = false;
            try
            {
                path = Produce(next);
                failed = path is null && !next.Cancel.IsCancellationRequested;
            }
            catch (OperationCanceledException)
            {
                // Soltada mientras se hacia: otra vez que se vea se vuelve a pedir.
            }
            catch (Exception ex)
            {
                failed = true;
                _logger.LogWarning(ex, "Could not make the thumbnail of {Path}", next.Item.FullPath);
            }

            lock (_gate)
            {
                if (_requests.TryGetValue(next.Item, out var current) && ReferenceEquals(current, next))
                    _requests.Remove(next.Item);
                if (path is not null || failed)
                    Remember(next.Key, path);
            }

            // Hecha aunque se haya soltado: no cuesta nada y la fila puede volver a verse.
            if (path is not null)
                Post(() => next.Item.ThumbnailPath = path);
        }
    }

    private string? Produce(Pending pending)
    {
        var token = pending.Cancel.Token;
        token.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_cacheDir);
        TrimDiskOnce();

        var target = Path.Combine(_cacheDir, pending.Key + ".thumb");
        if (File.Exists(target) && new FileInfo(target).Length > 0)
            return target;

        // Se escribe aparte y se renombra: una miniatura a medias no llega nunca a la cache. El nombre
        // aparte es de esta peticion: el mismo fichero puede estar en la lista y en los recientes a la vez.
        var temp = $"{target}.{Guid.NewGuid():N}.tmp";
        try
        {
            if (_generator.TryCreate(pending.Item.FullPath, Thumbnails.IsVideo(pending.Item), temp, Thumbnails.SizePx, token)
                && File.Exists(temp) && new FileInfo(temp).Length > 0)
            {
                try
                {
                    File.Move(temp, target, overwrite: true);
                }
                catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && File.Exists(target))
                {
                    // La otra peticion del mismo fichero la dejo a la vez: vale la suya.
                }
                return target;
            }
            token.ThrowIfCancellationRequested();
            // La otra peticion del mismo fichero pudo acabar antes.
            return File.Exists(target) ? target : null;
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    // Con el cerrojo tomado.
    private void Remember(string key, string? path)
    {
        if (_memory.TryGetValue(key, out var old))
            _memoryOrder.Remove(old);
        _memory[key] = _memoryOrder.AddFirst(new KeyValuePair<string, string?>(key, path));
        while (_memoryOrder.Count > MemoryEntries)
        {
            var last = _memoryOrder.Last!;
            _memoryOrder.RemoveLast();
            _memory.Remove(last.Value.Key);
        }
    }

    /// <summary>
    /// Una vez por sesion, antes de la primera miniatura: se borran los restos a medias y, si la cache
    /// se ha pasado del tope, las mas viejas hasta dejarla en tres cuartos.
    /// </summary>
    private void TrimDiskOnce()
    {
        // Los demas trabajadores esperan a que acabe: no se puede borrar un resto mientras otro escribe.
        lock (_trimGate)
        {
            if (_diskTrimmed)
                return;
            _diskTrimmed = true;
            TrimDisk();
        }
    }

    private void TrimDisk()
    {
        try
        {
            var dir = new DirectoryInfo(_cacheDir);
            foreach (var temp in dir.EnumerateFiles("*.tmp"))
                temp.Delete();

            var files = dir.GetFiles("*.thumb");
            if (files.Length <= MaxDiskFiles)
                return;

            foreach (var old in files.OrderBy(f => f.LastWriteTimeUtc).Take(files.Length - MaxDiskFiles * 3 / 4))
                old.Delete();
        }
        catch (Exception ex)
        {
            // La cache es un extra: si no se puede limpiar, se sigue.
            _logger.LogWarning(ex, "Could not trim the thumbnail cache");
        }
    }

    private sealed class Pending(FileItem item, string key)
    {
        public FileItem Item { get; } = item;
        public string Key { get; } = key;
        public CancellationTokenSource Cancel { get; } = new();
        public LinkedListNode<Pending>? Node { get; set; }
    }
}
