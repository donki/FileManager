using System.ComponentModel;
using System.Globalization;
using FileManager.Helpers;
using FileManager.Models;
using FileManager.Services;

namespace FileManager.Tests;

public class SizeFormatterTests
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1, "1 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(1073741824, "1 GB")]
    [InlineData(1099511627776, "1 TB")]
    [InlineData(1125899906842624, "1024 TB")]
    public void Format_English(long bytes, string expected) =>
        Assert.Equal(expected, SizeFormatter.Format(bytes, En));

    [Fact]
    public void Format_UsesTheCultureDecimalSeparator() =>
        Assert.Equal("2,5 MB", SizeFormatter.Format(2621440, Es));

    [Fact]
    public void Format_RoundsToOneDecimal() =>
        Assert.Equal("1.2 KB", SizeFormatter.Format(1234, En));

    [Fact]
    public void Format_NegativeIsTreatedAsZero() =>
        Assert.Equal("0 B", SizeFormatter.Format(-5, En));
}

public class MimeTypesTests
{
    [Theory]
    [InlineData("/x/photo.JPG", "image/jpeg")]
    [InlineData("doc.pdf", "application/pdf")]
    [InlineData("a.b.c.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("app.apk", "application/vnd.android.package-archive")]
    [InlineData("song.m4a", "audio/mp4")]
    [InlineData("notes.md", "text/markdown")]
    [InlineData("archive.7z", "application/x-7z-compressed")]
    public void ForPath_KnownExtensions(string path, string expected) =>
        Assert.Equal(expected, MimeTypes.ForPath(path));

    [Theory]
    [InlineData("noextension")]
    [InlineData("weird.xyz")]
    [InlineData("trailingdot.")]
    [InlineData("")]
    public void ForPath_UnknownOrMissingExtension_IsOctetStream(string path) =>
        Assert.Equal(MimeTypes.Default, MimeTypes.ForPath(path));
}

public class FileCategoriesTests
{
    [Theory]
    [InlineData("jpg", FileCategory.Image)]
    [InlineData("heic", FileCategory.Image)]
    [InlineData("mkv", FileCategory.Video)]
    [InlineData("opus", FileCategory.Audio)]
    [InlineData("pdf", FileCategory.Document)]
    [InlineData("csv", FileCategory.Document)]
    [InlineData("xapk", FileCategory.Apk)]
    [InlineData("tgz", FileCategory.Archive)]
    [InlineData("exe", FileCategory.Other)]
    [InlineData("", FileCategory.Other)]
    public void Of_ClassifiesExtension(string extension, FileCategory expected) =>
        Assert.Equal(expected, FileCategories.Of(extension));

    [Fact]
    public void Of_ExpectsLowerCase_AsFileItemProvides() =>
        Assert.Equal(FileCategory.Other, FileCategories.Of("JPG"));
}

public class FileItemTests
{
    [Theory]
    [InlineData("Photo.JPG", "jpg", FileCategory.Image)]
    [InlineData("archive.tar.gz", "gz", FileCategory.Archive)]
    [InlineData("README", "", FileCategory.Other)]
    [InlineData(".bashrc", "bashrc", FileCategory.Other)]
    public void Extension_And_Category_ForFiles(string name, string extension, FileCategory category)
    {
        var item = new FileItem { Name = name };
        Assert.Equal(extension, item.Extension);
        Assert.Equal(category, item.Category);
    }

    [Fact]
    public void Folder_HasNoExtension_AndFolderCategory()
    {
        var item = new FileItem { Name = "photos.jpg", IsDirectory = true };
        Assert.Equal(string.Empty, item.Extension);
        Assert.Equal(FileCategory.Folder, item.Category);
    }

    [Fact]
    public void Defaults()
    {
        var item = new FileItem();
        Assert.Equal("ic_file_generic.png", item.Icon);
        Assert.Equal(string.Empty, item.Details);
        Assert.False(item.IsSelected);
    }

    [Fact]
    public void IsSelected_NotifiesOnlyOnRealChanges()
    {
        var item = new FileItem { Name = "a" };
        var raised = new List<string?>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        item.IsSelected = true;
        item.IsSelected = true;
        item.IsSelected = false;

        Assert.Equal(new[] { "IsSelected", "IsSelected" }, raised);
        Assert.False(item.IsSelected);
    }

    [Fact]
    public void MutableDisplayProperties_AreSettable()
    {
        var item = new FileItem { Name = "a.txt" };
        item.Icon = "x.png";
        item.Details = "1 KB";
        Assert.Equal("x.png", item.Icon);
        Assert.Equal("1 KB", item.Details);
    }
}

public class FileIconsTests
{
    [Theory]
    [InlineData("a.PNG", "ic_file_image.png")]
    [InlineData("a.mp4", "ic_file_video.png")]
    [InlineData("a.flac", "ic_file_audio.png")]
    [InlineData("a.pdf", "ic_file_pdf.png")]
    [InlineData("a.xlsx", "ic_file_sheet.png")]
    [InlineData("a.pptx", "ic_file_slides.png")]
    [InlineData("a.epub", "ic_file_ebook.png")]
    [InlineData("a.html", "ic_file_web.png")]
    [InlineData("a.cs", "ic_file_code.png")]
    [InlineData("a.zip", "ic_file_archive.png")]
    [InlineData("a.apk", "ic_file_apk.png")]
    [InlineData("a.txt", "ic_file_document.png")]
    [InlineData("a.unknown", FileIcons.Generic)]
    [InlineData("noext", FileIcons.Generic)]
    public void For_File(string name, string expected) =>
        Assert.Equal(expected, FileIcons.For(new FileItem { Name = name }));

    [Fact]
    public void For_Folder_EvenWithExtensionInName() =>
        Assert.Equal(FileIcons.Folder, FileIcons.For(new FileItem { Name = "pics.png", IsDirectory = true }));
}

public class FileClipboardServiceTests
{
    [Fact]
    public void StartsEmpty()
    {
        var clipboard = new FileClipboardService();
        Assert.Null(clipboard.Current);
        Assert.False(clipboard.HasContent);
    }

    [Fact]
    public void Set_StoresACopyOfThePaths_AndRaisesChanged()
    {
        var clipboard = new FileClipboardService();
        var changes = 0;
        clipboard.Changed += (_, _) => changes++;
        var paths = new List<string> { "/a", "/b" };

        clipboard.Set(paths, isMove: true);
        paths.Add("/c");

        Assert.True(clipboard.HasContent);
        Assert.True(clipboard.Current!.IsMove);
        Assert.Equal(new[] { "/a", "/b" }, clipboard.Current.Paths);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Set_WithNoPaths_HasNoContent()
    {
        var clipboard = new FileClipboardService();
        clipboard.Set(Array.Empty<string>(), isMove: false);
        Assert.NotNull(clipboard.Current);
        Assert.False(clipboard.HasContent);
    }

    [Fact]
    public void Clear_EmptiesAndRaisesChanged()
    {
        var clipboard = new FileClipboardService();
        var changes = 0;
        clipboard.Set(new[] { "/a" }, false);
        clipboard.Changed += (_, _) => changes++;

        clipboard.Clear();

        Assert.Null(clipboard.Current);
        Assert.False(clipboard.HasContent);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void ClipboardEntry_Defaults()
    {
        var entry = new ClipboardEntry();
        Assert.Empty(entry.Paths);
        Assert.False(entry.IsMove);
    }
}
