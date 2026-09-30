using FileManager.Models;
using FileManager.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileManager.Tests;

public sealed class FileSystemServiceTests : IDisposable
{
    private readonly TempFolder _tmp = new();
    private readonly FileSystemService _fs = new(NullLogger<FileSystemService>.Instance);

    public void Dispose() => _tmp.Dispose();

    // ---------- RootPath ----------

    [Fact]
    public void RootPath_OutsideAndroid_IsEmulatedStorage() =>
        Assert.Equal("/storage/emulated/0", _fs.RootPath);

    // ---------- ListAsync ----------

    [Fact]
    public async Task List_PutsFoldersFirst_ThenSortsByName()
    {
        _tmp.File("b.txt");
        _tmp.File("A.txt");
        _tmp.Dir("zeta");
        _tmp.Dir("Alfa");

        var items = await _fs.ListAsync(_tmp.Path, showHidden: false, SortMode.NameAscending);

        Assert.Equal(new[] { "Alfa", "zeta", "A.txt", "b.txt" }, items.Select(i => i.Name));
        Assert.True(items[0].IsDirectory);
        Assert.Equal(0, items[0].Size);
        Assert.Equal(_tmp.Combine("Alfa"), items[0].FullPath);
    }

    [Fact]
    public async Task List_NameDescending_KeepsFoldersFirst()
    {
        _tmp.File("a.txt");
        _tmp.File("c.txt");
        _tmp.Dir("b");

        var items = await _fs.ListAsync(_tmp.Path, false, SortMode.NameDescending);

        Assert.Equal(new[] { "b", "c.txt", "a.txt" }, items.Select(i => i.Name));
    }

    [Fact]
    public async Task List_SortsBySize_BothDirections()
    {
        _tmp.File("small.txt", "1");
        _tmp.File("big.txt", new string('x', 1000));
        _tmp.File("mid.txt", new string('x', 100));

        var desc = await _fs.ListAsync(_tmp.Path, false, SortMode.SizeDescending);
        var asc = await _fs.ListAsync(_tmp.Path, false, SortMode.SizeAscending);

        Assert.Equal(new[] { "big.txt", "mid.txt", "small.txt" }, desc.Select(i => i.Name));
        Assert.Equal(new[] { "small.txt", "mid.txt", "big.txt" }, asc.Select(i => i.Name));
        Assert.Equal(1000, desc[0].Size);
    }

    [Fact]
    public async Task List_SortsByDate_BothDirections()
    {
        var old = _tmp.File("old.txt");
        var recent = _tmp.File("new.txt");
        File.SetLastWriteTime(old, new DateTime(2020, 1, 1));
        File.SetLastWriteTime(recent, new DateTime(2025, 1, 1));

        var desc = await _fs.ListAsync(_tmp.Path, false, SortMode.DateDescending);
        var asc = await _fs.ListAsync(_tmp.Path, false, SortMode.DateAscending);

        Assert.Equal(new[] { "new.txt", "old.txt" }, desc.Select(i => i.Name));
        Assert.Equal(new[] { "old.txt", "new.txt" }, asc.Select(i => i.Name));
        Assert.Equal(new DateTime(2025, 1, 1), desc[0].Modified);
    }

    [Fact]
    public async Task List_HidesDotEntriesUnlessAsked()
    {
        _tmp.File(".hidden");
        _tmp.Dir(".config");
        _tmp.File("visible.txt");

        var hidden = await _fs.ListAsync(_tmp.Path, false, SortMode.NameAscending);
        var shown = await _fs.ListAsync(_tmp.Path, true, SortMode.NameAscending);

        Assert.Equal(new[] { "visible.txt" }, hidden.Select(i => i.Name));
        Assert.Equal(3, shown.Count);
        Assert.All(shown.Where(i => i.Name.StartsWith('.')), i => Assert.True(i.IsHidden));
        Assert.False(shown.Single(i => i.Name == "visible.txt").IsHidden);
    }

    [Fact]
    public async Task List_EmptyFolder_ReturnsEmpty() =>
        Assert.Empty(await _fs.ListAsync(_tmp.Path, true, SortMode.NameAscending));

    [Fact]
    public async Task List_MissingFolder_Throws() =>
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _fs.ListAsync(_tmp.Combine("nope"), false, SortMode.NameAscending));

    [Fact]
    public async Task List_Cancelled_Throws()
    {
        _tmp.File("a.txt");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _fs.ListAsync(_tmp.Path, false, SortMode.NameAscending, cts.Token));
    }

    [Fact]
    public async Task List_UnknownSortValue_FallsBackToName()
    {
        _tmp.File("b.txt");
        _tmp.File("a.txt");

        var items = await _fs.ListAsync(_tmp.Path, false, (SortMode)99);

        Assert.Equal(new[] { "a.txt", "b.txt" }, items.Select(i => i.Name));
    }

    // ---------- SearchAsync ----------

    [Fact]
    public async Task Search_FindsInSubfolders_CaseInsensitive_FoldersFirstThenByName()
    {
        _tmp.File("Report.pdf");
        _tmp.File("sub/deep/old-report.txt");
        _tmp.File("sub/other.txt");
        _tmp.Dir("reports");

        var results = await _fs.SearchAsync(_tmp.Path, "REPORT", showHidden: false);

        Assert.Equal(new[] { "reports", "old-report.txt", "Report.pdf" }, results.Select(r => r.Name));
    }

    [Fact]
    public async Task Search_SkipsHiddenEntriesAndTheirContents_UnlessAsked()
    {
        _tmp.File(".secret/match.txt");
        _tmp.File(".match");
        _tmp.File("match.txt");

        var visible = await _fs.SearchAsync(_tmp.Path, "match", showHidden: false);
        var all = await _fs.SearchAsync(_tmp.Path, "match", showHidden: true);

        Assert.Equal(new[] { "match.txt" }, visible.Select(r => r.Name));
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task Search_NoMatches_ReturnsEmpty()
    {
        _tmp.File("a.txt");
        Assert.Empty(await _fs.SearchAsync(_tmp.Path, "zzz", false));
    }

    [Fact]
    public async Task Search_StopsAt500Results()
    {
        for (var i = 0; i < 260; i++)
        {
            _tmp.File($"one/hit{i}.txt");
            _tmp.File($"two/hit{i}.txt");
        }

        var results = await _fs.SearchAsync(_tmp.Path, "hit", false);

        Assert.Equal(500, results.Count);
    }

    [Fact]
    public async Task Search_UnreadableStartFolder_IsSkippedNotThrown() =>
        Assert.Empty(await _fs.SearchAsync(_tmp.Combine("missing"), "a", false));

    [Fact]
    public async Task Search_Cancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _fs.SearchAsync(_tmp.Path, "a", false, cts.Token));
    }

    // ---------- CountEntries ----------

    [Fact]
    public void CountEntries_CountsDirectChildrenOnly()
    {
        _tmp.File("a.txt");
        _tmp.File("sub/b.txt");
        _tmp.File("sub/c.txt");

        Assert.Equal(2, _fs.CountEntries(_tmp.Path));
        Assert.Equal(2, _fs.CountEntries(_tmp.Combine("sub")));
    }

    [Fact]
    public void CountEntries_MissingFolder_ReturnsMinusOne() =>
        Assert.Equal(-1, _fs.CountEntries(_tmp.Combine("missing")));

    // ---------- ValidateName ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateName_Blank_IsEmpty(string? name) =>
        Assert.Equal(NameValidation.Empty, _fs.ValidateName(name, _tmp.Path));

    [Theory]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    [InlineData("a*b")]
    [InlineData("a?b")]
    [InlineData("a\"b")]
    [InlineData("a<b")]
    [InlineData("a>b")]
    [InlineData("a|b")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData(" .. ")]
    public void ValidateName_ForbiddenCharactersOrDots_AreInvalid(string name) =>
        Assert.Equal(NameValidation.InvalidCharacters, _fs.ValidateName(name, _tmp.Path));

    [Fact]
    public void ValidateName_ExistingFileOrFolder_AlreadyExists()
    {
        _tmp.File("taken.txt");
        _tmp.Dir("folder");

        Assert.Equal(NameValidation.AlreadyExists, _fs.ValidateName("taken.txt", _tmp.Path));
        Assert.Equal(NameValidation.AlreadyExists, _fs.ValidateName("  folder  ", _tmp.Path));
    }

    [Fact]
    public void ValidateName_FreeName_IsValid() =>
        Assert.Equal(NameValidation.Valid, _fs.ValidateName("new name.txt", _tmp.Path));

    [Fact]
    public void ValidateName_RenamingToItsOwnNameOrOnlyCase_IsValid()
    {
        var current = _tmp.File("Photo.jpg");

        Assert.Equal(NameValidation.Valid, _fs.ValidateName("Photo.jpg", _tmp.Path, current));
        Assert.Equal(NameValidation.Valid, _fs.ValidateName("photo.JPG", _tmp.Path, current));
    }

    [Fact]
    public void ValidateName_RenamingOntoAnotherItem_AlreadyExists()
    {
        var current = _tmp.File("a.txt");
        _tmp.File("b.txt");

        Assert.Equal(NameValidation.AlreadyExists, _fs.ValidateName("b.txt", _tmp.Path, current));
    }

    // ---------- Create / Rename ----------

    [Fact]
    public async Task CreateDirectory_TrimsNameAndReturnsPath()
    {
        var created = await _fs.CreateDirectoryAsync(_tmp.Path, "  New  ");

        Assert.Equal(_tmp.Combine("New"), created);
        Assert.True(Directory.Exists(created));
    }

    [Fact]
    public async Task Rename_File_MovesIt()
    {
        var file = _tmp.File("a.txt", "content");

        var renamed = await _fs.RenameAsync(file, " b.txt ");

        Assert.Equal(_tmp.Combine("b.txt"), renamed);
        Assert.False(File.Exists(file));
        Assert.Equal("content", File.ReadAllText(renamed));
    }

    [Fact]
    public async Task Rename_Folder_KeepsItsContents()
    {
        _tmp.File("old/inner.txt");

        var renamed = await _fs.RenameAsync(_tmp.Combine("old"), "new");

        Assert.True(File.Exists(Path.Combine(renamed, "inner.txt")));
        Assert.False(Directory.Exists(_tmp.Combine("old")));
    }

    [Fact]
    public async Task Rename_MissingItem_Throws() =>
        await Assert.ThrowsAsync<FileNotFoundException>(() => _fs.RenameAsync(_tmp.Combine("ghost.txt"), "x.txt"));

    [Fact]
    public async Task Rename_Root_HasNoParent_Throws() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => _fs.RenameAsync(Path.GetPathRoot(_tmp.Path)!, "x"));

    // ---------- DeleteAsync ----------

    [Fact]
    public async Task Delete_FilesAndFoldersRecursively_CountsSuccesses_IgnoresMissing()
    {
        var file = _tmp.File("a.txt");
        _tmp.File("dir/sub/b.txt");

        var result = await _fs.DeleteAsync(new[] { file, _tmp.Combine("dir"), _tmp.Combine("ghost") });

        Assert.Equal(2, result.Succeeded);
        Assert.False(result.HasErrors);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_tmp.Path));
    }

    [Fact]
    public async Task Delete_OneFailure_DoesNotAbortTheBatch()
    {
        // Windows no deja borrar un fichero abierto sin compartir: sirve para provocar el fallo.
        if (!OperatingSystem.IsWindows())
            return;

        var locked = _tmp.File("locked.txt");
        var free = _tmp.File("free.txt");

        OperationResult result;
        using (File.Open(locked, FileMode.Open, FileAccess.Read, FileShare.None))
            result = await _fs.DeleteAsync(new[] { locked, free });

        Assert.Equal(1, result.Succeeded);
        Assert.True(result.HasErrors);
        Assert.StartsWith("locked.txt:", result.Errors[0]);
        Assert.False(File.Exists(free));
    }

    [Fact]
    public async Task Delete_Cancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _fs.DeleteAsync(new[] { _tmp.File("a") }, cts.Token));
    }

    [Fact]
    public void OperationResult_StartsEmpty()
    {
        var result = new OperationResult();
        Assert.Equal(0, result.Succeeded);
        Assert.False(result.HasErrors);
    }

    // ---------- GetConflicts ----------

    [Fact]
    public void GetConflicts_ListsNamesThatExistInDestination_ExceptTheSourceItself()
    {
        var src1 = _tmp.File("src/a.txt");
        var src2 = _tmp.File("src/b.txt");
        var src3 = _tmp.Dir("src/folder");
        _tmp.File("dst/a.txt");
        _tmp.Dir("dst/folder");

        var conflicts = _fs.GetConflicts(new[] { src1, src2, src3 }, _tmp.Combine("dst"));
        var sameFolder = _fs.GetConflicts(new[] { src1 }, _tmp.Combine("src"));

        Assert.Equal(new[] { "a.txt", "folder" }, conflicts);
        Assert.Empty(sameFolder);
    }

    // ---------- IsInside ----------

    [Fact]
    public void IsInside_DetectsSameFolderAndDescendants_NotSiblingsWithSamePrefix()
    {
        var a = _tmp.Combine("a");

        Assert.True(_fs.IsInside(a, a));
        Assert.True(_fs.IsInside(a, a + Path.DirectorySeparatorChar));
        Assert.True(_fs.IsInside(a, _tmp.Combine("a", "b", "c")));
        Assert.True(_fs.IsInside(a, _tmp.Combine("A", "b")));
        Assert.False(_fs.IsInside(a, _tmp.Combine("ab")));
        Assert.False(_fs.IsInside(a, _tmp.Path));
    }

    // ---------- PasteAsync ----------

    private Task<OperationResult> Paste(string destination, bool move, ConflictResolution resolution, params string[] paths) =>
        _fs.PasteAsync(new ClipboardEntry { Paths = paths, IsMove = move }, destination, resolution);

    [Fact]
    public async Task Paste_CopyFile_KeepsSource()
    {
        var src = _tmp.File("src/a.txt", "hello");
        var dst = _tmp.Dir("dst");

        var result = await Paste(dst, false, ConflictResolution.KeepBoth, src);

        Assert.Equal(1, result.Succeeded);
        Assert.True(File.Exists(src));
        Assert.Equal("hello", File.ReadAllText(Path.Combine(dst, "a.txt")));
    }

    [Fact]
    public async Task Paste_MoveFile_RemovesSource()
    {
        var src = _tmp.File("src/a.txt", "hello");
        var dst = _tmp.Dir("dst");

        var result = await Paste(dst, true, ConflictResolution.KeepBoth, src);

        Assert.Equal(1, result.Succeeded);
        Assert.False(File.Exists(src));
        Assert.Equal("hello", File.ReadAllText(Path.Combine(dst, "a.txt")));
    }

    [Fact]
    public async Task Paste_CopyFolder_CopiesTreeRecursively()
    {
        _tmp.File("src/tree/a.txt", "1");
        _tmp.File("src/tree/sub/b.txt", "2");
        _tmp.Dir("src/tree/empty");
        var dst = _tmp.Dir("dst");

        var result = await Paste(dst, false, ConflictResolution.KeepBoth, _tmp.Combine("src", "tree"));

        Assert.Equal(1, result.Succeeded);
        Assert.Equal("1", File.ReadAllText(Path.Combine(dst, "tree", "a.txt")));
        Assert.Equal("2", File.ReadAllText(Path.Combine(dst, "tree", "sub", "b.txt")));
        Assert.True(Directory.Exists(Path.Combine(dst, "tree", "empty")));
        Assert.True(File.Exists(_tmp.Combine("src", "tree", "a.txt")));
    }

    [Fact]
    public async Task Paste_MoveFolder_RemovesSource()
    {
        _tmp.File("src/tree/sub/b.txt", "2");
        var dst = _tmp.Dir("dst");

        var result = await Paste(dst, true, ConflictResolution.KeepBoth, _tmp.Combine("src", "tree"));

        Assert.Equal(1, result.Succeeded);
        Assert.False(Directory.Exists(_tmp.Combine("src", "tree")));
        Assert.Equal("2", File.ReadAllText(Path.Combine(dst, "tree", "sub", "b.txt")));
    }

    [Fact]
    public async Task Paste_CopyIntoSameFolder_CreatesNumberedCopies()
    {
        var src = _tmp.File("a.txt", "v");

        await Paste(_tmp.Path, false, ConflictResolution.Replace, src);
        await Paste(_tmp.Path, false, ConflictResolution.Replace, src);

        Assert.True(File.Exists(_tmp.Combine("a (2).txt")));
        Assert.True(File.Exists(_tmp.Combine("a (3).txt")));
        Assert.Equal("v", File.ReadAllText(src));
    }

    [Fact]
    public async Task Paste_CopyFolderWithDotInNameIntoSameFolder_NumbersTheWholeName()
    {
        _tmp.File("release.v2/a.txt");

        var result = await Paste(_tmp.Path, false, ConflictResolution.KeepBoth, _tmp.Combine("release.v2"));

        Assert.Equal(1, result.Succeeded);
        Assert.True(File.Exists(_tmp.Combine("release.v2 (2)", "a.txt")));
    }

    [Fact]
    public async Task Paste_MoveIntoSameFolder_DoesNothing()
    {
        var src = _tmp.File("a.txt");

        var result = await Paste(_tmp.Path, true, ConflictResolution.KeepBoth, src);

        Assert.Equal(0, result.Succeeded);
        Assert.False(result.HasErrors);
        Assert.Single(Directory.EnumerateFiles(_tmp.Path));
    }

    [Fact]
    public async Task Paste_Conflict_KeepBoth_AddsNumber()
    {
        var src = _tmp.File("src/a.txt", "new");
        _tmp.File("dst/a.txt", "old");

        await Paste(_tmp.Combine("dst"), false, ConflictResolution.KeepBoth, src);

        Assert.Equal("old", File.ReadAllText(_tmp.Combine("dst", "a.txt")));
        Assert.Equal("new", File.ReadAllText(_tmp.Combine("dst", "a (2).txt")));
    }

    [Fact]
    public async Task Paste_Conflict_Replace_OverwritesFileAndFolder()
    {
        var file = _tmp.File("src/a.txt", "new");
        _tmp.File("src/dir/new.txt");
        _tmp.File("dst/a.txt", "old");
        _tmp.File("dst/dir/old.txt");

        var result = await Paste(_tmp.Combine("dst"), true, ConflictResolution.Replace, file, _tmp.Combine("src", "dir"));

        Assert.Equal(2, result.Succeeded);
        Assert.Equal("new", File.ReadAllText(_tmp.Combine("dst", "a.txt")));
        Assert.True(File.Exists(_tmp.Combine("dst", "dir", "new.txt")));
        Assert.False(File.Exists(_tmp.Combine("dst", "dir", "old.txt")));
    }

    [Fact]
    public async Task Paste_Conflict_Replace_FolderByFile()
    {
        var file = _tmp.File("src/x", "file");
        _tmp.File("dst/x/inside.txt");

        var result = await Paste(_tmp.Combine("dst"), false, ConflictResolution.Replace, file);

        Assert.Equal(1, result.Succeeded);
        Assert.Equal("file", File.ReadAllText(_tmp.Combine("dst", "x")));
    }

    [Fact]
    public async Task Paste_Replace_TargetThatContainsTheSource_RefusesAndLosesNothing()
    {
        // Pegar p/x/x en p con «Reemplazar»: el destino p/x contiene el origen. Borrarlo antes
        // de mover se llevaba por delante lo que se queria pegar.
        _tmp.File("x/x/keep.txt", "valuable");

        var result = await Paste(_tmp.Path, true, ConflictResolution.Replace, _tmp.Combine("x", "x"));

        Assert.Equal(0, result.Succeeded);
        Assert.True(result.HasErrors);
        Assert.Equal("valuable", File.ReadAllText(_tmp.Combine("x", "x", "keep.txt")));
    }

    [Fact]
    public async Task Paste_FolderInsideItself_IsReportedAndNothingIsCopied()
    {
        var folder = _tmp.Dir("a");
        var inner = _tmp.Dir("a/b");

        var result = await Paste(inner, false, ConflictResolution.KeepBoth, folder);

        Assert.Equal(0, result.Succeeded);
        Assert.Single(result.Errors);
        Assert.StartsWith("a:", result.Errors[0]);
        Assert.Empty(Directory.EnumerateFileSystemEntries(inner));
    }

    [Fact]
    public async Task Paste_MissingSource_IsReportedByName()
    {
        var ok = _tmp.File("src/ok.txt");
        var dst = _tmp.Dir("dst");

        var result = await Paste(dst, false, ConflictResolution.KeepBoth, _tmp.Combine("src", "ghost.txt"), ok);

        Assert.Equal(1, result.Succeeded);
        Assert.Equal(new[] { "ghost.txt" }, result.Errors);
    }

    [Fact]
    public async Task Paste_MoveFolderToAnotherVolume_CopiesThenDeletesSource()
    {
        // Memoria interna -> tarjeta SD: Directory.Move no cruza volumenes y la app copia y borra.
        // Aqui, de la carpeta temporal a la de salida de las pruebas si estan en otra unidad.
        var otherVolume = Path.Combine(AppContext.BaseDirectory, "xvol-" + Guid.NewGuid().ToString("N"));
        if (string.Equals(Path.GetPathRoot(otherVolume), Path.GetPathRoot(_tmp.Path), StringComparison.OrdinalIgnoreCase))
            return;

        Directory.CreateDirectory(otherVolume);
        try
        {
            _tmp.File("tree/sub/a.txt", "moved");

            var result = await Paste(otherVolume, true, ConflictResolution.KeepBoth, _tmp.Combine("tree"));

            Assert.Equal(1, result.Succeeded);
            Assert.Equal("moved", File.ReadAllText(Path.Combine(otherVolume, "tree", "sub", "a.txt")));
            Assert.False(Directory.Exists(_tmp.Combine("tree")));
        }
        finally
        {
            Directory.Delete(otherVolume, recursive: true);
        }
    }

    [Fact]
    public async Task Paste_MoveFileInUse_ReportsErrorAndKeepsTheSource()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var src = _tmp.File("src/busy.txt", "data");
        var dst = _tmp.Dir("dst");

        OperationResult result;
        using (File.Open(src, FileMode.Open, FileAccess.Read, FileShare.Read))
            result = await Paste(dst, true, ConflictResolution.KeepBoth, src);

        Assert.Equal(0, result.Succeeded);
        Assert.StartsWith("busy.txt:", Assert.Single(result.Errors));
        Assert.Equal("data", File.ReadAllText(src));
    }

    [Fact]
    public async Task Paste_Cancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var entry = new ClipboardEntry { Paths = new[] { _tmp.File("a") } };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _fs.PasteAsync(entry, _tmp.Dir("d"), ConflictResolution.KeepBoth, cts.Token));
    }
}
