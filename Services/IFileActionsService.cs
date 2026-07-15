namespace FileManager.Services;

/// <summary>Abrir y compartir ficheros con las aplicaciones del dispositivo.</summary>
public interface IFileActionsService
{
    /// <summary>Abre el fichero con la aplicacion asociada del sistema.</summary>
    /// <returns><c>false</c> si ninguna aplicacion puede abrir ese tipo de fichero.</returns>
    Task<bool> OpenAsync(string path);

    Task ShareAsync(string path, string title);
}
