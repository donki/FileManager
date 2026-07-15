using FileManager.Helpers;
using Microsoft.Extensions.Logging;

namespace FileManager.Services;

/// <inheritdoc cref="IFileActionsService"/>
public class FileActionsService : IFileActionsService
{
    private readonly ILogger<FileActionsService> _logger;

    public FileActionsService(ILogger<FileActionsService> logger) => _logger = logger;

    public async Task<bool> OpenAsync(string path)
    {
        var request = new OpenFileRequest
        {
            File = new ReadOnlyFile(path, MimeTypes.ForPath(path))
        };

        try
        {
            await Launcher.Default.OpenAsync(request);
            return true;
        }
        catch (Exception ex)
        {
            // Android lanza ActivityNotFoundException cuando no hay ninguna app registrada
            // para el tipo de fichero; el llamante lo traduce a un mensaje para el usuario.
            _logger.LogInformation("No app available to open the file: {Message}", ex.Message);
            return false;
        }
    }

    public Task ShareAsync(string path, string title) =>
        Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(path, MimeTypes.ForPath(path))
        });
}
