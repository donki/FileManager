using System.Collections.ObjectModel;
using FileManager.Helpers;
using FileManager.Models;
using FileManager.Services;
using Microsoft.Extensions.Logging;

namespace FileManager.ViewModels;

/// <summary>Lo que la pantalla principal hace con sus propios controles y la logica no puede hacer.</summary>
public interface IMainView
{
    void FocusSearch();

    void UnfocusSearch();

    Task OpenSettingsAsync();

    Task OpenAboutAsync();
}

/// <summary>Un tramo de la ruta de migas: su texto, la carpeta a la que lleva y si es la actual.</summary>
public sealed record Crumb(string Label, string Path, bool IsLast);

/// <summary>
/// Logica de la pantalla principal (explorador): carpeta actual, listado, filtro, busqueda, seleccion
/// multiple, portapapeles y menus. La pagina solo vuelca este estado en sus controles
/// (<see cref="Changed"/>) y le pasa los toques; asi todo esto se prueba sin interfaz (General 8.6).
/// </summary>
public class MainViewModel
{
    /// <summary>Control de la pagina (x:Name) y clave del texto fijo que lleva.</summary>
    public static readonly IReadOnlyDictionary<string, string> StaticTexts = new Dictionary<string, string>
    {
        ["FileSearchBar"] = "SearchPlaceholder",
        ["EmptyHintLabel"] = "EmptyFolderHint",
        ["PermissionTitleLabel"] = "PermissionTitle",
        ["PermissionBodyLabel"] = "PermissionBody",
        ["PermissionButton"] = "PermissionGrant",
        ["PasteConfirmButton"] = "Paste",
        ["PasteCancelButton"] = "Cancel",
        ["SelectAllButton"] = "SelectAll",
    };

    private readonly IFileSystemService _files;
    private readonly IFileClipboardService _clipboard;
    private readonly IFileActionsService _actions;
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _l;
    private readonly IStoragePermissionService _permissions;
    private readonly IToastService _toast;
    private readonly IDialogService _dialogs;
    private readonly IMainView _view;
    private readonly ILogger _logger;

    /// <summary>Lista completa cargada de la carpeta (sin filtrar). <see cref="Items"/> es su vista filtrada.</summary>
    private readonly List<FileItem> _allItems = new();

    private CancellationTokenSource? _searchCts;

    public MainViewModel(
        IFileSystemService files,
        IFileClipboardService clipboard,
        IFileActionsService actions,
        ISettingsService settings,
        ILocalizationService localization,
        IStoragePermissionService permissions,
        IToastService toast,
        IDialogService dialogs,
        IMainView view,
        ILogger logger)
    {
        _files = files;
        _clipboard = clipboard;
        _actions = actions;
        _settings = settings;
        _l = localization;
        _permissions = permissions;
        _toast = toast;
        _dialogs = dialogs;
        _view = view;
        _logger = logger;

        CurrentPath = _files.RootPath;
        EmptyTitle = _l["EmptyFolder"];
    }

    /// <summary>Algo del estado de la pantalla ha cambiado: la pagina lo vuelve a volcar.</summary>
    public event EventHandler? Changed;

    /// <summary>Elementos visibles (con el filtro por tipo aplicado), enlazados a la lista.</summary>
    public ObservableCollection<FileItem> Items { get; } = new();

    // ---------- Estado ----------

    public string CurrentPath { get; private set; }

    /// <summary>Filtro por tipo activo (null = todos).</summary>
    public FileCategory? TypeFilter { get; private set; }

    /// <summary>Modo de seleccion multiple activo (operaciones en lote).</summary>
    public bool SelectMode { get; private set; }

    public bool SearchActive { get; private set; }

    /// <summary>Se pidio el permiso y estamos esperando a que el usuario vuelva de los ajustes.</summary>
    public bool AwaitingPermission { get; private set; }

    // ---------- Lo que se ve ----------

    public string Title { get; private set; } = string.Empty;

    public string Subtitle { get; private set; } = string.Empty;

    public bool UpVisible { get; private set; }

    public bool PermissionVisible { get; private set; }

    public bool FilesVisible { get; private set; } = true;

    public bool EmptyVisible { get; private set; }

    public string EmptyIcon { get; private set; } = "ic_folder_open.png";

    public string EmptyTitle { get; private set; }

    public bool EmptyHintVisible { get; private set; } = true;

    public bool NewFolderVisible { get; private set; } = true;

    public bool SearchButtonVisible { get; private set; } = true;

    public bool SearchBarVisible { get; private set; }

    /// <summary>Texto que debe tener el buscador; al salir de la busqueda se vacia.</summary>
    public string SearchText { get; private set; } = string.Empty;

    public bool SelectionBarVisible { get; private set; }

    public string SelectionCountText { get; private set; } = string.Empty;

    public bool PasteBarVisible { get; private set; }

    public string PasteText { get; private set; } = string.Empty;

    public bool IsBusy { get; private set; }

    /// <summary>Ruta de migas de la carpeta actual; cambia de objeto cuando hay que redibujarla.</summary>
    public IReadOnlyList<Crumb> Breadcrumb { get; private set; } = Array.Empty<Crumb>();

    private void Notify() => Changed?.Invoke(this, EventArgs.Empty);

    // ============ Ciclo de vida y permisos ============

    /// <summary>Al aparecer la pagina: textos, barra de pegar y estado del permiso.</summary>
    public async Task AppearingAsync()
    {
        UpdateHeader();
        UpdatePasteBar();
        await RefreshAccessAsync();
    }

    /// <summary>
    /// Consulta el estado del permiso y muestra la lista o la pantalla de permisos.
    /// Android no informa del resultado de la pantalla de ajustes, asi que hay que
    /// preguntar de nuevo cada vez que la aplicacion recupera el foco.
    /// </summary>
    public async Task RefreshAccessAsync()
    {
        if (!_permissions.HasFullAccess)
        {
            ShowPermissionGate();
            return;
        }

        if (AwaitingPermission)
        {
            AwaitingPermission = false;
            _toast.Show(_l["PermissionGranted"]);
        }

        PermissionVisible = false;
        Notify();

        // Recargar durante una busqueda borraria los resultados que el usuario esta viendo.
        if (!SearchActive)
            await LoadAsync(CurrentPath);
    }

    /// <summary>El idioma ha cambiado: textos y, si hay acceso, el listado (fechas y recuentos).</summary>
    public async Task LanguageChangedAsync()
    {
        EmptyTitle = _l["EmptyFolder"];
        UpdateHeader();
        if (_permissions.HasFullAccess)
            await LoadAsync(CurrentPath);
    }

    private void ShowPermissionGate()
    {
        PermissionVisible = true;
        FilesVisible = false;
        EmptyVisible = false;
        NewFolderVisible = false;
        PasteBarVisible = false;
        Notify();
    }

    public async Task RequestPermissionAsync()
    {
        try
        {
            AwaitingPermission = true;
            await _permissions.RequestFullAccessAsync();
        }
        catch (Exception ex)
        {
            AwaitingPermission = false;
            _logger.LogError(ex, "Could not open the storage permission settings");
            await _dialogs.AlertAsync(_l["Error"], ex.Message, _l["Ok"]);
        }
    }

    // ============ Listado ============

    public async Task LoadAsync(string path)
    {
        if (!Directory.Exists(path))
        {
            // La carpeta pudo borrarse desde otra app mientras estabamos dentro.
            _logger.LogInformation("Current folder no longer exists, falling back to the root");
            path = _files.RootPath;
        }

        CurrentPath = path;
        SetBusy(true);

        try
        {
            var items = await _files.ListAsync(path, _settings.ShowHiddenFiles, _settings.Sort);
            Populate(items, isSearch: false);
        }
        catch (UnauthorizedAccessException)
        {
            await _dialogs.AlertAsync(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorAccessDenied"], path), _l["Ok"]);
            await NavigateUpAsync();
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Could not list the folder");
            await _dialogs.AlertAsync(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorReadFolder"], ex.Message), _l["Ok"]);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Populate(IReadOnlyList<FileItem> items, bool isSearch)
    {
        // Cambiar de carpeta/lista sale del modo seleccion.
        if (SelectMode)
            ExitSelectionMode();

        _allItems.Clear();
        foreach (var item in items)
        {
            item.Icon = FileIcons.For(item);
            item.Details = DescribeItem(item);
            item.IsSelected = false;
            _allItems.Add(item);
        }

        FilesVisible = true;
        PermissionVisible = false;
        NewFolderVisible = !isSearch;

        ApplyFilter();

        EmptyIcon = isSearch ? "ic_search_empty.png" : "ic_folder_open.png";
        EmptyTitle = isSearch ? _l["NoSearchResults"] : _l["EmptyFolder"];
        EmptyHintVisible = !isSearch;

        BuildBreadcrumb();
        UpdateHeader();
        UpdatePasteBar();
    }

    /// <summary>Rellena <see cref="Items"/> desde la lista completa aplicando el filtro por tipo.
    /// Las CARPETAS se muestran siempre (para poder seguir navegando aunque haya filtro).</summary>
    private void ApplyFilter()
    {
        Items.Clear();
        foreach (var item in _allItems)
        {
            if (TypeFilter is null || item.IsDirectory || item.Category == TypeFilter)
                Items.Add(item);
        }
        EmptyVisible = Items.Count == 0;
    }

    /// <summary>Nombre localizado de una categoria de tipo (para el menu y el subtitulo).</summary>
    public string FilterName(FileCategory? category) => category switch
    {
        null => _l["FilterAll"],
        FileCategory.Image => _l["FilterImages"],
        FileCategory.Video => _l["FilterVideo"],
        FileCategory.Audio => _l["FilterAudio"],
        FileCategory.Document => _l["FilterDocuments"],
        FileCategory.Apk => _l["FilterApk"],
        FileCategory.Archive => _l["FilterArchives"],
        _ => _l["FilterOther"]
    };

    public async Task ShowFilterMenuAsync()
    {
        FileCategory?[] categories =
        {
            null, FileCategory.Image, FileCategory.Video, FileCategory.Audio,
            FileCategory.Document, FileCategory.Apk, FileCategory.Archive, FileCategory.Other
        };
        var options = categories.Select(c => (Label: FilterName(c), Cat: c)).ToArray();

        var choice = await _dialogs.ActionSheetAsync(_l["Filter"], _l["Cancel"], options.Select(o => o.Label).ToArray());
        if (choice is null || choice == _l["Cancel"])
            return;

        TypeFilter = options.FirstOrDefault(o => o.Label == choice).Cat;
        ApplyFilter();
        UpdateHeader();
    }

    /// <summary>Linea secundaria de cada fila: fecha y, para ficheros, tamano; para carpetas, su contenido.</summary>
    public string DescribeItem(FileItem item)
    {
        var culture = _l.CurrentCulture;
        var date = item.Modified.ToString("d MMM yyyy HH:mm", culture);

        if (!item.IsDirectory)
            return $"{date}  ·  {SizeFormatter.Format(item.Size, culture)}";

        var count = _files.CountEntries(item.FullPath);
        var contents = count switch
        {
            < 0 => string.Empty,
            0 => _l["FolderEmpty"],
            1 => _l["FolderOneItem"],
            _ => string.Format(culture, _l["FolderItems"], count)
        };

        return string.IsNullOrEmpty(contents) ? date : $"{date}  ·  {contents}";
    }

    private void SetBusy(bool busy)
    {
        IsBusy = busy;
        Notify();
    }

    private void UpdateHeader()
    {
        var isRoot = IsRoot(CurrentPath);
        Title = isRoot ? _l["InternalStorage"] : Path.GetFileName(CurrentPath);
        UpVisible = !isRoot;

        var count = Items.Count switch
        {
            0 => string.Empty,
            1 => _l["OneItem"],
            _ => string.Format(_l.CurrentCulture, _l["ItemsCount"], Items.Count)
        };
        // Si hay filtro por tipo activo, se indica en el subtitulo.
        Subtitle = TypeFilter is null
            ? count
            : (string.IsNullOrEmpty(count) ? FilterName(TypeFilter) : $"{count}  ·  {FilterName(TypeFilter)}");
        Notify();
    }

    // ============ Navegacion ============

    public bool IsRoot(string path) =>
        string.Equals(Path.TrimEndingDirectorySeparator(path),
                      Path.TrimEndingDirectorySeparator(_files.RootPath),
                      StringComparison.OrdinalIgnoreCase);

    private void BuildBreadcrumb()
    {
        var root = Path.TrimEndingDirectorySeparator(_files.RootPath);
        var segments = new List<(string Label, string Path)> { (_l["InternalStorage"], root) };

        if (!IsRoot(CurrentPath) && CurrentPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            var relative = CurrentPath[root.Length..].Trim(Path.DirectorySeparatorChar);
            var accumulated = root;

            foreach (var segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                accumulated = Path.Combine(accumulated, segment);
                segments.Add((segment, accumulated));
            }
        }

        Breadcrumb = segments.Select((s, i) => new Crumb(s.Label, s.Path, i == segments.Count - 1)).ToList();
    }

    public async Task<bool> NavigateUpAsync()
    {
        if (SearchActive)
        {
            ExitSearch();
            await LoadAsync(CurrentPath);
            return true;
        }

        if (IsRoot(CurrentPath))
            return false;

        var parent = Path.GetDirectoryName(CurrentPath);
        if (string.IsNullOrEmpty(parent))
            return false;

        await LoadAsync(parent);
        return true;
    }

    /// <summary>
    /// Boton atras: en seleccion sale de la seleccion; en una busqueda o en una subcarpeta sube.
    /// Devuelve lo que hay que hacer, o null si la pagina no lo atiende (en la raiz).
    /// </summary>
    public Func<Task>? BackAction()
    {
        if (SelectMode)
            return () => { ExitSelectionMode(); return Task.CompletedTask; };

        // El boton atras de Android sube una carpeta en lugar de cerrar la aplicacion.
        if (SearchActive || !IsRoot(CurrentPath))
            return NavigateUpAsync;

        return null;
    }

    /// <summary>Pulsacion larga: inicia el modo seleccion y marca el elemento.</summary>
    public void ItemLongPressed(FileItem item)
    {
        if (!SelectMode)
            EnterSelectionMode();

        item.IsSelected = true;
        UpdateSelectionCount();
    }

    public async Task ItemTappedAsync(FileItem item)
    {
        // En modo seleccion, tocar una fila la marca/desmarca (no abre).
        if (SelectMode)
        {
            item.IsSelected = !item.IsSelected;
            UpdateSelectionCount();
            return;
        }

        if (item.IsDirectory)
        {
            if (SearchActive)
                ExitSearch();

            await LoadAsync(item.FullPath);
            return;
        }

        await OpenFileAsync(item);
    }

    private async Task OpenFileAsync(FileItem item)
    {
        try
        {
            if (!await _actions.OpenAsync(item.FullPath))
                await _dialogs.AlertAsync(_l["Error"], _l["ErrorNoAppForFile"], _l["Ok"]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open the file");
            await _dialogs.AlertAsync(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorOpenFile"], ex.Message), _l["Ok"]);
        }
    }

    // ============ Busqueda ============

    public async Task ToggleSearchAsync()
    {
        if (SearchActive)
        {
            ExitSearch();
            await LoadAsync(CurrentPath);
            return;
        }

        SearchActive = true;
        SearchBarVisible = true;
        Notify();
        _view.FocusSearch();
    }

    private void ExitSearch()
    {
        _searchCts?.Cancel();
        SearchActive = false;
        SearchBarVisible = false;
        SearchText = string.Empty;
        Notify();
        _view.UnfocusSearch();
    }

    /// <summary>Texto del buscador cambiado: espera breve y busca desde la carpeta actual.</summary>
    public async Task SearchTextChangedAsync(string? text, int debounceMs = 350)
    {
        SearchText = text ?? string.Empty;
        var query = SearchText.Trim();

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        if (query.Length == 0)
        {
            await LoadAsync(CurrentPath);
            return;
        }

        try
        {
            // Espera breve: no se lanza una busqueda por cada tecla pulsada.
            await Task.Delay(debounceMs, token);

            SetBusy(true);
            var results = await _files.SearchAsync(CurrentPath, query, _settings.ShowHiddenFiles, token);

            if (!token.IsCancellationRequested)
                Populate(results, isSearch: true);
        }
        catch (OperationCanceledException)
        {
            // Cancelada por una pulsacion posterior: no hay nada que informar.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed");
            await _dialogs.AlertAsync(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorSearch"], ex.Message), _l["Ok"]);
        }
        finally
        {
            if (!token.IsCancellationRequested)
                SetBusy(false);
        }
    }

    // ============ Menu de un elemento ============

    public async Task ItemMenuAsync(FileItem item)
    {
        var options = item.IsDirectory
            ? new[] { _l["Select"], _l["Open"], _l["Copy"], _l["Cut"], _l["Rename"], _l["Details"], _l["Delete"] }
            : new[] { _l["Select"], _l["Open"], _l["Share"], _l["Copy"], _l["Cut"], _l["Rename"], _l["Details"], _l["Delete"] };

        var choice = await _dialogs.ActionSheetAsync(item.Name, _l["Cancel"], options);

        if (choice == _l["Select"])
        {
            EnterSelectionMode();
            item.IsSelected = true;
            UpdateSelectionCount();
        }
        else if (choice == _l["Open"])
        {
            if (item.IsDirectory)
                await LoadAsync(item.FullPath);
            else
                await OpenFileAsync(item);
        }
        else if (choice == _l["Share"])
        {
            await ShareAsync(item);
        }
        else if (choice == _l["Copy"])
        {
            _clipboard.Set(new[] { item.FullPath }, isMove: false);
            _toast.Show(string.Format(_l.CurrentCulture, _l["Copied"], 1));
        }
        else if (choice == _l["Cut"])
        {
            _clipboard.Set(new[] { item.FullPath }, isMove: true);
            _toast.Show(string.Format(_l.CurrentCulture, _l["CutToClipboard"], 1));
        }
        else if (choice == _l["Rename"])
        {
            await RenameAsync(item);
        }
        else if (choice == _l["Details"])
        {
            await ShowDetailsAsync(item);
        }
        else if (choice == _l["Delete"])
        {
            await DeleteAsync(item);
        }
    }

    private async Task ShareAsync(FileItem item)
    {
        try
        {
            await _actions.ShareAsync(item.FullPath, item.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not share the file");
            await _dialogs.AlertAsync(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorShare"], ex.Message), _l["Ok"]);
        }
    }

    private async Task RenameAsync(FileItem item)
    {
        var name = await _dialogs.PromptAsync(_l["RenameTitle"], _l["RenamePrompt"], _l["Save"], _l["Cancel"], item.Name);
        if (name is null)
            return;

        var parent = Path.GetDirectoryName(item.FullPath) ?? CurrentPath;
        if (!await ValidateNameAsync(name, parent, item.FullPath))
            return;

        if (string.Equals(name.Trim(), item.Name, StringComparison.Ordinal))
            return;

        try
        {
            await _files.RenameAsync(item.FullPath, name);
            await LoadAsync(CurrentPath);
            _toast.Show(_l["Renamed"]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rename failed");
            await _dialogs.AlertAsync(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorRename"], ex.Message), _l["Ok"]);
        }
    }

    /// <summary>Texto de la ficha de detalles de un elemento.</summary>
    public string DescribeDetails(FileItem item)
    {
        var culture = _l.CurrentCulture;
        var lines = new List<string>
        {
            $"{_l["DetailsName"]}: {item.Name}",
            $"{_l["DetailsPath"]}: {item.FullPath}",
            $"{_l["DetailsType"]}: {(item.IsDirectory ? _l["TypeFolder"] : DescribeFileType(item))}",
            $"{_l["DetailsModified"]}: {item.Modified.ToString("F", culture)}"
        };

        if (item.IsDirectory)
        {
            var count = _files.CountEntries(item.FullPath);
            if (count >= 0)
                lines.Add($"{_l["DetailsContents"]}: {string.Format(culture, _l["FolderItems"], count)}");
        }
        else
        {
            lines.Add($"{_l["DetailsSize"]}: {SizeFormatter.Format(item.Size, culture)}");
        }

        return string.Join("\n\n", lines);
    }

    private Task ShowDetailsAsync(FileItem item) =>
        _dialogs.AlertAsync(_l["Details"], DescribeDetails(item), _l["Close"]);

    private string DescribeFileType(FileItem item) =>
        string.IsNullOrEmpty(item.Extension)
            ? _l["TypeFile"]
            : $"{item.Extension.ToUpperInvariant()} · {MimeTypes.ForPath(item.FullPath)}";

    private async Task DeleteAsync(FileItem item)
    {
        if (_settings.ConfirmDelete)
        {
            var message = string.Format(
                _l.CurrentCulture,
                item.IsDirectory ? _l["DeleteFolderConfirm"] : _l["DeleteFileConfirm"],
                item.Name);

            if (!await _dialogs.AlertAsync(_l["DeleteTitle"], message, _l["Delete"], _l["Cancel"]))
                return;
        }

        await DeletePathsAsync(new[] { item.FullPath }, exitSelection: false);
    }

    private async Task DeletePathsAsync(IReadOnlyList<string> paths, bool exitSelection)
    {
        SetBusy(true);
        try
        {
            var result = await _files.DeleteAsync(paths);
            if (exitSelection)
                ExitSelectionMode();
            await LoadAsync(CurrentPath);

            if (result.HasErrors)
                await _dialogs.AlertAsync(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorDelete"], string.Join("\n", result.Errors)), _l["Ok"]);
            else
                _toast.Show(string.Format(_l.CurrentCulture, _l["Deleted"], result.Succeeded));
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ============ Seleccion multiple / operaciones en lote ============

    public void EnterSelectionMode()
    {
        if (SelectMode)
            return;
        if (SearchActive)
            ExitSearch();
        SelectMode = true;
        SelectionBarVisible = true;
        SearchButtonVisible = false;
        NewFolderVisible = false;
        PasteBarVisible = false;
        UpdateSelectionCount();
    }

    public void ExitSelectionMode()
    {
        SelectMode = false;
        foreach (var it in _allItems)
            it.IsSelected = false;
        SelectionBarVisible = false;
        SearchButtonVisible = true;
        NewFolderVisible = !SearchActive;
        UpdatePasteBar();
    }

    public List<FileItem> SelectedItems() => Items.Where(i => i.IsSelected).ToList();

    private void UpdateSelectionCount()
    {
        var n = Items.Count(i => i.IsSelected);
        SelectionCountText = string.Format(_l.CurrentCulture, _l["SelectedCount"], n);
        Notify();
    }

    /// <summary>Marca todos los visibles o, si ya lo estaban, los desmarca.</summary>
    public void SelectAll()
    {
        var visible = Items.ToList();
        var allSelected = visible.Count > 0 && visible.All(i => i.IsSelected);
        foreach (var it in visible)
            it.IsSelected = !allSelected;
        UpdateSelectionCount();
    }

    public async Task BatchActionsAsync()
    {
        var selected = SelectedItems();
        if (selected.Count == 0)
        {
            _toast.Show(_l["NothingSelected"]);
            return;
        }

        var choice = await _dialogs.ActionSheetAsync(
            string.Format(_l.CurrentCulture, _l["SelectedCount"], selected.Count),
            _l["Cancel"],
            _l["Copy"], _l["Cut"], _l["Delete"]);

        if (choice == _l["Copy"] || choice == _l["Cut"])
        {
            _clipboard.Set(selected.Select(i => i.FullPath).ToArray(), isMove: choice == _l["Cut"]);
            ExitSelectionMode();
        }
        else if (choice == _l["Delete"])
        {
            if (_settings.ConfirmDelete)
            {
                var message = string.Format(_l.CurrentCulture, _l["DeleteManyConfirm"], selected.Count);
                if (!await _dialogs.AlertAsync(_l["DeleteTitle"], message, _l["Delete"], _l["Cancel"]))
                    return;
            }

            await DeletePathsAsync(selected.Select(i => i.FullPath).ToArray(), exitSelection: true);
        }
    }

    // ============ Nueva carpeta ============

    public async Task NewFolderAsync()
    {
        var name = await _dialogs.PromptAsync(_l["NewFolderTitle"], _l["NewFolderPrompt"], _l["Save"], _l["Cancel"]);
        if (name is null)
            return;

        if (!await ValidateNameAsync(name, CurrentPath))
            return;

        try
        {
            await _files.CreateDirectoryAsync(CurrentPath, name);
            await LoadAsync(CurrentPath);
            _toast.Show(_l["FolderCreated"]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not create the folder");
            await _dialogs.AlertAsync(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorCreateFolder"], ex.Message), _l["Ok"]);
        }
    }

    private async Task<bool> ValidateNameAsync(string name, string parent, string? currentPath = null)
    {
        var validation = _files.ValidateName(name, parent, currentPath);
        if (validation == NameValidation.Valid)
            return true;

        var message = validation switch
        {
            NameValidation.Empty => _l["ErrorNameEmpty"],
            NameValidation.InvalidCharacters => _l["ErrorNameInvalid"],
            _ => _l["ErrorNameExists"]
        };

        await _dialogs.AlertAsync(_l["Error"], message, _l["Ok"]);
        return false;
    }

    // ============ Portapapeles ============

    /// <summary>Barra de pegar: visible con algo copiado, fuera de la busqueda y con permiso.</summary>
    public void UpdatePasteBar()
    {
        var entry = _clipboard.Current;
        PasteBarVisible = _clipboard.HasContent && !SearchActive && _permissions.HasFullAccess;

        if (PasteBarVisible && entry is not null)
        {
            var action = entry.IsMove ? _l["Cut"] : _l["Copy"];
            var names = string.Join(", ", entry.Paths.Select(Path.GetFileName));
            PasteText = $"{action}: {names}";
        }
        Notify();
    }

    public void CancelPaste() => _clipboard.Clear();

    public async Task PasteAsync()
    {
        var entry = _clipboard.Current;
        if (entry is null)
            return;

        // Pegar una carpeta dentro de si misma crearia una recursion infinita.
        if (entry.Paths.Any(p => Directory.Exists(p) && _files.IsInside(p, CurrentPath)))
        {
            await _dialogs.AlertAsync(_l["Error"], _l["ErrorPasteIntoItself"], _l["Ok"]);
            return;
        }

        var resolution = ConflictResolution.KeepBoth;
        var conflicts = _files.GetConflicts(entry.Paths, CurrentPath);

        if (conflicts.Count > 0)
        {
            var answer = await _dialogs.ActionSheetAsync(
                string.Format(_l.CurrentCulture, _l["OverwriteConfirm"], string.Join(", ", conflicts)),
                _l["Cancel"],
                _l["Replace"], _l["KeepBoth"]);

            if (answer == _l["Replace"])
                resolution = ConflictResolution.Replace;
            else if (answer != _l["KeepBoth"])
                return;
        }

        SetBusy(true);
        try
        {
            var result = await _files.PasteAsync(entry, CurrentPath, resolution);

            // Tras mover, el portapapeles ya no apunta a rutas validas.
            if (entry.IsMove)
                _clipboard.Clear();

            await LoadAsync(CurrentPath);

            if (result.HasErrors)
                await _dialogs.AlertAsync(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorPaste"], string.Join("\n", result.Errors)), _l["Ok"]);
            else
                _toast.Show(string.Format(_l.CurrentCulture, entry.IsMove ? _l["Moved"] : _l["Pasted"], result.Succeeded));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paste failed");
            await _dialogs.AlertAsync(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorPaste"], ex.Message), _l["Ok"]);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ============ Menu general ============

    public async Task MoreMenuAsync()
    {
        var hidden = _settings.ShowHiddenFiles ? _l["ShowHiddenOff"] : _l["ShowHiddenOn"];
        var choice = await _dialogs.ActionSheetAsync(
            _l["More"],
            _l["Cancel"],
            _l["Select"], _l["Filter"], _l["Sort"], hidden, _l["Refresh"], _l["Settings"], _l["About"]);

        if (choice == _l["Select"])
        {
            EnterSelectionMode();
        }
        else if (choice == _l["Filter"])
        {
            await ShowFilterMenuAsync();
        }
        else if (choice == _l["Sort"])
        {
            await ShowSortMenuAsync();
        }
        else if (choice == hidden)
        {
            _settings.ShowHiddenFiles = !_settings.ShowHiddenFiles;
            await LoadAsync(CurrentPath);
        }
        else if (choice == _l["Refresh"])
        {
            await LoadAsync(CurrentPath);
        }
        else if (choice == _l["Settings"])
        {
            await _view.OpenSettingsAsync();
        }
        else if (choice == _l["About"])
        {
            await _view.OpenAboutAsync();
        }
    }

    public async Task ShowSortMenuAsync()
    {
        var options = new Dictionary<string, SortMode>
        {
            [_l["SortNameAsc"]] = SortMode.NameAscending,
            [_l["SortNameDesc"]] = SortMode.NameDescending,
            [_l["SortDateDesc"]] = SortMode.DateDescending,
            [_l["SortDateAsc"]] = SortMode.DateAscending,
            [_l["SortSizeDesc"]] = SortMode.SizeDescending,
            [_l["SortSizeAsc"]] = SortMode.SizeAscending
        };

        var choice = await _dialogs.ActionSheetAsync(_l["Sort"], _l["Cancel"], options.Keys.ToArray());

        if (choice is not null && options.TryGetValue(choice, out var mode))
        {
            _settings.Sort = mode;
            await LoadAsync(CurrentPath);
        }
    }
}
