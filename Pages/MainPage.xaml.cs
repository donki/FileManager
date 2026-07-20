using System.Collections.ObjectModel;
using FileManager.Helpers;
using FileManager.Models;
using FileManager.Services;
using Microsoft.Extensions.Logging;

namespace FileManager.Pages;

public partial class MainPage : ContentPage
{
    private readonly IFileSystemService _files;
    private readonly IFileClipboardService _clipboard;
    private readonly IFileActionsService _actions;
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _l;
    private readonly IStoragePermissionService _permissions;
    private readonly IToastService _toast;
    private readonly UpdateService _update;
    private readonly ILogger<MainPage> _logger;

    private readonly ObservableCollection<FileItem> _items = new();

    /// <summary>Lista completa cargada de la carpeta (sin filtrar). `_items` es su vista filtrada.</summary>
    private readonly List<FileItem> _allItems = new();

    /// <summary>Filtro por tipo activo (null = todos).</summary>
    private FileCategory? _typeFilter;

    /// <summary>Modo de selección múltiple activo (operaciones en lote).</summary>
    private bool _selectMode;

    private string _currentPath = string.Empty;
    private bool _searchActive;
    private CancellationTokenSource? _searchCts;

    /// <summary>Se pidio el permiso y estamos esperando a que el usuario vuelva de los ajustes.</summary>
    private bool _awaitingPermission;

    public MainPage(
        IFileSystemService files,
        IFileClipboardService clipboard,
        IFileActionsService actions,
        ISettingsService settings,
        ILocalizationService localization,
        IStoragePermissionService permissions,
        IToastService toast,
        UpdateService update,
        ILogger<MainPage> logger)
    {
        InitializeComponent();

        _files = files;
        _clipboard = clipboard;
        _actions = actions;
        _settings = settings;
        _l = localization;
        _permissions = permissions;
        _toast = toast;
        _update = update;
        _logger = logger;

        FilesView.ItemsSource = _items;
        _currentPath = _files.RootPath;

        _clipboard.Changed += OnClipboardChanged;
        _l.LanguageChanged += OnLanguageChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Conceder el permiso ocurre en otra Activity del sistema, y al volver de ella
        // Android reanuda la ventana sin disparar OnAppearing: sin escuchar Resumed la
        // pantalla de permisos se quedaria puesta con el acceso ya concedido.
        if (Window is not null)
        {
            Window.Resumed -= OnWindowResumed;
            Window.Resumed += OnWindowResumed;
        }

        ApplyTexts();
        UpdatePasteBar();

        // Comprobacion de version al arrancar (constitucion 15): no bloqueante y silenciosa si ya
        // se esta al dia. El propio servicio solo comprueba una vez por sesion.
        _ = _update.CheckAndPromptAsync(this);

        await RefreshAccessAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (Window is not null)
            Window.Resumed -= OnWindowResumed;
    }

    private void OnWindowResumed(object? sender, EventArgs e) =>
        Dispatcher.Dispatch(async () => await RefreshAccessAsync());

    /// <summary>
    /// Consulta el estado del permiso y muestra la lista o la pantalla de permisos.
    /// Android no informa del resultado de la pantalla de ajustes, asi que hay que
    /// preguntar de nuevo cada vez que la aplicacion recupera el foco.
    /// </summary>
    private async Task RefreshAccessAsync()
    {
        if (!_permissions.HasFullAccess)
        {
            ShowPermissionGate();
            return;
        }

        if (_awaitingPermission)
        {
            _awaitingPermission = false;
            _toast.Show(_l["PermissionGranted"]);
        }

        PermissionView.IsVisible = false;

        // Recargar durante una busqueda borraria los resultados que el usuario esta viendo.
        if (!_searchActive)
            await LoadAsync(_currentPath);
    }

    // ============ Textos ============

    private void OnLanguageChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            ApplyTexts();
            if (_permissions.HasFullAccess)
                await LoadAsync(_currentPath);
        });

    /// <summary>
    /// Vuelca los textos del idioma activo en la interfaz. Se llama al aparecer la pagina
    /// y cada vez que cambia el idioma (constitucion 15).
    /// </summary>
    private void ApplyTexts()
    {
        FileSearchBar.Placeholder = _l["SearchPlaceholder"];
        EmptyTitleLabel.Text = _l["EmptyFolder"];
        EmptyHintLabel.Text = _l["EmptyFolderHint"];
        PermissionTitleLabel.Text = _l["PermissionTitle"];
        PermissionBodyLabel.Text = _l["PermissionBody"];
        PermissionButton.Text = _l["PermissionGrant"];
        PasteConfirmButton.Text = _l["Paste"];
        PasteCancelButton.Text = _l["Cancel"];
        SelectAllButton.Text = _l["SelectAll"];

        UpdateHeader();
    }

    private void UpdateHeader()
    {
        var isRoot = IsRoot(_currentPath);
        TitleLabel.Text = isRoot ? _l["InternalStorage"] : Path.GetFileName(_currentPath);
        UpButton.IsVisible = !isRoot;

        var count = _items.Count switch
        {
            0 => string.Empty,
            1 => _l["OneItem"],
            _ => string.Format(_l.CurrentCulture, _l["ItemsCount"], _items.Count)
        };
        // Si hay filtro por tipo activo, se indica en el subtítulo.
        SubtitleLabel.Text = _typeFilter is null
            ? count
            : (string.IsNullOrEmpty(count) ? FilterName(_typeFilter) : $"{count}  ·  {FilterName(_typeFilter)}");
    }

    // ============ Permisos ============

    private void ShowPermissionGate()
    {
        PermissionView.IsVisible = true;
        FilesView.IsVisible = false;
        EmptyView.IsVisible = false;
        NewFolderButton.IsVisible = false;
        PasteBar.IsVisible = false;
    }

    private async void OnRequestPermissionClicked(object? sender, EventArgs e)
    {
        try
        {
            _awaitingPermission = true;
            await _permissions.RequestFullAccessAsync();
        }
        catch (Exception ex)
        {
            _awaitingPermission = false;
            _logger.LogError(ex, "Could not open the storage permission settings");
            await DisplayAlert(_l["Error"], ex.Message, _l["Ok"]);
        }
    }

    // ============ Listado ============

    private async Task LoadAsync(string path)
    {
        if (!Directory.Exists(path))
        {
            // La carpeta pudo borrarse desde otra app mientras estabamos dentro.
            _logger.LogInformation("Current folder no longer exists, falling back to the root");
            path = _files.RootPath;
        }

        _currentPath = path;
        SetBusy(true);

        try
        {
            var items = await _files.ListAsync(path, _settings.ShowHiddenFiles, _settings.Sort);
            Populate(items, isSearch: false);
        }
        catch (UnauthorizedAccessException)
        {
            await DisplayAlert(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorAccessDenied"], path), _l["Ok"]);
            await NavigateUpIfPossible();
        }
        catch (Exception ex) when (ex is IOException)
        {
            _logger.LogError(ex, "Could not list the folder");
            await DisplayAlert(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorReadFolder"], ex.Message), _l["Ok"]);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Populate(IReadOnlyList<FileItem> items, bool isSearch)
    {
        // Cambiar de carpeta/lista sale del modo selección.
        if (_selectMode)
            ExitSelectionMode();

        _allItems.Clear();
        foreach (var item in items)
        {
            item.Icon = FileIcons.For(item);
            item.Details = DescribeItem(item);
            item.IsSelected = false;
            _allItems.Add(item);
        }

        FilesView.IsVisible = true;
        PermissionView.IsVisible = false;
        NewFolderButton.IsVisible = !isSearch;

        ApplyFilterAndShow();

        EmptyIconLabel.Text = isSearch ? "🔍" : "📂";
        EmptyTitleLabel.Text = isSearch ? _l["NoSearchResults"] : _l["EmptyFolder"];
        EmptyHintLabel.IsVisible = !isSearch;

        BuildBreadcrumb();
        UpdateHeader();
        UpdatePasteBar();
    }

    /// <summary>Rellena <see cref="_items"/> desde <see cref="_allItems"/> aplicando el filtro por
    /// tipo. Las CARPETAS se muestran siempre (para poder seguir navegando aunque haya filtro).</summary>
    private void ApplyFilterAndShow()
    {
        _items.Clear();
        foreach (var item in _allItems)
        {
            if (_typeFilter is null || item.IsDirectory || item.Category == _typeFilter)
                _items.Add(item);
        }
        EmptyView.IsVisible = _items.Count == 0;
    }

    /// <summary>Nombre localizado de una categoría de tipo (para el menú y el subtítulo).</summary>
    private string FilterName(FileCategory? category) => category switch
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

    private async Task ShowFilterMenuAsync()
    {
        var options = new (string Label, FileCategory? Cat)[]
        {
            (_l["FilterAll"], null),
            (_l["FilterImages"], FileCategory.Image),
            (_l["FilterVideo"], FileCategory.Video),
            (_l["FilterAudio"], FileCategory.Audio),
            (_l["FilterDocuments"], FileCategory.Document),
            (_l["FilterApk"], FileCategory.Apk),
            (_l["FilterArchives"], FileCategory.Archive),
            (_l["FilterOther"], FileCategory.Other),
        };

        var choice = await DisplayActionSheet(_l["Filter"], _l["Cancel"], null,
            options.Select(o => o.Label).ToArray());
        if (choice is null || choice == _l["Cancel"])
            return;

        var picked = options.FirstOrDefault(o => o.Label == choice);
        _typeFilter = picked.Cat;
        ApplyFilterAndShow();
        UpdateHeader();
    }

    /// <summary>Linea secundaria de cada fila: fecha y, para ficheros, tamano.</summary>
    private string DescribeItem(FileItem item)
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
        Busy.IsVisible = busy;
        Busy.IsRunning = busy;
    }

    // ============ Navegacion ============

    private bool IsRoot(string path) =>
        string.Equals(Path.TrimEndingDirectorySeparator(path),
                      Path.TrimEndingDirectorySeparator(_files.RootPath),
                      StringComparison.OrdinalIgnoreCase);

    private void BuildBreadcrumb()
    {
        BreadcrumbBar.Clear();

        var root = Path.TrimEndingDirectorySeparator(_files.RootPath);
        var segments = new List<(string Label, string Path)> { (_l["InternalStorage"], root) };

        if (!IsRoot(_currentPath) && _currentPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            var relative = _currentPath[root.Length..].Trim(Path.DirectorySeparatorChar);
            var accumulated = root;

            foreach (var segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                accumulated = Path.Combine(accumulated, segment);
                segments.Add((segment, accumulated));
            }
        }

        for (var index = 0; index < segments.Count; index++)
        {
            var (label, path) = segments[index];
            var isLast = index == segments.Count - 1;

            if (index > 0)
            {
                BreadcrumbBar.Add(new Label
                {
                    Text = "›",
                    TextColor = Color.FromArgb("#8FB4D8"),
                    FontSize = 14,
                    VerticalOptions = LayoutOptions.Center
                });
            }

            var crumb = new Button
            {
                Text = label,
                FontSize = 13,
                FontAttributes = isLast ? FontAttributes.Bold : FontAttributes.None,
                TextColor = isLast ? Colors.White : Color.FromArgb("#CFE0F0"),
                BackgroundColor = Colors.Transparent,
                Padding = new Thickness(6, 2),
                HeightRequest = 30,
                MinimumWidthRequest = 0
            };

            if (!isLast)
                crumb.Clicked += async (_, _) => await LoadAsync(path);

            BreadcrumbBar.Add(crumb);
        }

        // La carpeta actual queda a la derecha del todo: se desplaza para que se vea.
        Dispatcher.Dispatch(async () =>
            await BreadcrumbScroll.ScrollToAsync(BreadcrumbBar, ScrollToPosition.End, false));
    }

    private void OnMenuClicked(object? sender, EventArgs e)
    {
        // Abre el menu hamburguesa (flyout de Shell): navegacion de primer nivel (constitucion A.9).
        if (Shell.Current is not null)
            Shell.Current.FlyoutIsPresented = true;
    }

    private async void OnUpClicked(object? sender, EventArgs e) => await NavigateUpIfPossible();

    private async Task<bool> NavigateUpIfPossible()
    {
        if (_searchActive)
        {
            ExitSearch();
            await LoadAsync(_currentPath);
            return true;
        }

        if (IsRoot(_currentPath))
            return false;

        var parent = Path.GetDirectoryName(_currentPath);
        if (string.IsNullOrEmpty(parent))
            return false;

        await LoadAsync(parent);
        return true;
    }

    protected override bool OnBackButtonPressed()
    {
        // En modo selección, el botón atrás sale de la selección.
        if (_selectMode)
        {
            Dispatcher.Dispatch(ExitSelectionMode);
            return true;
        }

        // El boton atras de Android sube una carpeta en lugar de cerrar la aplicacion.
        if (_searchActive || !IsRoot(_currentPath))
        {
            Dispatcher.Dispatch(async () => await NavigateUpIfPossible());
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private async void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not FileItem item)
            return;

        // En modo selección, tocar una fila la marca/desmarca (no abre).
        if (_selectMode)
        {
            item.IsSelected = !item.IsSelected;
            UpdateSelectionCount();
            return;
        }

        if (item.IsDirectory)
        {
            if (_searchActive)
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
                await DisplayAlert(_l["Error"], _l["ErrorNoAppForFile"], _l["Ok"]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open the file");
            await DisplayAlert(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorOpenFile"], ex.Message), _l["Ok"]);
        }
    }

    // ============ Busqueda ============

    private void OnSearchToggleClicked(object? sender, EventArgs e)
    {
        if (_searchActive)
        {
            ExitSearch();
            Dispatcher.Dispatch(async () => await LoadAsync(_currentPath));
            return;
        }

        _searchActive = true;
        FileSearchBar.IsVisible = true;
        FileSearchBar.Focus();
    }

    private void ExitSearch()
    {
        _searchCts?.Cancel();
        _searchActive = false;
        FileSearchBar.IsVisible = false;
        FileSearchBar.Text = string.Empty;
        FileSearchBar.Unfocus();
    }

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue?.Trim() ?? string.Empty;

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        if (query.Length == 0)
        {
            await LoadAsync(_currentPath);
            return;
        }

        try
        {
            // Espera breve: no se lanza una busqueda por cada tecla pulsada.
            await Task.Delay(350, token);

            SetBusy(true);
            var results = await _files.SearchAsync(_currentPath, query, _settings.ShowHiddenFiles, token);

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
            await DisplayAlert(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorSearch"], ex.Message), _l["Ok"]);
        }
        finally
        {
            if (!token.IsCancellationRequested)
                SetBusy(false);
        }
    }

    // ============ Menu de un elemento ============

    private async void OnItemMenuClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not FileItem item)
            return;

        var options = item.IsDirectory
            ? new[] { _l["Open"], _l["Copy"], _l["Cut"], _l["Rename"], _l["Details"], _l["Delete"] }
            : new[] { _l["Open"], _l["Share"], _l["Copy"], _l["Cut"], _l["Rename"], _l["Details"], _l["Delete"] };

        var choice = await DisplayActionSheet(item.Name, _l["Cancel"], null, options);

        if (choice == _l["Open"])
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
            await DisplayAlert(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorShare"], ex.Message), _l["Ok"]);
        }
    }

    private async Task RenameAsync(FileItem item)
    {
        var name = await DisplayPromptAsync(
            _l["RenameTitle"],
            _l["RenamePrompt"],
            accept: _l["Save"],
            cancel: _l["Cancel"],
            initialValue: item.Name,
            maxLength: 255);

        if (name is null)
            return;

        var parent = Path.GetDirectoryName(item.FullPath) ?? _currentPath;
        if (!await ValidateNameAsync(name, parent, item.FullPath))
            return;

        if (string.Equals(name.Trim(), item.Name, StringComparison.Ordinal))
            return;

        try
        {
            await _files.RenameAsync(item.FullPath, name);
            await LoadAsync(_currentPath);
            _toast.Show(_l["Renamed"]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rename failed");
            await DisplayAlert(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorRename"], ex.Message), _l["Ok"]);
        }
    }

    private async Task ShowDetailsAsync(FileItem item)
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

        await DisplayAlert(_l["Details"], string.Join("\n\n", lines), _l["Close"]);
    }

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

            if (!await DisplayAlert(_l["DeleteTitle"], message, _l["Delete"], _l["Cancel"]))
                return;
        }

        SetBusy(true);
        try
        {
            var result = await _files.DeleteAsync(new[] { item.FullPath });
            await LoadAsync(_currentPath);

            if (result.HasErrors)
                await DisplayAlert(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorDelete"], string.Join("\n", result.Errors)), _l["Ok"]);
            else
                _toast.Show(string.Format(_l.CurrentCulture, _l["Deleted"], result.Succeeded));
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ============ Selección múltiple / operaciones en lote ============

    private void EnterSelectionMode()
    {
        if (_selectMode)
            return;
        if (_searchActive)
            ExitSearch();
        _selectMode = true;
        SelectionBar.IsVisible = true;
        SearchButton.IsVisible = false;
        NewFolderButton.IsVisible = false;
        PasteBar.IsVisible = false;
        UpdateSelectionCount();
    }

    private void ExitSelectionMode()
    {
        _selectMode = false;
        foreach (var it in _allItems)
            it.IsSelected = false;
        SelectionBar.IsVisible = false;
        SearchButton.IsVisible = true;
        NewFolderButton.IsVisible = !_searchActive;
        UpdatePasteBar();
    }

    private List<FileItem> SelectedItems() => _items.Where(i => i.IsSelected).ToList();

    private void UpdateSelectionCount()
    {
        var n = _items.Count(i => i.IsSelected);
        SelCountLabel.Text = string.Format(_l.CurrentCulture, _l["SelectedCount"], n);
    }

    private void OnSelectionCloseClicked(object? sender, EventArgs e) => ExitSelectionMode();

    private void OnSelectAllClicked(object? sender, EventArgs e)
    {
        var visible = _items.ToList();
        var allSelected = visible.Count > 0 && visible.All(i => i.IsSelected);
        foreach (var it in visible)
            it.IsSelected = !allSelected;
        UpdateSelectionCount();
    }

    private async void OnBatchActionsClicked(object? sender, EventArgs e)
    {
        var selected = SelectedItems();
        if (selected.Count == 0)
        {
            _toast.Show(_l["NothingSelected"]);
            return;
        }

        var choice = await DisplayActionSheet(
            string.Format(_l.CurrentCulture, _l["SelectedCount"], selected.Count),
            _l["Cancel"], null,
            _l["Copy"], _l["Cut"], _l["Delete"]);

        if (choice == _l["Copy"])
        {
            _clipboard.Set(selected.Select(i => i.FullPath).ToArray(), isMove: false);
            ExitSelectionMode();
        }
        else if (choice == _l["Cut"])
        {
            _clipboard.Set(selected.Select(i => i.FullPath).ToArray(), isMove: true);
            ExitSelectionMode();
        }
        else if (choice == _l["Delete"])
        {
            await BatchDeleteAsync(selected);
        }
    }

    private async Task BatchDeleteAsync(IReadOnlyList<FileItem> selected)
    {
        if (_settings.ConfirmDelete)
        {
            var message = string.Format(_l.CurrentCulture, _l["DeleteManyConfirm"], selected.Count);
            if (!await DisplayAlert(_l["DeleteTitle"], message, _l["Delete"], _l["Cancel"]))
                return;
        }

        SetBusy(true);
        try
        {
            var result = await _files.DeleteAsync(selected.Select(i => i.FullPath).ToArray());
            ExitSelectionMode();
            await LoadAsync(_currentPath);

            if (result.HasErrors)
                await DisplayAlert(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorDelete"], string.Join("\n", result.Errors)), _l["Ok"]);
            else
                _toast.Show(string.Format(_l.CurrentCulture, _l["Deleted"], result.Succeeded));
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ============ Nueva carpeta ============

    private async void OnNewFolderClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync(
            _l["NewFolderTitle"],
            _l["NewFolderPrompt"],
            accept: _l["Save"],
            cancel: _l["Cancel"],
            maxLength: 255);

        if (name is null)
            return;

        if (!await ValidateNameAsync(name, _currentPath))
            return;

        try
        {
            await _files.CreateDirectoryAsync(_currentPath, name);
            await LoadAsync(_currentPath);
            _toast.Show(_l["FolderCreated"]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not create the folder");
            await DisplayAlert(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorCreateFolder"], ex.Message), _l["Ok"]);
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

        await DisplayAlert(_l["Error"], message, _l["Ok"]);
        return false;
    }

    // ============ Portapapeles ============

    private void OnClipboardChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(UpdatePasteBar);

    private void UpdatePasteBar()
    {
        var entry = _clipboard.Current;
        var visible = _clipboard.HasContent && !_searchActive && _permissions.HasFullAccess;

        PasteBar.IsVisible = visible;
        if (!visible || entry is null)
            return;

        var action = entry.IsMove ? _l["Cut"] : _l["Copy"];
        var names = string.Join(", ", entry.Paths.Select(Path.GetFileName));
        PasteLabel.Text = $"{action}: {names}";
        PasteConfirmButton.Text = _l["Paste"];
        PasteCancelButton.Text = _l["Cancel"];
    }

    private void OnCancelPasteClicked(object? sender, EventArgs e) => _clipboard.Clear();

    private async void OnPasteClicked(object? sender, EventArgs e)
    {
        var entry = _clipboard.Current;
        if (entry is null)
            return;

        // Pegar una carpeta dentro de si misma crearia una recursion infinita.
        var selfPaste = entry.Paths.FirstOrDefault(p => Directory.Exists(p) && _files.IsInside(p, _currentPath));
        if (selfPaste is not null)
        {
            await DisplayAlert(_l["Error"], _l["ErrorPasteIntoItself"], _l["Ok"]);
            return;
        }

        var resolution = ConflictResolution.KeepBoth;
        var conflicts = _files.GetConflicts(entry.Paths, _currentPath);

        if (conflicts.Count > 0)
        {
            var answer = await DisplayActionSheet(
                string.Format(_l.CurrentCulture, _l["OverwriteConfirm"], string.Join(", ", conflicts)),
                _l["Cancel"],
                null,
                _l["Replace"], _l["KeepBoth"]);

            if (answer == _l["Replace"])
                resolution = ConflictResolution.Replace;
            else if (answer != _l["KeepBoth"])
                return;
        }

        SetBusy(true);
        try
        {
            var result = await _files.PasteAsync(entry, _currentPath, resolution);

            // Tras mover, el portapapeles ya no apunta a rutas validas.
            if (entry.IsMove)
                _clipboard.Clear();

            await LoadAsync(_currentPath);

            if (result.HasErrors)
            {
                await DisplayAlert(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorPaste"], string.Join("\n", result.Errors)), _l["Ok"]);
            }
            else
            {
                var message = string.Format(_l.CurrentCulture, entry.IsMove ? _l["Moved"] : _l["Pasted"], result.Succeeded);
                _toast.Show(message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paste failed");
            await DisplayAlert(_l["Error"], string.Format(_l.CurrentCulture, _l["ErrorPaste"], ex.Message), _l["Ok"]);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ============ Menu general ============

    private async void OnMoreClicked(object? sender, EventArgs e)
    {
        var hidden = _settings.ShowHiddenFiles ? _l["ShowHiddenOff"] : _l["ShowHiddenOn"];
        var choice = await DisplayActionSheet(
            _l["More"],
            _l["Cancel"],
            null,
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
            await LoadAsync(_currentPath);
        }
        else if (choice == _l["Refresh"])
        {
            await LoadAsync(_currentPath);
        }
        else if (choice == _l["Settings"])
        {
            await Navigation.PushAsync(ServiceHelper.GetRequiredService<SettingsPage>());
        }
        else if (choice == _l["About"])
        {
            await Navigation.PushAsync(ServiceHelper.GetRequiredService<AboutPage>());
        }
    }

    private async Task ShowSortMenuAsync()
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

        var choice = await DisplayActionSheet(_l["Sort"], _l["Cancel"], null, options.Keys.ToArray());

        if (choice is not null && options.TryGetValue(choice, out var mode))
        {
            _settings.Sort = mode;
            await LoadAsync(_currentPath);
        }
    }
}
