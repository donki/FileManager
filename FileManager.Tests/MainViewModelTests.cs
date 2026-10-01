using System.Globalization;
using FileManager.Models;
using FileManager.Services;
using FileManager.Tests.Fakes;
using FileManager.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileManager.Tests;

/// <summary>
/// Logica de la pantalla principal sobre una carpeta temporal real que hace de almacenamiento
/// interno, con dialogos que contestan solos.
/// </summary>
public sealed class MainViewModelTests : IDisposable
{
    private readonly CultureInfo? _culture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? _uiCulture = CultureInfo.DefaultThreadCurrentUICulture;
    private readonly TempFolder _tmp = new();
    private readonly RootedFileSystem _fs;
    private readonly FileClipboardService _clipboard = new();
    private readonly FakeActions _actions = new();
    private readonly MemorySettings _settings = new();
    private readonly LocalizationService _l;
    private readonly FakePermissions _permissions = new();
    private readonly FakeToast _toast = new();
    private readonly ScriptedDialogs _dialogs = new();
    private readonly FakeView _view = new();
    private readonly MainViewModel _vm;
    private int _changes;

    public MainViewModelTests()
    {
        _fs = new RootedFileSystem(_tmp.Path);
        _l = new LocalizationService(_settings, NullLogger<LocalizationService>.Instance);
        _vm = new MainViewModel(_fs, _clipboard, _actions, _settings, _l, _permissions, _toast, _dialogs, _view, NullLogger.Instance);
        _vm.Changed += (_, _) => _changes++;
    }

    public void Dispose()
    {
        _tmp.Dispose();
        CultureInfo.DefaultThreadCurrentCulture = _culture;
        CultureInfo.DefaultThreadCurrentUICulture = _uiCulture;
    }

    private string L(string key) => _l[key];

    private FileItem Item(string name) => _vm.Items.Single(i => i.Name == name);

    private async Task OpenRootAsync()
    {
        await _vm.AppearingAsync();
        Assert.Equal(_tmp.Path, _vm.CurrentPath);
    }

    // ---------- Arranque y permiso ----------

    [Fact]
    public void StartsAtTheRoot_WithTheEmptyFolderTitle()
    {
        Assert.Equal(_tmp.Path, _vm.CurrentPath);
        Assert.Equal(L("EmptyFolder"), _vm.EmptyTitle);
        Assert.True(_vm.FilesVisible);
        Assert.False(_vm.PermissionVisible);
    }

    [Fact]
    public async Task Appearing_WithoutPermission_ShowsTheGate_AndDoesNotList()
    {
        _permissions.HasFullAccess = false;
        _tmp.File("a.txt");

        await _vm.AppearingAsync();

        Assert.True(_vm.PermissionVisible);
        Assert.False(_vm.FilesVisible);
        Assert.False(_vm.EmptyVisible);
        Assert.False(_vm.NewFolderVisible);
        Assert.False(_vm.PasteBarVisible);
        Assert.Equal(0, _fs.ListCalls);
        Assert.Empty(_vm.Items);
    }

    [Fact]
    public async Task Appearing_WithPermission_ListsTheRoot()
    {
        _tmp.File("b.txt", "hello");
        _tmp.Dir("Music");

        await OpenRootAsync();

        Assert.Equal(new[] { "Music", "b.txt" }, _vm.Items.Select(i => i.Name));
        Assert.Equal(L("InternalStorage"), _vm.Title);
        Assert.False(_vm.UpVisible);
        Assert.Equal(string.Format(L("ItemsCount"), 2), _vm.Subtitle);
        Assert.False(_vm.EmptyVisible);
        Assert.False(_vm.IsBusy);
        Assert.True(_changes > 0);
        Assert.Equal("ic_folder_open.png", _vm.EmptyIcon);
        Assert.Single(_vm.Breadcrumb);
        Assert.True(_vm.Breadcrumb[0].IsLast);
    }

    [Fact]
    public async Task EmptyFolder_ShowsEmptyView_AndNoSubtitle()
    {
        await OpenRootAsync();

        Assert.True(_vm.EmptyVisible);
        Assert.Equal(string.Empty, _vm.Subtitle);
        Assert.True(_vm.EmptyHintVisible);
    }

    [Fact]
    public async Task OneItem_UsesTheSingularSubtitle()
    {
        _tmp.File("a.txt");
        await OpenRootAsync();
        Assert.Equal(L("OneItem"), _vm.Subtitle);
    }

    [Fact]
    public async Task PermissionGranted_AfterRequest_ShowsAToast_Once()
    {
        _permissions.HasFullAccess = false;
        await _vm.AppearingAsync();

        await _vm.RequestPermissionAsync();
        Assert.True(_vm.AwaitingPermission);
        Assert.Equal(1, _permissions.Requests);

        _permissions.HasFullAccess = true;
        await _vm.RefreshAccessAsync();
        await _vm.RefreshAccessAsync();

        Assert.Equal(new[] { L("PermissionGranted") }, _toast.Shown);
        Assert.False(_vm.PermissionVisible);
        Assert.True(_vm.FilesVisible);
    }

    [Fact]
    public async Task RequestPermission_Failing_ShowsTheError_AndStopsWaiting()
    {
        _permissions.RequestError = new InvalidOperationException("no settings screen");

        await _vm.RequestPermissionAsync();

        Assert.False(_vm.AwaitingPermission);
        Assert.Equal("no settings screen", _dialogs.Last.Message);
    }

    [Fact]
    public async Task RefreshAccess_DuringASearch_KeepsTheResults()
    {
        _tmp.File("x/report.txt");
        await OpenRootAsync();
        await _vm.ToggleSearchAsync();
        await _vm.SearchTextChangedAsync("report", debounceMs: 0);
        var lists = _fs.ListCalls;

        await _vm.RefreshAccessAsync();

        Assert.Equal(lists, _fs.ListCalls);
        Assert.Equal("report.txt", Assert.Single(_vm.Items).Name);
    }

    [Fact]
    public async Task LanguageChanged_TranslatesTheHeaderAndReloads()
    {
        _tmp.File("a.txt");
        await OpenRootAsync();
        var lists = _fs.ListCalls;

        _l.SetLanguage("es");
        await _vm.LanguageChangedAsync();

        Assert.Equal("Almacenamiento interno", _vm.Title);
        Assert.Equal(L("EmptyFolder"), _vm.EmptyTitle);
        Assert.Equal(lists + 1, _fs.ListCalls);
    }

    [Fact]
    public async Task LanguageChanged_WithoutPermission_DoesNotList()
    {
        _permissions.HasFullAccess = false;
        await _vm.LanguageChangedAsync();
        Assert.Equal(0, _fs.ListCalls);
    }

    // ---------- Listado ----------

    [Fact]
    public async Task Load_MissingFolder_FallsBackToTheRoot()
    {
        await _vm.LoadAsync(_tmp.Combine("gone"));
        Assert.Equal(_tmp.Path, _vm.CurrentPath);
    }

    [Fact]
    public async Task Load_AccessDenied_WarnsAndGoesUp()
    {
        var sub = _tmp.Dir("locked");
        _fs.ListError = new UnauthorizedAccessException();

        await _vm.LoadAsync(sub);

        Assert.Contains(_dialogs.Messages, m => m == string.Format(L("ErrorAccessDenied"), sub));
        Assert.False(_vm.IsBusy);
    }

    [Fact]
    public async Task Load_IOError_ShowsItsMessage()
    {
        _fs.ListError = new IOException("disk gone");

        await _vm.LoadAsync(_tmp.Path);

        Assert.Equal(string.Format(L("ErrorReadFolder"), "disk gone"), _dialogs.Last.Message);
        Assert.False(_vm.IsBusy);
    }

    [Fact]
    public async Task Load_OtherErrors_AreNotSwallowed()
    {
        _fs.ListError = new InvalidOperationException("bug");
        await Assert.ThrowsAsync<InvalidOperationException>(() => _vm.LoadAsync(_tmp.Path));
        Assert.False(_vm.IsBusy);
    }

    [Fact]
    public async Task Items_GetIconAndDetails()
    {
        _tmp.File("photo.jpg", "12345");
        _tmp.File("Docs/a.txt");
        _tmp.Dir("Empty");
        await OpenRootAsync();

        Assert.Equal("ic_file_image.png", Item("photo.jpg").Icon);
        Assert.EndsWith("5 B", Item("photo.jpg").Details);
        Assert.EndsWith(L("FolderOneItem"), Item("Docs").Details);
        Assert.EndsWith(L("FolderEmpty"), Item("Empty").Details);
    }

    [Fact]
    public void DescribeItem_FolderWithSeveralItems_AndUnreadableFolder()
    {
        var dir = _tmp.Dir("Many");
        _tmp.File("Many/a");
        _tmp.File("Many/b");
        var modified = new DateTime(2026, 1, 2, 3, 4, 0);

        var many = _vm.DescribeItem(new FileItem { Name = "Many", FullPath = dir, IsDirectory = true, Modified = modified });
        var missing = _vm.DescribeItem(new FileItem { Name = "Nope", FullPath = _tmp.Combine("nope"), IsDirectory = true, Modified = modified });

        Assert.Equal($"2 Jan 2026 03:04  ·  {string.Format(L("FolderItems"), 2)}", many);
        Assert.Equal("2 Jan 2026 03:04", missing);
    }

    // ---------- Filtro ----------

    [Fact]
    public async Task Filter_KeepsFoldersAndTheChosenType()
    {
        _tmp.File("a.jpg");
        _tmp.File("b.mp3");
        _tmp.Dir("Folder");
        await OpenRootAsync();

        _dialogs.Answer(L("FilterImages"));
        await _vm.ShowFilterMenuAsync();

        Assert.Equal(FileCategory.Image, _vm.TypeFilter);
        Assert.Equal(new[] { "Folder", "a.jpg" }, _vm.Items.Select(i => i.Name));
        Assert.Equal($"{string.Format(L("ItemsCount"), 2)}  ·  {L("FilterImages")}", _vm.Subtitle);
        Assert.Equal(8, _dialogs.Last.Options.Length);
    }

    [Fact]
    public async Task Filter_WithNothingLeft_ShowsOnlyTheFilterName()
    {
        _tmp.File("b.mp3");
        await OpenRootAsync();

        _dialogs.Answer(L("FilterVideo"));
        await _vm.ShowFilterMenuAsync();

        Assert.Empty(_vm.Items);
        Assert.True(_vm.EmptyVisible);
        Assert.Equal(L("FilterVideo"), _vm.Subtitle);
    }

    [Fact]
    public async Task Filter_Cancelled_OrAll_ShowsEverything()
    {
        _tmp.File("a.jpg");
        _tmp.File("b.mp3");
        await OpenRootAsync();

        _dialogs.Answer(L("FilterAudio"));
        await _vm.ShowFilterMenuAsync();
        Assert.Single(_vm.Items);

        _dialogs.Answer(L("Cancel"));
        await _vm.ShowFilterMenuAsync();
        Assert.Equal(FileCategory.Audio, _vm.TypeFilter);

        _dialogs.Answer(new object?[] { null });
        await _vm.ShowFilterMenuAsync();
        Assert.Equal(FileCategory.Audio, _vm.TypeFilter);

        _dialogs.Answer(L("FilterAll"));
        await _vm.ShowFilterMenuAsync();
        Assert.Null(_vm.TypeFilter);
        Assert.Equal(2, _vm.Items.Count);
    }

    [Theory]
    [InlineData(null, "FilterAll")]
    [InlineData(FileCategory.Image, "FilterImages")]
    [InlineData(FileCategory.Video, "FilterVideo")]
    [InlineData(FileCategory.Audio, "FilterAudio")]
    [InlineData(FileCategory.Document, "FilterDocuments")]
    [InlineData(FileCategory.Apk, "FilterApk")]
    [InlineData(FileCategory.Archive, "FilterArchives")]
    [InlineData(FileCategory.Other, "FilterOther")]
    [InlineData(FileCategory.Folder, "FilterOther")]
    public void FilterName_IsLocalised(FileCategory? category, string key) =>
        Assert.Equal(L(key), _vm.FilterName(category));

    // ---------- Navegacion ----------

    [Fact]
    public async Task TappingAFolder_OpensIt_WithBreadcrumbAndUp()
    {
        _tmp.File("A/B/c.txt");
        await OpenRootAsync();

        await _vm.ItemTappedAsync(Item("A"));
        await _vm.ItemTappedAsync(Item("B"));

        Assert.Equal(_tmp.Combine("A", "B"), _vm.CurrentPath);
        Assert.Equal("B", _vm.Title);
        Assert.True(_vm.UpVisible);
        Assert.Equal(new[] { L("InternalStorage"), "A", "B" }, _vm.Breadcrumb.Select(c => c.Label));
        Assert.Equal(new[] { false, false, true }, _vm.Breadcrumb.Select(c => c.IsLast));
        Assert.Equal(_tmp.Combine("A"), _vm.Breadcrumb[1].Path);
    }

    [Fact]
    public async Task Breadcrumb_OutsideTheRoot_HasOnlyTheRoot()
    {
        using var other = new TempFolder();
        await _vm.LoadAsync(other.Path);

        Assert.Single(_vm.Breadcrumb);
        Assert.Equal(Path.GetFileName(other.Path), _vm.Title);
    }

    [Fact]
    public async Task NavigateUp_GoesToTheParent_UntilTheRoot()
    {
        _tmp.Dir("A/B");
        await _vm.LoadAsync(_tmp.Combine("A", "B"));

        Assert.True(await _vm.NavigateUpAsync());
        Assert.Equal(_tmp.Combine("A"), _vm.CurrentPath);
        Assert.True(await _vm.NavigateUpAsync());
        Assert.Equal(_tmp.Path, _vm.CurrentPath);
        Assert.False(await _vm.NavigateUpAsync());
    }

    [Fact]
    public async Task NavigateUp_AtTheFileSystemRoot_StaysPut()
    {
        var driveRoot = Path.GetPathRoot(_tmp.Path)!;
        _fs.RootPath = _tmp.Combine("elsewhere");
        await _vm.LoadAsync(driveRoot);

        Assert.False(await _vm.NavigateUpAsync());
    }

    [Fact]
    public void IsRoot_IgnoresTrailingSeparatorAndCase()
    {
        Assert.True(_vm.IsRoot(_tmp.Path + Path.DirectorySeparatorChar));
        Assert.True(_vm.IsRoot(_tmp.Path.ToUpperInvariant()));
        Assert.False(_vm.IsRoot(_tmp.Combine("x")));
    }

    [Fact]
    public async Task Back_AtTheRoot_IsLeftToTheSystem()
    {
        await OpenRootAsync();
        Assert.Null(_vm.BackAction());
    }

    [Fact]
    public async Task Back_InASubfolder_GoesUp()
    {
        _tmp.Dir("A");
        await _vm.LoadAsync(_tmp.Combine("A"));

        await _vm.BackAction()!();

        Assert.Equal(_tmp.Path, _vm.CurrentPath);
    }

    [Fact]
    public async Task Back_InSelection_OnlyLeavesTheSelection()
    {
        _tmp.Dir("A");
        _tmp.File("A/f.txt");
        await _vm.LoadAsync(_tmp.Combine("A"));
        _vm.ItemLongPressed(Item("f.txt"));

        await _vm.BackAction()!();

        Assert.False(_vm.SelectMode);
        Assert.False(Item("f.txt").IsSelected);
        Assert.Equal(_tmp.Combine("A"), _vm.CurrentPath);
    }

    [Fact]
    public async Task Back_InASearchAtTheRoot_ClosesTheSearch()
    {
        await OpenRootAsync();
        await _vm.ToggleSearchAsync();

        await _vm.BackAction()!();

        Assert.False(_vm.SearchActive);
        Assert.False(_vm.SearchBarVisible);
        Assert.Equal(1, _view.Unfocused);
    }

    // ---------- Abrir ----------

    [Fact]
    public async Task TappingAFile_OpensIt()
    {
        var file = _tmp.File("a.pdf");
        await OpenRootAsync();

        await _vm.ItemTappedAsync(Item("a.pdf"));

        Assert.Equal(new[] { file }, _actions.Opened);
        Assert.Empty(_dialogs.Calls);
    }

    [Fact]
    public async Task OpeningAFile_WithoutAnApp_Explains()
    {
        _tmp.File("a.xyz");
        _actions.OpenResult = false;
        await OpenRootAsync();

        await _vm.ItemTappedAsync(Item("a.xyz"));

        Assert.Equal(L("ErrorNoAppForFile"), _dialogs.Last.Message);
    }

    [Fact]
    public async Task OpeningAFile_Failing_ShowsTheError()
    {
        _tmp.File("a.txt");
        _actions.OpenError = new InvalidOperationException("kaput");
        await OpenRootAsync();

        await _vm.ItemTappedAsync(Item("a.txt"));

        Assert.Equal(string.Format(L("ErrorOpenFile"), "kaput"), _dialogs.Last.Message);
    }

    // ---------- Busqueda ----------

    [Fact]
    public async Task Search_FindsInSubfolders_AndClosingReloadsTheFolder()
    {
        _tmp.File("deep/er/needle.txt");
        _tmp.File("hay.txt");
        await OpenRootAsync();

        await _vm.ToggleSearchAsync();
        Assert.True(_vm.SearchActive);
        Assert.True(_vm.SearchBarVisible);
        Assert.Equal(1, _view.Focused);

        await _vm.SearchTextChangedAsync("  needle ", debounceMs: 0);

        Assert.Equal("needle.txt", Assert.Single(_vm.Items).Name);
        Assert.False(_vm.NewFolderVisible);
        Assert.Equal("ic_search_empty.png", _vm.EmptyIcon);
        Assert.False(_vm.EmptyHintVisible);
        Assert.False(_vm.PasteBarVisible);
        Assert.False(_vm.IsBusy);

        await _vm.ToggleSearchAsync();

        Assert.False(_vm.SearchActive);
        Assert.Equal(string.Empty, _vm.SearchText);
        Assert.Equal(2, _vm.Items.Count);
        Assert.True(_vm.NewFolderVisible);
    }

    [Fact]
    public async Task Search_NoResults_SaysSo()
    {
        await OpenRootAsync();
        await _vm.ToggleSearchAsync();

        await _vm.SearchTextChangedAsync("zzz", debounceMs: 0);

        Assert.True(_vm.EmptyVisible);
        Assert.Equal(L("NoSearchResults"), _vm.EmptyTitle);
    }

    [Fact]
    public async Task Search_EmptyText_ListsTheFolder()
    {
        _tmp.File("a.txt");
        await OpenRootAsync();
        var lists = _fs.ListCalls;

        await _vm.SearchTextChangedAsync("   ");
        await _vm.SearchTextChangedAsync(null);

        Assert.Equal(lists + 2, _fs.ListCalls);
        Assert.Equal(0, _fs.SearchCalls);
    }

    [Fact]
    public async Task Search_ANewKeystroke_CancelsTheOldSearch()
    {
        _tmp.File("abc.txt");
        _tmp.File("abd.txt");
        await OpenRootAsync();
        await _vm.ToggleSearchAsync();

        var first = _vm.SearchTextChangedAsync("ab", debounceMs: 10_000);
        await _vm.SearchTextChangedAsync("abd", debounceMs: 0);
        await first;

        Assert.Equal(1, _fs.SearchCalls);
        Assert.Equal("abd.txt", Assert.Single(_vm.Items).Name);
        Assert.Empty(_dialogs.Calls);
    }

    [Fact]
    public async Task Search_CancelledWhileRunning_DropsItsResults()
    {
        _tmp.File("abc.txt");
        await OpenRootAsync();
        await _vm.ToggleSearchAsync();
        _fs.SearchGate = new TaskCompletionSource();

        var running = _vm.SearchTextChangedAsync("abc", debounceMs: 0);
        await _vm.ToggleSearchAsync(); // cierra la busqueda: cancela y vuelve a listar
        _fs.SearchGate.SetResult();
        await running;

        Assert.False(_vm.SearchActive);
        Assert.Equal("abc.txt", Assert.Single(_vm.Items).Name);
        Assert.False(_vm.EmptyVisible);
    }

    [Fact]
    public async Task Search_Failing_ShowsTheError()
    {
        await OpenRootAsync();
        _fs.SearchError = new IOException("bad");

        await _vm.SearchTextChangedAsync("x", debounceMs: 0);

        Assert.Equal(string.Format(L("ErrorSearch"), "bad"), _dialogs.Last.Message);
        Assert.False(_vm.IsBusy);
    }

    [Fact]
    public async Task TappingAFolder_InASearch_ClosesTheSearch()
    {
        _tmp.File("A/needle/x.txt");
        await OpenRootAsync();
        await _vm.ToggleSearchAsync();
        await _vm.SearchTextChangedAsync("needle", debounceMs: 0);

        await _vm.ItemTappedAsync(Item("needle"));

        Assert.False(_vm.SearchActive);
        Assert.Equal(_tmp.Combine("A", "needle"), _vm.CurrentPath);
    }

    // ---------- Menu de un elemento ----------

    [Fact]
    public async Task ItemMenu_FolderHasNoShare_FileHasIt()
    {
        _tmp.File("a.txt");
        _tmp.Dir("D");
        await OpenRootAsync();

        await _vm.ItemMenuAsync(Item("D"));
        Assert.DoesNotContain(L("Share"), _dialogs.Last.Options);
        Assert.Equal(7, _dialogs.Last.Options.Length);

        await _vm.ItemMenuAsync(Item("a.txt"));
        Assert.Contains(L("Share"), _dialogs.Last.Options);
        Assert.Equal("a.txt", _dialogs.Last.Title);
    }

    [Fact]
    public async Task ItemMenu_Select_StartsSelectionWithTheItem()
    {
        _tmp.File("a.txt");
        _tmp.File("b.txt");
        await OpenRootAsync();

        _dialogs.Answer(L("Select"));
        await _vm.ItemMenuAsync(Item("a.txt"));

        Assert.True(_vm.SelectMode);
        Assert.True(Item("a.txt").IsSelected);
        Assert.Equal(string.Format(L("SelectedCount"), 1), _vm.SelectionCountText);
    }

    [Fact]
    public async Task ItemMenu_Open_OpensFoldersAndFiles()
    {
        var file = _tmp.File("a.txt");
        _tmp.Dir("D");
        await OpenRootAsync();

        _dialogs.Answer(L("Open"));
        await _vm.ItemMenuAsync(Item("a.txt"));
        Assert.Equal(new[] { file }, _actions.Opened);

        _dialogs.Answer(L("Open"));
        await _vm.ItemMenuAsync(Item("D"));
        Assert.Equal(_tmp.Combine("D"), _vm.CurrentPath);
    }

    [Fact]
    public async Task ItemMenu_Share_SharesWithItsName_OrShowsTheError()
    {
        var file = _tmp.File("a.txt");
        await OpenRootAsync();

        _dialogs.Answer(L("Share"));
        await _vm.ItemMenuAsync(Item("a.txt"));
        Assert.Equal((file, "a.txt"), Assert.Single(_actions.Shared));

        _actions.ShareError = new InvalidOperationException("nope");
        _dialogs.Answer(L("Share"));
        await _vm.ItemMenuAsync(Item("a.txt"));
        Assert.Equal(string.Format(L("ErrorShare"), "nope"), _dialogs.Last.Message);
    }

    [Fact]
    public async Task ItemMenu_CopyAndCut_FillTheClipboard()
    {
        var file = _tmp.File("a.txt");
        await OpenRootAsync();

        _dialogs.Answer(L("Copy"));
        await _vm.ItemMenuAsync(Item("a.txt"));
        Assert.False(_clipboard.Current!.IsMove);
        Assert.Equal(new[] { file }, _clipboard.Current.Paths);

        _dialogs.Answer(L("Cut"));
        await _vm.ItemMenuAsync(Item("a.txt"));
        Assert.True(_clipboard.Current!.IsMove);

        Assert.Equal(new[] { string.Format(L("Copied"), 1), string.Format(L("CutToClipboard"), 1) }, _toast.Shown);
    }

    [Fact]
    public async Task ItemMenu_Cancelled_DoesNothing()
    {
        _tmp.File("a.txt");
        await OpenRootAsync();

        await _vm.ItemMenuAsync(Item("a.txt"));

        Assert.False(_vm.SelectMode);
        Assert.Null(_clipboard.Current);
        Assert.Empty(_toast.Shown);
    }

    [Fact]
    public async Task Rename_RenamesAndReloads()
    {
        _tmp.File("old.txt");
        await OpenRootAsync();

        _dialogs.Answer(L("Rename"), "new.txt");
        await _vm.ItemMenuAsync(Item("old.txt"));

        Assert.True(File.Exists(_tmp.Combine("new.txt")));
        Assert.Equal("new.txt", Assert.Single(_vm.Items).Name);
        Assert.Equal(new[] { L("Renamed") }, _toast.Shown);
        Assert.Equal("old.txt", _dialogs.Calls.Single(c => c.Kind == "prompt").Message);
    }

    [Fact]
    public async Task Rename_CancelledOrSameName_ChangesNothing()
    {
        _tmp.File("old.txt");
        await OpenRootAsync();

        _dialogs.Answer(L("Rename"), null);
        await _vm.ItemMenuAsync(Item("old.txt"));
        _dialogs.Answer(L("Rename"), " old.txt ");
        await _vm.ItemMenuAsync(Item("old.txt"));

        Assert.True(File.Exists(_tmp.Combine("old.txt")));
        Assert.Empty(_toast.Shown);
        Assert.DoesNotContain(_dialogs.Calls, c => c.Kind == "alert");
    }

    [Theory]
    [InlineData("", "ErrorNameEmpty")]
    [InlineData("a/b", "ErrorNameInvalid")]
    [InlineData("other.txt", "ErrorNameExists")]
    public async Task Rename_InvalidName_IsExplained(string name, string key)
    {
        _tmp.File("old.txt");
        _tmp.File("other.txt");
        await OpenRootAsync();

        _dialogs.Answer(L("Rename"), name);
        await _vm.ItemMenuAsync(Item("old.txt"));

        Assert.Equal(L(key), _dialogs.Last.Message);
        Assert.True(File.Exists(_tmp.Combine("old.txt")));
    }

    [Fact]
    public async Task Rename_Failing_ShowsTheError()
    {
        _tmp.File("old.txt");
        await OpenRootAsync();
        _fs.RenameError = new IOException("locked");

        _dialogs.Answer(L("Rename"), "new.txt");
        await _vm.ItemMenuAsync(Item("old.txt"));

        Assert.Equal(string.Format(L("ErrorRename"), "locked"), _dialogs.Last.Message);
    }

    [Fact]
    public async Task Details_OfAFile_HaveTypeAndSize()
    {
        _tmp.File("song.mp3", "123");
        await OpenRootAsync();

        _dialogs.Answer(L("Details"));
        await _vm.ItemMenuAsync(Item("song.mp3"));

        var text = _dialogs.Last.Message!;
        Assert.Equal(L("Details"), _dialogs.Last.Title);
        Assert.Contains($"{L("DetailsName")}: song.mp3", text);
        Assert.Contains($"{L("DetailsType")}: MP3 · audio/mpeg", text);
        Assert.Contains($"{L("DetailsSize")}: 3 B", text);
        Assert.Equal(5, text.Split("\n\n").Length);
    }

    [Fact]
    public void Details_OfAFolder_HaveItsContents_AndAFileWithoutExtensionIsAFile()
    {
        var dir = _tmp.Dir("D");
        _tmp.File("D/x");
        var noExt = _tmp.File("README");

        var folder = _vm.DescribeDetails(new FileItem { Name = "D", FullPath = dir, IsDirectory = true });
        var gone = _vm.DescribeDetails(new FileItem { Name = "G", FullPath = _tmp.Combine("G"), IsDirectory = true });
        var file = _vm.DescribeDetails(new FileItem { Name = "README", FullPath = noExt });

        Assert.Contains($"{L("DetailsType")}: {L("TypeFolder")}", folder);
        Assert.Contains($"{L("DetailsContents")}: {string.Format(L("FolderItems"), 1)}", folder);
        Assert.DoesNotContain(L("DetailsContents"), gone);
        Assert.Contains($"{L("DetailsType")}: {L("TypeFile")}", file);
    }

    [Fact]
    public async Task Delete_AsksFirst_AndDeletesOnYes()
    {
        _tmp.File("a.txt");
        _tmp.Dir("D");
        await OpenRootAsync();

        _dialogs.Answer(L("Delete"), false);
        await _vm.ItemMenuAsync(Item("a.txt"));
        Assert.True(File.Exists(_tmp.Combine("a.txt")));
        Assert.Equal(string.Format(L("DeleteFileConfirm"), "a.txt"), _dialogs.Last.Message);

        _dialogs.Answer(L("Delete"), true);
        await _vm.ItemMenuAsync(Item("D"));
        Assert.False(Directory.Exists(_tmp.Combine("D")));
        Assert.Equal(string.Format(L("DeleteFolderConfirm"), "D"), _dialogs.Calls[^1].Message);
        Assert.Equal(new[] { string.Format(L("Deleted"), 1) }, _toast.Shown);
        Assert.Equal("a.txt", Assert.Single(_vm.Items).Name);
    }

    [Fact]
    public async Task Delete_WithoutConfirmation_AndWithErrors()
    {
        _settings.ConfirmDelete = false;
        var file = _tmp.File("a.txt");
        await OpenRootAsync();
        _fs.DeleteErrorFor = file;

        _dialogs.Answer(L("Delete"));
        await _vm.ItemMenuAsync(Item("a.txt"));

        Assert.Equal(string.Format(L("ErrorDelete"), "boom: a.txt"), _dialogs.Last.Message);
        Assert.Empty(_toast.Shown);
        Assert.False(_vm.IsBusy);
    }

    // ---------- Seleccion multiple ----------

    [Fact]
    public async Task LongPress_StartsSelection_TapsToggle()
    {
        _tmp.File("a.txt");
        _tmp.File("b.txt");
        await OpenRootAsync();

        _vm.ItemLongPressed(Item("a.txt"));
        Assert.True(_vm.SelectMode);
        Assert.True(_vm.SelectionBarVisible);
        Assert.False(_vm.SearchButtonVisible);
        Assert.False(_vm.NewFolderVisible);

        await _vm.ItemTappedAsync(Item("b.txt"));
        await _vm.ItemTappedAsync(Item("a.txt"));
        _vm.ItemLongPressed(Item("a.txt"));

        Assert.Equal(new[] { "a.txt", "b.txt" }, _vm.SelectedItems().Select(i => i.Name));
        Assert.Equal(string.Format(L("SelectedCount"), 2), _vm.SelectionCountText);
        Assert.Empty(_actions.Opened);
    }

    [Fact]
    public async Task SelectAll_SelectsThenClears()
    {
        _tmp.File("a.txt");
        _tmp.File("b.txt");
        await OpenRootAsync();
        _vm.EnterSelectionMode();
        _vm.EnterSelectionMode();

        _vm.SelectAll();
        Assert.Equal(2, _vm.SelectedItems().Count);
        _vm.SelectAll();
        Assert.Empty(_vm.SelectedItems());
    }

    [Fact]
    public async Task SelectAll_OnAnEmptyFolder_IsHarmless()
    {
        await OpenRootAsync();
        _vm.SelectAll();
        Assert.Equal(string.Format(L("SelectedCount"), 0), _vm.SelectionCountText);
    }

    [Fact]
    public async Task EnteringSelection_ClosesTheSearch()
    {
        await OpenRootAsync();
        await _vm.ToggleSearchAsync();

        _vm.EnterSelectionMode();

        Assert.False(_vm.SearchActive);
        Assert.True(_vm.SelectMode);
    }

    [Fact]
    public async Task ExitSelection_ClearsTheMarks_AndRestoresTheButtons()
    {
        _tmp.File("a.txt");
        await OpenRootAsync();
        _vm.ItemLongPressed(Item("a.txt"));

        _vm.ExitSelectionMode();

        Assert.False(Item("a.txt").IsSelected);
        Assert.False(_vm.SelectionBarVisible);
        Assert.True(_vm.SearchButtonVisible);
        Assert.True(_vm.NewFolderVisible);
    }

    [Fact]
    public async Task ChangingFolder_LeavesSelection()
    {
        _tmp.File("A/x.txt");
        await OpenRootAsync();
        _vm.ItemLongPressed(Item("A"));

        await _vm.LoadAsync(_tmp.Combine("A"));

        Assert.False(_vm.SelectMode);
    }

    [Fact]
    public async Task Batch_WithNothingSelected_Says()
    {
        await OpenRootAsync();
        await _vm.BatchActionsAsync();
        Assert.Equal(new[] { L("NothingSelected") }, _toast.Shown);
        Assert.Empty(_dialogs.Calls);
    }

    [Fact]
    public async Task Batch_CopyAndCut_FillTheClipboard_AndLeaveSelection()
    {
        var a = _tmp.File("a.txt");
        var b = _tmp.File("b.txt");
        await OpenRootAsync();
        _vm.EnterSelectionMode();
        _vm.SelectAll();

        _dialogs.Answer(L("Copy"));
        await _vm.BatchActionsAsync();
        Assert.Equal(new[] { a, b }, _clipboard.Current!.Paths);
        Assert.False(_clipboard.Current.IsMove);
        Assert.False(_vm.SelectMode);
        Assert.Equal(string.Format(L("SelectedCount"), 2), _dialogs.Last.Title);

        _vm.EnterSelectionMode();
        _vm.SelectAll();
        _dialogs.Answer(L("Cut"));
        await _vm.BatchActionsAsync();
        Assert.True(_clipboard.Current!.IsMove);
    }

    [Fact]
    public async Task Batch_Cancelled_KeepsTheSelection()
    {
        _tmp.File("a.txt");
        await OpenRootAsync();
        _vm.ItemLongPressed(Item("a.txt"));

        await _vm.BatchActionsAsync();

        Assert.True(_vm.SelectMode);
        Assert.Null(_clipboard.Current);
    }

    [Fact]
    public async Task Batch_Delete_AsksWithTheCount_AndDeletes()
    {
        _tmp.File("a.txt");
        _tmp.File("b.txt");
        _tmp.File("keep.txt");
        await OpenRootAsync();
        _vm.ItemLongPressed(Item("a.txt"));
        _vm.ItemLongPressed(Item("b.txt"));

        _dialogs.Answer(L("Delete"), false);
        await _vm.BatchActionsAsync();
        Assert.Equal(string.Format(L("DeleteManyConfirm"), 2), _dialogs.Last.Message);
        Assert.Equal(3, _vm.Items.Count);
        Assert.True(_vm.SelectMode);

        _dialogs.Answer(L("Delete"), true);
        await _vm.BatchActionsAsync();
        Assert.Equal("keep.txt", Assert.Single(_vm.Items).Name);
        Assert.False(_vm.SelectMode);
        Assert.Equal(new[] { string.Format(L("Deleted"), 2) }, _toast.Shown);
    }

    [Fact]
    public async Task Batch_Delete_WithErrors_ListsThem()
    {
        _settings.ConfirmDelete = false;
        _tmp.File("a.txt");
        var b = _tmp.File("b.txt");
        await OpenRootAsync();
        _vm.EnterSelectionMode();
        _vm.SelectAll();
        _fs.DeleteErrorFor = b;

        _dialogs.Answer(L("Delete"));
        await _vm.BatchActionsAsync();

        Assert.Equal(string.Format(L("ErrorDelete"), "boom: b.txt"), _dialogs.Last.Message);
        Assert.Equal("b.txt", Assert.Single(_vm.Items).Name);
    }

    // ---------- Nueva carpeta ----------

    [Fact]
    public async Task NewFolder_CreatesIt()
    {
        await OpenRootAsync();

        _dialogs.Answer("Photos");
        await _vm.NewFolderAsync();

        Assert.True(Directory.Exists(_tmp.Combine("Photos")));
        Assert.Equal("Photos", Assert.Single(_vm.Items).Name);
        Assert.Equal(new[] { L("FolderCreated") }, _toast.Shown);
    }

    [Fact]
    public async Task NewFolder_CancelledOrInvalid_CreatesNothing()
    {
        _tmp.Dir("Taken");
        await OpenRootAsync();

        await _vm.NewFolderAsync();
        _dialogs.Answer("Taken");
        await _vm.NewFolderAsync();

        Assert.Equal(L("ErrorNameExists"), _dialogs.Last.Message);
        Assert.Single(Directory.GetDirectories(_tmp.Path));
    }

    [Fact]
    public async Task NewFolder_Failing_ShowsTheError()
    {
        await OpenRootAsync();
        _fs.CreateError = new IOException("full");

        _dialogs.Answer("X");
        await _vm.NewFolderAsync();

        Assert.Equal(string.Format(L("ErrorCreateFolder"), "full"), _dialogs.Last.Message);
    }

    // ---------- Portapapeles y pegar ----------

    [Fact]
    public async Task PasteBar_ShowsWhatIsInTheClipboard()
    {
        var a = _tmp.File("a.txt");
        var b = _tmp.File("b.txt");
        await OpenRootAsync();
        Assert.False(_vm.PasteBarVisible);

        _clipboard.Set(new[] { a, b }, isMove: true);
        _vm.UpdatePasteBar();

        Assert.True(_vm.PasteBarVisible);
        Assert.Equal($"{L("Cut")}: a.txt, b.txt", _vm.PasteText);

        _clipboard.Set(new[] { a }, isMove: false);
        _vm.UpdatePasteBar();
        Assert.Equal($"{L("Copy")}: a.txt", _vm.PasteText);

        _vm.CancelPaste();
        _vm.UpdatePasteBar();
        Assert.False(_vm.PasteBarVisible);
    }

    [Fact]
    public async Task PasteBar_IsHidden_InASearch_OrWithoutPermission()
    {
        _clipboard.Set(new[] { _tmp.File("a.txt") }, isMove: false);
        await OpenRootAsync();
        await _vm.ToggleSearchAsync();
        _vm.UpdatePasteBar();
        Assert.False(_vm.PasteBarVisible);

        await _vm.ToggleSearchAsync();
        _permissions.HasFullAccess = false;
        _vm.UpdatePasteBar();
        Assert.False(_vm.PasteBarVisible);
    }

    [Fact]
    public async Task Paste_WithAnEmptyClipboard_DoesNothing()
    {
        await OpenRootAsync();
        await _vm.PasteAsync();
        Assert.Empty(_dialogs.Calls);
        Assert.Empty(_toast.Shown);
    }

    [Fact]
    public async Task Paste_Copy_CopiesHere_AndKeepsTheClipboard()
    {
        var src = _tmp.File("src/a.txt");
        _tmp.Dir("dst");
        await _vm.LoadAsync(_tmp.Combine("dst"));
        _clipboard.Set(new[] { src }, isMove: false);

        await _vm.PasteAsync();

        Assert.True(File.Exists(_tmp.Combine("dst", "a.txt")));
        Assert.True(File.Exists(src));
        Assert.NotNull(_clipboard.Current);
        Assert.Equal(new[] { string.Format(L("Pasted"), 1) }, _toast.Shown);
        Assert.Equal("a.txt", Assert.Single(_vm.Items).Name);
    }

    [Fact]
    public async Task Paste_Move_MovesHere_AndEmptiesTheClipboard()
    {
        var src = _tmp.File("src/a.txt");
        _tmp.Dir("dst");
        await _vm.LoadAsync(_tmp.Combine("dst"));
        _clipboard.Set(new[] { src }, isMove: true);

        await _vm.PasteAsync();

        Assert.False(File.Exists(src));
        Assert.Null(_clipboard.Current);
        Assert.Equal(new[] { string.Format(L("Moved"), 1) }, _toast.Shown);
    }

    [Fact]
    public async Task Paste_AFolderIntoItself_IsRefused()
    {
        var dir = _tmp.Dir("D");
        _tmp.Dir("D/inner");
        await _vm.LoadAsync(_tmp.Combine("D", "inner"));
        _clipboard.Set(new[] { dir }, isMove: false);

        await _vm.PasteAsync();

        Assert.Equal(L("ErrorPasteIntoItself"), _dialogs.Last.Message);
        Assert.Empty(_toast.Shown);
    }

    [Fact]
    public async Task Paste_Conflict_ReplaceKeepBothOrCancel()
    {
        var src = _tmp.File("src/a.txt", "new");
        _tmp.File("dst/a.txt", "old");
        await _vm.LoadAsync(_tmp.Combine("dst"));
        _clipboard.Set(new[] { src }, isMove: false);

        _dialogs.Answer(L("Cancel"));
        await _vm.PasteAsync();
        Assert.Equal(string.Format(L("OverwriteConfirm"), "a.txt"), _dialogs.Last.Title);
        Assert.Single(_vm.Items);

        _dialogs.Answer(L("KeepBoth"));
        await _vm.PasteAsync();
        Assert.Equal(2, _vm.Items.Count);
        Assert.Equal("old", File.ReadAllText(_tmp.Combine("dst", "a.txt")));

        _dialogs.Answer(L("Replace"));
        await _vm.PasteAsync();
        Assert.Equal("new", File.ReadAllText(_tmp.Combine("dst", "a.txt")));
    }

    [Fact]
    public async Task Paste_WithItemErrors_ListsThem()
    {
        var gone = _tmp.Combine("vanished.txt");
        await OpenRootAsync();
        _clipboard.Set(new[] { gone }, isMove: false);

        await _vm.PasteAsync();

        Assert.StartsWith(string.Format(L("ErrorPaste"), "").TrimEnd(), _dialogs.Last.Message);
        Assert.Empty(_toast.Shown);
        Assert.False(_vm.IsBusy);
    }

    [Fact]
    public async Task Paste_Failing_ShowsTheError()
    {
        var src = _tmp.File("src/a.txt");
        await OpenRootAsync();
        _clipboard.Set(new[] { src }, isMove: false);
        _fs.PasteError = new IOException("full");

        await _vm.PasteAsync();

        Assert.Equal(string.Format(L("ErrorPaste"), "full"), _dialogs.Last.Message);
        Assert.False(_vm.IsBusy);
    }

    // ---------- Menu general ----------

    [Fact]
    public async Task More_ShowsHidden_TogglesAndReloads()
    {
        _tmp.File(".hidden");
        await OpenRootAsync();
        Assert.Empty(_vm.Items);

        _dialogs.Answer(L("ShowHiddenOn"));
        await _vm.MoreMenuAsync();
        Assert.True(_settings.ShowHiddenFiles);
        Assert.Single(_vm.Items);
        Assert.Contains(L("ShowHiddenOn"), _dialogs.Last.Options);

        _dialogs.Answer(L("ShowHiddenOff"));
        await _vm.MoreMenuAsync();
        Assert.False(_settings.ShowHiddenFiles);
        Assert.Empty(_vm.Items);
    }

    [Fact]
    public async Task More_Refresh_PicksUpNewFiles()
    {
        await OpenRootAsync();
        _tmp.File("late.txt");

        _dialogs.Answer(L("Refresh"));
        await _vm.MoreMenuAsync();

        Assert.Single(_vm.Items);
    }

    [Fact]
    public async Task More_OpensSettingsAboutSelectionFilterAndSort()
    {
        _tmp.File("a.txt");
        _tmp.File("bb.txt", "longer");
        await OpenRootAsync();

        _dialogs.Answer(L("Settings"));
        await _vm.MoreMenuAsync();
        _dialogs.Answer(L("About"));
        await _vm.MoreMenuAsync();
        Assert.Equal(1, _view.SettingsOpened);
        Assert.Equal(1, _view.AboutOpened);

        _dialogs.Answer(L("Filter"), L("FilterDocuments"));
        await _vm.MoreMenuAsync();
        Assert.Equal(FileCategory.Document, _vm.TypeFilter);

        _dialogs.Answer(L("Sort"), L("SortSizeDesc"));
        await _vm.MoreMenuAsync();
        Assert.Equal(SortMode.SizeDescending, _settings.Sort);
        Assert.Equal(new[] { "bb.txt", "a.txt" }, _vm.Items.Select(i => i.Name));

        _dialogs.Answer(L("Select"));
        await _vm.MoreMenuAsync();
        Assert.True(_vm.SelectMode);
    }

    [Fact]
    public async Task Sort_Cancelled_KeepsTheOrder()
    {
        _settings.Sort = SortMode.DateAscending;
        await OpenRootAsync();

        await _vm.ShowSortMenuAsync();
        _dialogs.Answer(new object?[] { null });
        await _vm.ShowSortMenuAsync();

        Assert.Equal(SortMode.DateAscending, _settings.Sort);
        Assert.Equal(6, _dialogs.Last.Options.Length);
    }

    [Fact]
    public async Task More_Cancelled_DoesNothing()
    {
        await OpenRootAsync();
        var lists = _fs.ListCalls;
        await _vm.MoreMenuAsync();
        Assert.Equal(lists, _fs.ListCalls);
        Assert.False(_vm.SelectMode);
    }

    // ---------- Textos fijos ----------

    [Fact]
    public void StaticTexts_AllExistInBothLanguages()
    {
        static Dictionary<string, string> Table(string name) =>
            (Dictionary<string, string>)typeof(LocalizationService)
                .GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .GetValue(null)!;

        var keys = MainViewModel.StaticTexts.Values
            .Concat(SettingsViewModel.StaticTexts.Values)
            .Concat(AboutViewModel.StaticTexts.Values)
            .Append(SettingsViewModel.TitleKey)
            .Append(AboutViewModel.TitleKey);

        foreach (var key in keys)
        {
            Assert.True(Table("English").ContainsKey(key), key);
            Assert.True(Table("Spanish").ContainsKey(key), key);
        }
    }
}
