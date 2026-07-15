namespace FileManager.Services;

/// <summary>
/// Acceso al almacenamiento del dispositivo. La implementacion es especifica de Android
/// y vive en Platforms/Android (constitucion 4).
/// </summary>
public interface IStoragePermissionService
{
    /// <summary>La aplicacion puede leer y escribir en el almacenamiento del dispositivo.</summary>
    bool HasFullAccess { get; }

    /// <summary>
    /// Abre los ajustes del sistema donde el usuario concede el acceso a todos los ficheros.
    /// Android no devuelve el resultado: hay que volver a consultar <see cref="HasFullAccess"/>
    /// cuando la aplicacion recupera el foco.
    /// </summary>
    Task RequestFullAccessAsync();
}
