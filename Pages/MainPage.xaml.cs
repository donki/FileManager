using FileManager.Helpers;
using FileManager.Models;
using FileManager.Services;
using FileManager.ViewModels;
using Microsoft.Extensions.Logging;

namespace FileManager.Pages;

/// <summary>
/// Pantalla principal: enlace fino entre los controles y <see cref="MainViewModel"/>, que tiene toda
/// la logica. Aqui solo se vuelca su estado en los controles y se le pasan los toques.
/// </summary>
public partial class MainPage : ContentPage, IMainView
{
    private readonly ILocalizationService _l;
    private readonly IDialogService _dialogs;
    private readonly UpdateService _update;
    private readonly MainViewModel _vm;
    private IReadOnlyList<Crumb>? _drawnBreadcrumb;

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

        _l = localization;
        _update = update;
        _dialogs = new PageDialogService(this);
        _vm = new MainViewModel(files, clipboard, actions, settings, localization, permissions, toast, _dialogs, this, logger);

        FilesView.ItemsSource = _vm.Items;
        _vm.Changed += (_, _) => Render();
        clipboard.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(_vm.UpdatePasteBar);
        _l.LanguageChanged += (_, _) => MainThread.BeginInvokeOnMainThread(async () =>
        {
            PageTexts.Apply(this, MainViewModel.StaticTexts, _l);
            await _vm.LanguageChangedAsync();
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Conceder el permiso ocurre en otra Activity del sistema, y al volver de ella Android reanuda
        // la ventana sin disparar OnAppearing: sin escuchar Resumed la pantalla de permisos se quedaria
        // puesta con el acceso ya concedido.
        if (Window is not null)
        {
            Window.Resumed -= OnWindowResumed;
            Window.Resumed += OnWindowResumed;
        }

        PageTexts.Apply(this, MainViewModel.StaticTexts, _l);

        // Comprobacion de version al arrancar (constitucion 15): no bloqueante y silenciosa si ya se
        // esta al dia. El propio servicio solo comprueba una vez por sesion.
        _ = _update.CheckAndPromptAsync(_dialogs);

        await _vm.AppearingAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (Window is not null)
            Window.Resumed -= OnWindowResumed;
    }

    private void OnWindowResumed(object? sender, EventArgs e) =>
        Dispatcher.Dispatch(async () => await _vm.RefreshAccessAsync());

    /// <summary>Vuelca el estado de la logica en los controles.</summary>
    private void Render()
    {
        TitleLabel.Text = _vm.Title;
        SubtitleLabel.Text = _vm.Subtitle;
        UpButton.IsVisible = _vm.UpVisible;
        PermissionView.IsVisible = _vm.PermissionVisible;
        FilesView.IsVisible = _vm.FilesVisible;
        EmptyView.IsVisible = _vm.EmptyVisible;
        EmptyIconImage.Source = _vm.EmptyIcon;
        EmptyTitleLabel.Text = _vm.EmptyTitle;
        EmptyHintLabel.IsVisible = _vm.EmptyHintVisible;
        NewFolderButton.IsVisible = _vm.NewFolderVisible;
        SearchButton.IsVisible = _vm.SearchButtonVisible;
        SelectionBar.IsVisible = _vm.SelectionBarVisible;
        SelCountLabel.Text = _vm.SelectionCountText;
        PasteBar.IsVisible = _vm.PasteBarVisible;
        PasteLabel.Text = _vm.PasteText;
        Busy.IsVisible = Busy.IsRunning = _vm.IsBusy;
        FileSearchBar.IsVisible = _vm.SearchBarVisible;

        if (!ReferenceEquals(_drawnBreadcrumb, _vm.Breadcrumb))
            DrawBreadcrumb(_drawnBreadcrumb = _vm.Breadcrumb);

        // Lo ultimo: vaciar el buscador dispara su TextChanged, que vuelve a la logica.
        if (string.IsNullOrEmpty(_vm.SearchText) && !string.IsNullOrEmpty(FileSearchBar.Text))
            FileSearchBar.Text = string.Empty;
    }

    private void DrawBreadcrumb(IReadOnlyList<Crumb> crumbs)
    {
        BreadcrumbBar.Clear();
        foreach (var crumb in crumbs)
        {
            if (BreadcrumbBar.Count > 0)
                BreadcrumbBar.Add(new Label { Text = "›", TextColor = Color.FromArgb("#8FB4D8"), FontSize = 14, VerticalOptions = LayoutOptions.Center });

            var button = new Button
            {
                Text = crumb.Label,
                FontSize = 13,
                FontAttributes = crumb.IsLast ? FontAttributes.Bold : FontAttributes.None,
                TextColor = crumb.IsLast ? Colors.White : Color.FromArgb("#CFE0F0"),
                BackgroundColor = Colors.Transparent,
                Padding = new Thickness(6, 2),
                HeightRequest = 30,
                MinimumWidthRequest = 0
            };
            if (!crumb.IsLast)
                button.Clicked += async (_, _) => await _vm.LoadAsync(crumb.Path);
            BreadcrumbBar.Add(button);
        }

        // La carpeta actual queda a la derecha del todo: se desplaza para que se vea.
        Dispatcher.Dispatch(async () => await BreadcrumbScroll.ScrollToAsync(BreadcrumbBar, ScrollToPosition.End, false));
    }

    protected override bool OnBackButtonPressed()
    {
        var action = _vm.BackAction();
        if (action is null)
            return base.OnBackButtonPressed();

        Dispatcher.Dispatch(async () => await action());
        return true;
    }

    // ============ IMainView ============

    public void FocusSearch() => FileSearchBar.Focus();

    public void UnfocusSearch() => FileSearchBar.Unfocus();

    public Task OpenSettingsAsync() => Navigation.PushAsync(ServiceHelper.GetRequiredService<SettingsPage>());

    public Task OpenAboutAsync() => Navigation.PushAsync(ServiceHelper.GetRequiredService<AboutPage>());

    // ============ Toques ============

    private static FileItem? ItemOf(object? sender) => (sender as BindableObject)?.BindingContext as FileItem;

    private void OnMenuClicked(object? sender, EventArgs e)
    {
        // Abre el menu hamburguesa (flyout de Shell): navegacion de primer nivel (constitucion A.9).
        if (Shell.Current is not null)
            Shell.Current.FlyoutIsPresented = true;
    }

    private async void OnRequestPermissionClicked(object? sender, EventArgs e) => await _vm.RequestPermissionAsync();

    private async void OnUpClicked(object? sender, EventArgs e) => await _vm.NavigateUpAsync();

    // El gesto lo aporta ItemTouchBehavior (View.Click/LongClick de Android); MAUI no trae ninguno.
    private void OnItemLongPressed(object? sender, EventArgs e)
    {
        if (ItemOf(sender) is { } item)
            _vm.ItemLongPressed(item);
    }

    private async void OnItemTapped(object? sender, EventArgs e)
    {
        if (ItemOf(sender) is { } item)
            await _vm.ItemTappedAsync(item);
    }

    private async void OnItemMenuClicked(object? sender, EventArgs e)
    {
        if (ItemOf(sender) is { } item)
            await _vm.ItemMenuAsync(item);
    }

    private async void OnSearchToggleClicked(object? sender, EventArgs e) => await _vm.ToggleSearchAsync();

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e) =>
        await _vm.SearchTextChangedAsync(e.NewTextValue);

    private void OnSelectionCloseClicked(object? sender, EventArgs e) => _vm.ExitSelectionMode();

    private void OnSelectAllClicked(object? sender, EventArgs e) => _vm.SelectAll();

    private async void OnBatchActionsClicked(object? sender, EventArgs e) => await _vm.BatchActionsAsync();

    private async void OnNewFolderClicked(object? sender, EventArgs e) => await _vm.NewFolderAsync();

    private void OnCancelPasteClicked(object? sender, EventArgs e) => _vm.CancelPaste();

    private async void OnPasteClicked(object? sender, EventArgs e) => await _vm.PasteAsync();

    private async void OnMoreClicked(object? sender, EventArgs e) => await _vm.MoreMenuAsync();
}
