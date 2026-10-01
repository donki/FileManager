using System.Net;
using FileManager.Models;
using FileManager.Services;
using FileManager.ViewModels;

namespace FileManager.Tests.Fakes;

/// <summary>Preferencias en memoria.</summary>
public sealed class MemorySettings : ISettingsService
{
    public string Language { get; set; } = "en";
    public bool ShowHiddenFiles { get; set; }
    public bool ConfirmDelete { get; set; } = true;
    public SortMode Sort { get; set; }
}

/// <summary>
/// Sistema de ficheros real (FileSystemService) con la raiz en una carpeta temporal y errores que se
/// pueden provocar a voluntad.
/// </summary>
public sealed class RootedFileSystem(string root) : IFileSystemService
{
    private readonly FileSystemService _real = new(Microsoft.Extensions.Logging.Abstractions.NullLogger<FileSystemService>.Instance);

    public Exception? ListError { get; set; }
    public Exception? SearchError { get; set; }
    public Exception? CreateError { get; set; }
    public Exception? RenameError { get; set; }
    public Exception? PasteError { get; set; }
    public string? DeleteErrorFor { get; set; }
    public TaskCompletionSource? SearchGate { get; set; }
    public int ListCalls { get; private set; }
    public int SearchCalls { get; private set; }

    public string RootPath { get; set; } = root;

    public async Task<IReadOnlyList<FileItem>> ListAsync(string path, bool showHidden, SortMode sort, CancellationToken cancellationToken = default)
    {
        ListCalls++;
        if (ListError is { } e)
            throw e;
        return await _real.ListAsync(path, showHidden, sort, cancellationToken);
    }

    public async Task<IReadOnlyList<FileItem>> SearchAsync(string path, string query, bool showHidden, CancellationToken cancellationToken = default)
    {
        SearchCalls++;
        if (SearchGate is { } gate)
            await gate.Task;
        if (SearchError is { } e)
            throw e;
        return await _real.SearchAsync(path, query, showHidden, cancellationToken);
    }

    public int CountEntries(string directoryPath) => _real.CountEntries(directoryPath);

    public NameValidation ValidateName(string? name, string parentPath, string? currentPath = null) =>
        _real.ValidateName(name, parentPath, currentPath);

    public Task<string> CreateDirectoryAsync(string parentPath, string name) =>
        CreateError is { } e ? Task.FromException<string>(e) : _real.CreateDirectoryAsync(parentPath, name);

    public Task<string> RenameAsync(string path, string newName) =>
        RenameError is { } e ? Task.FromException<string>(e) : _real.RenameAsync(path, newName);

    public async Task<OperationResult> DeleteAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        var result = await _real.DeleteAsync(paths.Where(p => p != DeleteErrorFor).ToList(), cancellationToken);
        if (DeleteErrorFor is not null && paths.Contains(DeleteErrorFor))
            result.Errors.Add("boom: " + Path.GetFileName(DeleteErrorFor));
        return result;
    }

    public IReadOnlyList<string> GetConflicts(IReadOnlyList<string> paths, string destination) =>
        _real.GetConflicts(paths, destination);

    public Task<OperationResult> PasteAsync(ClipboardEntry entry, string destination, ConflictResolution resolution, CancellationToken cancellationToken = default) =>
        PasteError is { } e ? Task.FromException<OperationResult>(e) : _real.PasteAsync(entry, destination, resolution, cancellationToken);

    public bool IsInside(string path, string destination) => _real.IsInside(path, destination);
}

/// <summary>Dialogos que contestan solos, en orden, y apuntan lo que se ha preguntado.</summary>
public sealed class ScriptedDialogs : IDialogService
{
    private readonly Queue<object?> _answers = new();

    public List<(string Kind, string? Title, string? Message, string[] Options)> Calls { get; } = new();

    public ScriptedDialogs Answer(params object?[] answers)
    {
        foreach (var a in answers)
            _answers.Enqueue(a);
        return this;
    }

    public (string Kind, string? Title, string? Message, string[] Options) Last => Calls[^1];

    public IEnumerable<string?> Messages => Calls.Select(c => c.Message);

    public Task<bool> AlertAsync(string title, string message, string accept, string? cancel = null)
    {
        Calls.Add(("alert", title, message, cancel is null ? new[] { accept } : new[] { accept, cancel }));
        if (cancel is null)
            return Task.FromResult(true);
        return Task.FromResult(_answers.Count > 0 ? (bool)_answers.Dequeue()! : true);
    }

    public Task<string?> ActionSheetAsync(string? title, string cancel, params string[] options)
    {
        Calls.Add(("sheet", title, null, options));
        return Task.FromResult(_answers.Count > 0 ? (string?)_answers.Dequeue() : cancel);
    }

    public Task<string?> PromptAsync(string title, string? message, string accept, string cancel, string? initialValue = null)
    {
        Calls.Add(("prompt", title, initialValue, Array.Empty<string>()));
        return Task.FromResult(_answers.Count > 0 ? (string?)_answers.Dequeue() : null);
    }
}

public sealed class FakeView : IMainView
{
    public int Focused, Unfocused, SettingsOpened, AboutOpened;
    public void FocusSearch() => Focused++;
    public void UnfocusSearch() => Unfocused++;
    public Task OpenSettingsAsync() { SettingsOpened++; return Task.CompletedTask; }
    public Task OpenAboutAsync() { AboutOpened++; return Task.CompletedTask; }
}

public sealed class FakeToast : IToastService
{
    public List<string> Shown { get; } = new();
    public void Show(string message) => Shown.Add(message);
}

public sealed class FakePermissions : IStoragePermissionService
{
    public bool HasFullAccess { get; set; } = true;
    public Exception? RequestError { get; set; }
    public int Requests { get; private set; }

    public Task RequestFullAccessAsync()
    {
        Requests++;
        return RequestError is { } e ? Task.FromException(e) : Task.CompletedTask;
    }
}

public sealed class FakeActions : IFileActionsService
{
    public bool OpenResult { get; set; } = true;
    public Exception? OpenError { get; set; }
    public Exception? ShareError { get; set; }
    public List<string> Opened { get; } = new();
    public List<(string Path, string Title)> Shared { get; } = new();

    public Task<bool> OpenAsync(string path)
    {
        if (OpenError is { } e)
            throw e;
        Opened.Add(path);
        return Task.FromResult(OpenResult);
    }

    public Task ShareAsync(string path, string title)
    {
        if (ShareError is { } e)
            throw e;
        Shared.Add((path, title));
        return Task.CompletedTask;
    }
}

public sealed class FakeEnvironment : IAppEnvironment
{
    public string VersionString { get; set; } = "2026.10.01.0";
    public bool EmailAvailable { get; set; } = true;
    public Exception? EmailError { get; set; }
    public List<Uri> Opened { get; } = new();
    public List<(string Subject, string To)> Emails { get; } = new();

    public Task OpenUrlAsync(Uri uri)
    {
        Opened.Add(uri);
        return Task.CompletedTask;
    }

    public Task<bool> ComposeEmailAsync(string subject, string to)
    {
        if (EmailError is { } e)
            throw e;
        Emails.Add((subject, to));
        return Task.FromResult(EmailAvailable);
    }
}

/// <summary>Respuesta HTTP fija (sin red).</summary>
public sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<Uri?> Requests { get; } = new();

    public static StubHttpHandler Json(string json) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri);
        return Task.FromResult(respond(request));
    }
}
