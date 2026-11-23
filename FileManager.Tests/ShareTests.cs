using System.IO.Compression;
using FileManager.Helpers;

namespace FileManager.Tests;

/// <summary>Compartir: carpetas comprimidas en .zip y tipo MIME comun para varios ficheros.</summary>
public sealed class ShareTests : IDisposable
{
    private readonly TempFolder _tmp = new();

    public void Dispose() => _tmp.Dispose();

    private string Work => _tmp.Combine("cache", "share");

    [Fact]
    public void Prepare_FilesGoAsTheyAre()
    {
        var a = _tmp.File("a.txt");
        var b = _tmp.File("sub/b.jpg");

        var result = SharePreparer.Prepare(new[] { a, b }, Work);

        Assert.Equal(new[] { a, b }, result);
    }

    [Fact]
    public void Prepare_AFolder_BecomesAZipWithItsContents()
    {
        _tmp.File("Photos/one.jpg", "1");
        _tmp.File("Photos/inner/two.jpg", "22");
        _tmp.Dir("Photos/empty");

        var zip = Assert.Single(SharePreparer.Prepare(new[] { _tmp.Combine("Photos") }, Work));

        Assert.Equal(Path.Combine(Work, "Photos.zip"), zip);
        using var archive = ZipFile.OpenRead(zip);
        var names = archive.Entries.Select(e => e.FullName.Replace(Path.DirectorySeparatorChar == '/' ? '/' : (char)92, '/')).ToList();
        Assert.Contains("Photos/one.jpg", names);
        Assert.Contains("Photos/inner/two.jpg", names);
        Assert.Contains("Photos/empty/", names);
    }

    [Fact]
    public void Prepare_TwoFoldersWithTheSameName_GetDifferentZips()
    {
        var x = _tmp.Dir("A/Docs");
        var y = _tmp.Dir("B/Docs");
        _tmp.File("A/Docs/1.txt");

        var result = SharePreparer.Prepare(new[] { x + Path.DirectorySeparatorChar, y }, Work);

        Assert.Equal(new[] { Path.Combine(Work, "Docs.zip"), Path.Combine(Work, "Docs (2).zip") }, result);
        Assert.All(result, p => Assert.True(File.Exists(p)));
    }

    [Fact]
    public void Prepare_CleansTheZipsOfThePreviousTime()
    {
        var dir = _tmp.Dir("Old");
        var first = Assert.Single(SharePreparer.Prepare(new[] { dir }, Work));
        var file = _tmp.File("a.txt");

        SharePreparer.Prepare(new[] { file }, Work);

        Assert.False(File.Exists(first));
    }

    [Fact]
    public void Prepare_Missing_OrEmpty_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => SharePreparer.Prepare(new[] { _tmp.Combine("nope.txt") }, Work));
        Assert.Throws<ArgumentException>(() => SharePreparer.Prepare(Array.Empty<string>(), Work));
    }

    [Fact]
    public void Prepare_Cancelled_Throws()
    {
        var a = _tmp.File("a.txt");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => SharePreparer.Prepare(new[] { a }, Work, cts.Token));
    }

    [Theory]
    [InlineData("image/jpeg", "a.jpg", "b.JPEG")]
    [InlineData("image/*", "a.jpg", "b.png")]
    [InlineData("*/*", "a.jpg", "b.pdf")]
    [InlineData("application/zip", "Docs.zip")]
    [InlineData("application/*", "a.pdf", "b.zip")]
    [InlineData("*/*")]
    public void Common_IsTheTypeThatFitsAll(string expected, params string[] files) =>
        Assert.Equal(expected, MimeTypes.Common(files));
}
