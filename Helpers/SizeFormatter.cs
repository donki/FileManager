using System.Globalization;

namespace FileManager.Helpers;

/// <summary>Formatea tamanos en bytes usando la cultura activa (constitucion 15).</summary>
public static class SizeFormatter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string Format(long bytes, CultureInfo culture)
    {
        if (bytes < 0)
            bytes = 0;

        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        // Los bytes se muestran enteros; a partir de KB, con un decimal.
        var number = unit == 0
            ? value.ToString("0", culture)
            : value.ToString("0.#", culture);

        return $"{number} {Units[unit]}";
    }
}
