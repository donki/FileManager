// Doble en memoria de Microsoft.Maui.Storage.Preferences, con la misma firma que usa la app,
// para probar SettingsService sin MAUI ni dispositivo.
namespace Microsoft.Maui.Storage;

public static class Preferences
{
    private static readonly Dictionary<string, object?> Store = new();

    public static void Clear() => Store.Clear();

    public static bool ContainsKey(string key) => Store.ContainsKey(key);

    public static string Get(string key, string defaultValue) => Read(key, defaultValue);

    public static bool Get(string key, bool defaultValue) => Read(key, defaultValue);

    public static int Get(string key, int defaultValue) => Read(key, defaultValue);

    public static void Set(string key, string? value) => Store[key] = value;

    public static void Set(string key, bool value) => Store[key] = value;

    public static void Set(string key, int value) => Store[key] = value;

    private static T Read<T>(string key, T defaultValue) =>
        Store.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;
}
