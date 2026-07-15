using FileManager.Models;

namespace FileManager.Services;

/// <inheritdoc cref="IFileClipboardService"/>
public class FileClipboardService : IFileClipboardService
{
    public ClipboardEntry? Current { get; private set; }

    public bool HasContent => Current is { Paths.Count: > 0 };

    public event EventHandler? Changed;

    public void Set(IReadOnlyList<string> paths, bool isMove)
    {
        Current = new ClipboardEntry { Paths = paths.ToList(), IsMove = isMove };
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        Current = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
