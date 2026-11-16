using System.Management;

namespace CleanMenu.Infrastructure.Wmi;

/// <summary>
/// Small read-only WMI helper. Every failure returns an empty result: missing data is shown as
/// "not reported", never invented.
/// </summary>
internal static class WmiReader
{
    public const string Cimv2 = @"root\cimv2";
    public const string Storage = @"root\Microsoft\Windows\Storage";

    public static IReadOnlyList<IReadOnlyDictionary<string, object?>> Query(string scope, string query)
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        try
        {
            using var searcher = new ManagementObjectSearcher(scope, query);
            using var results = searcher.Get();
            foreach (var item in results)
            {
                using (item)
                {
                    var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var property in item.Properties)
                    {
                        row[property.Name] = property.Value;
                    }

                    rows.Add(row);
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            // WMI unavailable, namespace missing or access denied.
        }

        return rows;
    }

    public static IReadOnlyDictionary<string, object?>? QueryFirst(string scope, string query)
    {
        var rows = Query(scope, query);
        return rows.Count > 0 ? rows[0] : null;
    }

    public static string? String(this IReadOnlyDictionary<string, object?> row, string name) =>
        row.TryGetValue(name, out var value) && value is not null && !string.IsNullOrWhiteSpace(value.ToString())
            ? value.ToString()!.Trim()
            : null;

    public static long? Long(this IReadOnlyDictionary<string, object?> row, string name)
    {
        if (!row.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        try
        {
            return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return null;
        }
    }

    public static int? Int(this IReadOnlyDictionary<string, object?> row, string name) =>
        row.Long(name) is { } value && value is >= int.MinValue and <= int.MaxValue ? (int)value : null;

    public static DateTimeOffset? Date(this IReadOnlyDictionary<string, object?> row, string name)
    {
        var text = row.String(name);
        if (text is null)
        {
            return null;
        }

        try
        {
            return new DateTimeOffset(ManagementDateTimeConverter.ToDateTime(text));
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
