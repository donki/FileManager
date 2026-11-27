using System.Reflection;
using FileManager.Helpers;
using FileManager.Models;
using FileManager.Pages;
using FileManager.ViewModels;

namespace FileManager.Tests;

/// <summary>
/// La pantalla principal de verdad (XAML y code-behind) sobre una carpeta temporal: que vuelca el
/// estado de su logica en los controles y que cada toque llega a la logica.
/// </summary>
public sealed class MainPageTests : PageHost
{
    private MainPage NewPage() => ServiceHelper.GetRequiredService<MainPage>();

    private static MainViewModel Vm(MainPage page) =>
        (MainViewModel)typeof(MainPage).GetField("_vm", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;

    private static async Task AppearAsync(MainPage page)
    {
        Invoke(page, "OnAppearing");
        await Until(() => !Vm(page).IsBusy && Vm(page).RecentTask.IsCompleted, "la carga inicial");
        await Vm(page).RecentTask;
        await Settle();
    }

    /// <summary>Crea la fila de la lista para un elemento, como la crearia el CollectionView.</summary>
    private static View Row(MainPage page, FileItem item)
    {
        var row = (View)Named<CollectionView>(page, "FilesView").ItemTemplate.CreateContent();
        row.BindingContext = item;
        return row;
    }

    private static ItemTouchBehavior Touch(View row) =>
        row.GetVisualTreeDescendants().OfType<View>().SelectMany(v => v.Behaviors).OfType<ItemTouchBehavior>().Single();

    private static FileItem Item(MainPage page, string name) => Vm(page).Items.Single(i => i.Name == name);

    [Fact]
    public async Task Al_aparecer_vuelca_la_raiz_textos_migas_y_recientes()
    {
        Tmp.File("nota.txt");
        Tmp.Dir("Musica");
        var page = NewPage();

        await AppearAsync(page);
        var vm = Vm(page);

        Assert.Equal(vm.Title, Named<Label>(page, "TitleLabel").Text);
        Assert.Equal(vm.Subtitle, Named<Label>(page, "SubtitleLabel").Text);
        Assert.False(Named<ImageButton>(page, "UpButton").IsVisible);
        Assert.True(Named<CollectionView>(page, "FilesView").IsVisible);
        Assert.Same(vm.Items, Named<CollectionView>(page, "FilesView").ItemsSource);
        Assert.False(Named<View>(page, "PermissionView").IsVisible);
        Assert.False(Named<View>(page, "SelectionBar").IsVisible);
        Assert.False(Named<View>(page, "PasteBar").IsVisible);
        Assert.False(Named<ActivityIndicator>(page, "Busy").IsRunning);
        Assert.False(Named<SearchBar>(page, "FileSearchBar").IsVisible);
        Assert.False(Named<ImageButton>(page, "StorageButton").IsVisible);

        // Textos fijos: etiquetas, botones, buscador y la descripcion de los botones de solo icono.
        Assert.Equal(L("PermissionGrant"), Named<Button>(page, "PermissionButton").Text);
        Assert.Equal(L("SearchPlaceholder"), Named<SearchBar>(page, "FileSearchBar").Placeholder);
        Assert.Equal(L("SelectAll"), SemanticProperties.GetDescription(Named<ImageButton>(page, "SelectAllButton")));
        Assert.Equal(L("RecentFiles"), Named<Label>(page, "RecentTitleLabel").Text);

        // En la raiz, la franja de recientes va en la cabecera de la lista, con sus tarjetas.
        Assert.True(Named<View>(page, "RecentSection").IsVisible);
        Assert.Same(Named<View>(page, "RecentSection"), Named<CollectionView>(page, "FilesView").Header);
        Assert.Single(Named<HorizontalStackLayout>(page, "RecentBar").Children);

        // Migas: solo la memoria, en negrita.
        var crumbs = Named<HorizontalStackLayout>(page, "BreadcrumbBar").Children.OfType<Button>().ToList();
        Assert.Single(crumbs);
        Assert.Equal(FontAttributes.Bold, crumbs[0].FontAttributes);
    }

    [Fact]
    public async Task Tocar_una_carpeta_entra_y_las_migas_y_subir_vuelven()
    {
        Tmp.File("Docs/a.txt");
        var page = NewPage();
        await AppearAsync(page);

        Touch(Row(page, Item(page, "Docs"))).RaiseTapped();
        await Until(() => Vm(page).CurrentPath == Tmp.Combine("Docs"), "entrar en Docs");
        await Settle();

        Assert.True(Named<ImageButton>(page, "UpButton").IsVisible);
        // Fuera de la raiz no hay cabecera (en Android dejaba un hueco).
        Assert.Null(Named<CollectionView>(page, "FilesView").Header);
        var bar = Named<HorizontalStackLayout>(page, "BreadcrumbBar");
        Assert.Equal(3, bar.Children.Count);
        Assert.Equal("›", ((Label)bar.Children[1]).Text);
        var root = (Button)bar.Children[0];
        Assert.Equal(FontAttributes.None, root.FontAttributes);

        // La miga de la raiz vuelve a la raiz.
        ((IButtonController)root).SendClicked();
        await Until(() => Vm(page).CurrentPath == Tmp.Path, "volver por la miga");

        // Y subir desde una carpeta tambien.
        await Vm(page).LoadAsync(Tmp.Combine("Docs"));
        Click(page, "UpButton");
        await Until(() => Vm(page).CurrentPath == Tmp.Path, "subir");
    }

    [Fact]
    public async Task Tocar_un_fichero_lo_abre()
    {
        var file = Tmp.File("foto.jpg");
        var page = NewPage();
        await AppearAsync(page);

        Touch(Row(page, Item(page, "foto.jpg"))).RaiseTapped();
        await Until(() => Actions.Opened.Count == 1, "abrir el fichero");

        Assert.Equal(file, Actions.Opened[0]);
    }

    [Fact]
    public async Task Pulsacion_larga_selecciona_y_la_barra_de_seleccion_responde()
    {
        Tmp.File("a.txt");
        Tmp.File("b.txt");
        var page = NewPage();
        await AppearAsync(page);

        Touch(Row(page, Item(page, "a.txt"))).RaiseLongPressed();

        Assert.True(Named<View>(page, "SelectionBar").IsVisible);
        Assert.Equal(Vm(page).SelectionCountText, Named<Label>(page, "SelCountLabel").Text);
        Assert.True(Item(page, "a.txt").IsSelected);

        Click(page, "SelectAllButton");
        Assert.True(Item(page, "b.txt").IsSelected);

        // Compartir la seleccion.
        Click(page, "SelShareButton");
        await Until(() => Actions.Shared.Count == 1, "compartir");
        Assert.Equal(2, Actions.Shared[0].Paths.Length);
        Assert.False(Named<View>(page, "SelectionBar").IsVisible);

        // Acciones por lotes: copiar deja la barra de pegar.
        Touch(Row(page, Item(page, "b.txt"))).RaiseLongPressed();
        Click(page, "SelActionsButton");
        await AnswerDialog(page, L("Copy"));
        Assert.True(Named<View>(page, "PasteBar").IsVisible);
        Assert.Contains("b.txt", Named<Label>(page, "PasteLabel").Text);

        // Cerrar la seleccion con su boton.
        Touch(Row(page, Item(page, "a.txt"))).RaiseLongPressed();
        Click(page, "SelCloseButton");
        Assert.False(Named<View>(page, "SelectionBar").IsVisible);
    }

    [Fact]
    public async Task Atras_sale_de_la_seleccion_sube_de_carpeta_y_en_la_raiz_lo_deja_al_sistema()
    {
        Tmp.File("Docs/a.txt");
        var page = NewPage();
        await AppearAsync(page);

        // En la raiz, sin nada que deshacer, decide el sistema.
        Assert.False((bool)Invoke(page, "OnBackButtonPressed")!);

        Touch(Row(page, Item(page, "Docs"))).RaiseLongPressed();
        Assert.True((bool)Invoke(page, "OnBackButtonPressed")!);
        Assert.False(Vm(page).SelectMode);

        await Vm(page).LoadAsync(Tmp.Combine("Docs"));
        Assert.True((bool)Invoke(page, "OnBackButtonPressed")!);
        await Until(() => Vm(page).CurrentPath == Tmp.Path, "subir con atras");
    }

    [Fact]
    public async Task Menu_de_un_elemento_copiar_y_pegar_en_otra_carpeta()
    {
        Tmp.File("a.txt", "hola");
        Tmp.Dir("Destino");
        var page = NewPage();
        await AppearAsync(page);

        var menu = Row(page, Item(page, "a.txt")).GetVisualTreeDescendants().OfType<Button>().Single(b => b.Text == "⋮");
        ((IButtonController)menu).SendClicked();
        var text = await AnswerDialog(page, L("Copy"));

        Assert.Contains("a.txt", text);
        Assert.True(Named<View>(page, "PasteBar").IsVisible);
        Assert.Equal(L("Paste"), Named<Button>(page, "PasteConfirmButton").Text);

        await Vm(page).LoadAsync(Tmp.Combine("Destino"));
        Click(page, "PasteConfirmButton");
        await Until(() => File.Exists(Tmp.Combine("Destino", "a.txt")), "pegar");
        Assert.Equal("hola", File.ReadAllText(Tmp.Combine("Destino", "a.txt")));
    }

    [Fact]
    public async Task Cancelar_el_pegado_quita_la_barra()
    {
        var file = Tmp.File("a.txt");
        var page = NewPage();
        await AppearAsync(page);

        // El portapapeles avisa y la pagina pinta la barra (por su Dispatcher).
        Clipboard.Set(new[] { file }, isMove: true);
        Assert.True(Named<View>(page, "PasteBar").IsVisible);
        Assert.StartsWith(L("Cut"), Named<Label>(page, "PasteLabel").Text);

        Click(page, "PasteCancelButton");
        Assert.False(Named<View>(page, "PasteBar").IsVisible);
    }

    [Fact]
    public async Task Buscar_muestra_el_buscador_filtra_y_al_cerrarlo_se_vacia()
    {
        Tmp.File("Docs/informe.pdf");
        Tmp.File("otro.txt");
        var page = NewPage();
        await AppearAsync(page);

        Click(page, "SearchButton");
        await Settle();
        var bar = Named<SearchBar>(page, "FileSearchBar");
        Assert.True(bar.IsVisible);

        bar.Text = "informe";
        await Until(() => Vm(page).Items.Count == 1 && Vm(page).Items[0].Name == "informe.pdf", "resultados de la busqueda");

        Click(page, "SearchButton");
        await Until(() => !bar.IsVisible, "cerrar el buscador");
        await Until(() => string.IsNullOrEmpty(bar.Text), "vaciar el buscador");
    }

    [Fact]
    public async Task Nueva_carpeta_pide_el_nombre_y_la_crea()
    {
        var page = NewPage();
        await AppearAsync(page);

        Click(page, "NewFolderButton");
        await AnswerPrompt(page, "Nueva", L("Save"));

        await Until(() => Directory.Exists(Tmp.Combine("Nueva")), "crear la carpeta");
        await Until(() => Toast.Shown.Contains(L("FolderCreated")), "avisar");
    }

    [Fact]
    public async Task Carpeta_vacia_muestra_su_aviso()
    {
        Tmp.Dir("Vacia");
        var page = NewPage();
        await AppearAsync(page);

        await Vm(page).LoadAsync(Tmp.Combine("Vacia"));

        Assert.True(Named<View>(page, "EmptyView").IsVisible);
        Assert.Equal(L("EmptyFolder"), Named<Label>(page, "EmptyTitleLabel").Text);
        Assert.True(Named<Label>(page, "EmptyHintLabel").IsVisible);
        Assert.Equal(L("EmptyFolderHint"), Named<Label>(page, "EmptyHintLabel").Text);
    }

    [Fact]
    public async Task Sin_permiso_pide_acceso_y_al_volver_de_ajustes_lista()
    {
        Tmp.File("a.txt");
        Permissions.HasFullAccess = false;
        var page = NewPage();
        Invoke(page, "OnAppearing");
        await Settle();

        Assert.True(Named<View>(page, "PermissionView").IsVisible);
        Assert.False(Named<CollectionView>(page, "FilesView").IsVisible);
        Assert.Equal(L("PermissionTitle"), Named<Label>(page, "PermissionTitleLabel").Text);

        Click(page, "PermissionButton");
        await Until(() => Permissions.Requests == 1, "pedir el permiso");

        // Android reanuda la ventana al volver de los ajustes del sistema.
        Permissions.HasFullAccess = true;
        Invoke(page, "OnWindowResumed", null, EventArgs.Empty);
        await Until(() => Named<CollectionView>(page, "FilesView").IsVisible && Vm(page).Items.Count == 1, "listar tras conceder");
        Assert.Contains(L("PermissionGranted"), Toast.Shown);

        Invoke(page, "OnDisappearing");
    }

    [Fact]
    public async Task Con_ventana_escucha_la_reanudacion_y_la_suelta_al_irse()
    {
        var page = NewPage();
        var window = new Window(page);

        await AppearAsync(page);
        Invoke(page, "OnDisappearing");

        Assert.Same(window, page.Window);
    }

    [Fact]
    public async Task Con_tarjeta_SD_el_boton_de_memoria_cambia_de_memoria()
    {
        var sd = Tmp.Dir("sd");
        Files.RemovableRoots.Add(sd);
        var page = NewPage();
        await AppearAsync(page);

        Assert.True(Named<ImageButton>(page, "StorageButton").IsVisible);

        Click(page, "StorageButton");
        var sdLabel = Vm(page).VolumeLabel(new StorageVolume(sd, true));
        await AnswerDialog(page, sdLabel);

        await Until(() => Vm(page).CurrentPath == sd, "ir a la tarjeta");
        // Atras en la raiz de la tarjeta vuelve a la memoria interna.
        Assert.True((bool)Invoke(page, "OnBackButtonPressed")!);
        await Until(() => Vm(page).CurrentPath == Tmp.Path, "volver a la interna");
    }

    [Fact]
    public async Task Recientes_tocar_abre_y_mantener_va_a_su_carpeta()
    {
        var file = Tmp.File("Fotos/playa.jpg");
        var page = NewPage();
        await AppearAsync(page);

        var card = (View)Named<HorizontalStackLayout>(page, "RecentBar").Children.Single();
        var touch = Touch(card);

        touch.RaiseTapped();
        await Until(() => Actions.Opened.Contains(file), "abrir el reciente");

        touch.RaiseLongPressed();
        await Until(() => Vm(page).CurrentPath == Tmp.Combine("Fotos"), "ir a la carpeta del reciente");
    }

    [Fact]
    public async Task Las_fotos_de_la_lista_cambian_el_icono_por_su_miniatura_al_enlazar_la_fila()
    {
        Tmp.File("Fotos/playa.jpg");
        Tmp.File("Fotos/clip.mp4");
        Tmp.File("Fotos/nota.txt");
        var page = NewPage();
        await AppearAsync(page);
        await Vm(page).LoadAsync(Tmp.Combine("Fotos"));

        var photo = Item(page, "playa.jpg");
        var video = Item(page, "clip.mp4");
        var note = Item(page, "nota.txt");
        var photoRow = Row(page, photo);
        var videoRow = Row(page, video);
        Row(page, note);

        await Until(() => photo.HasThumbnail && video.HasThumbnail, "las miniaturas de la lista");
        // Llegan por el hilo de la interfaz: el servicio entrega por el Dispatcher de la pagina.
        var dispatcher = Assert.IsType<TestDispatcher>(page.Dispatcher);
        var before = dispatcher.Dispatched;
        var ran = false;
        Thumbs.Post(() => ran = true);
        Assert.True(ran);
        Assert.Equal(before + 1, dispatcher.Dispatched);
        Assert.True(note.ShowIcon);
        Assert.Equal(new[] { false, true, false }, VisibleBorders(photoRow));
        Assert.Equal(new[] { false, true, true }, VisibleBorders(videoRow));

        // La fila se recicla para la nota: suelta la foto y la nota sigue con su icono.
        photoRow.BindingContext = note;
        await Settle();
        Assert.True(note.ShowIcon);
    }

    /// <summary>Visibilidad de icono, miniatura y marca de video (las pastillas con imagen; la casilla de seleccion lleva texto).</summary>
    private static bool[] VisibleBorders(View view) =>
        view.GetVisualTreeDescendants().OfType<Border>().Where(b => b.Content is Image).Select(b => b.IsVisible).ToArray();

    [Fact]
    public async Task Los_recientes_tambien_llevan_miniatura_y_con_el_interruptor_apagado_no()
    {
        Tmp.File("Fotos/playa.jpg");
        var page = NewPage();
        await AppearAsync(page);

        var card = (View)Named<HorizontalStackLayout>(page, "RecentBar").Children.Single();
        var recent = (FileItem)card.BindingContext;
        await Until(() => recent.HasThumbnail, "la miniatura del reciente");
        Assert.Equal(new[] { false, true, false }, VisibleBorders(card));

        Settings.ShowThumbnails = false;
        var other = new FileItem { Name = "otra.jpg", FullPath = Tmp.File("otra.jpg"), Modified = DateTime.Now };
        Row(page, other);
        await Settle();
        Assert.True(other.ShowIcon);
    }

    [Fact]
    public async Task Toques_sin_elemento_no_hacen_nada()
    {
        var page = NewPage();
        await AppearAsync(page);
        var path = Vm(page).CurrentPath;

        foreach (var handler in new[] { "OnItemTapped", "OnItemLongPressed", "OnRecentTapped", "OnRecentLongPressed", "OnItemMenuClicked" })
            Invoke(page, handler, new Label(), EventArgs.Empty);
        await Settle();

        Assert.False(Vm(page).SelectMode);
        Assert.False(DialogOpen(page));
        Assert.Equal(path, Vm(page).CurrentPath);
        Assert.Empty(Actions.Opened);
    }

    [Fact]
    public async Task Menu_mas_abre_configuracion_y_acerca_de_apiladas()
    {
        var page = NewPage();
        var nav = new NavigationPage(page);
        await AppearAsync(page);

        Click(page, "MoreButton");
        await AnswerDialog(page, L("Settings"));
        await Until(() => nav.Navigation.NavigationStack.Count == 2, "abrir Configuracion");
        Assert.IsType<SettingsPage>(nav.CurrentPage);

        await nav.PopAsync(false);
        Click(page, "MoreButton");
        await AnswerDialog(page, L("About"));
        await Until(() => nav.Navigation.NavigationStack.Count == 2, "abrir Acerca de");
        Assert.IsType<AboutPage>(nav.CurrentPage);
    }

    [Fact]
    public async Task El_boton_de_menu_sin_Shell_no_falla()
    {
        var page = NewPage();
        await AppearAsync(page);

        Click(page, "MenuButton");

        Assert.Null(Shell.Current);
    }

    [Fact]
    public async Task Cambiar_de_idioma_vuelve_a_poner_los_textos()
    {
        var page = NewPage();
        await AppearAsync(page);

        Loc.SetLanguage("es");
        await Settle();

        Assert.Equal(L("PermissionGrant"), Named<Button>(page, "PermissionButton").Text);
        Assert.Equal("es", Loc.CurrentLanguage);
        Assert.Equal(Vm(page).Title, Named<Label>(page, "TitleLabel").Text);
    }
}
