namespace FileManager.Services;

/// <summary>
/// Aviso breve y no bloqueante tras una accion correcta. La implementacion es especifica
/// de Android y vive en Platforms/Android (constitucion 4).
/// </summary>
public interface IToastService
{
    void Show(string message);
}
