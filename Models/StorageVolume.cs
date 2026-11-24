namespace FileManager.Models;

/// <summary>
/// Una memoria del dispositivo que se puede explorar: la interna (siempre la primera) o una
/// extraible, como la tarjeta SD.
/// </summary>
public sealed record StorageVolume(string RootPath, bool IsRemovable);
