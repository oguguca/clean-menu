using System.Globalization;

namespace CleanMenu.Core.Formatting;

/// <summary>
/// Human-friendly byte sizes in Brazilian Portuguese ("1,5 GB"). Uses binary units (1 KB = 1024 bytes),
/// the same convention as Windows Explorer.
/// </summary>
public static class ByteSize
{
    private static readonly string[] Units = ["bytes", "KB", "MB", "GB", "TB", "PB"];
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Format(long bytes)
    {
        if (bytes < 0)
        {
            return "-" + Format(-bytes);
        }

        if (bytes < 1024)
        {
            return bytes == 1 ? "1 byte" : $"{bytes.ToString(PtBr)} bytes";
        }

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var format = value >= 100 ? "0" : "0.#";
        return $"{value.ToString(format, PtBr)} {Units[unit]}";
    }

    public static string Format(ulong bytes) => Format(bytes > long.MaxValue ? long.MaxValue : (long)bytes);
}
