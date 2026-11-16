namespace CleanMenu.Core.Startup;

/// <summary>
/// Extracts the program path from a startup command line such as
/// <c>"C:\Program Files\App\app.exe" --minimized</c> or <c>C:\Tools\app.exe -silent</c>.
/// </summary>
public static class StartupCommandParser
{
    private static readonly string[] ExecutableExtensions = [".exe", ".com", ".bat", ".cmd", ".lnk", ".vbs", ".ps1", ".msc", ".dll"];

    public static string? ExtractExecutable(string? command, Func<string, string>? expandEnvironment = null)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var text = (expandEnvironment ?? Environment.ExpandEnvironmentVariables)(command.Trim());

        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            return end > 1 ? text[1..end] : null;
        }

        // Unquoted: take the shortest prefix that ends with a known executable extension.
        foreach (var extension in ExecutableExtensions)
        {
            var index = text.IndexOf(extension, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                var endIndex = index + extension.Length;
                if (endIndex == text.Length || text[endIndex] == ' ' || text[endIndex] == ',')
                {
                    return text[..endIndex];
                }

                index = text.IndexOf(extension, endIndex, StringComparison.OrdinalIgnoreCase);
            }
        }

        var space = text.IndexOf(' ', StringComparison.Ordinal);
        return space > 0 ? text[..space] : text;
    }

    /// <summary>
    /// StartupApproved values are 12 bytes; the first byte is even (0x02, 0x06) when enabled and
    /// odd (0x03, 0x07) when disabled. A missing value means enabled.
    /// </summary>
    public static bool IsApproved(byte[]? startupApprovedValue) =>
        startupApprovedValue is not { Length: > 0 } || (startupApprovedValue[0] & 0x01) == 0;

    /// <summary>Builds a StartupApproved value: state byte, three zero bytes, then the change time (FILETIME).</summary>
    public static byte[] BuildApprovedValue(bool enabled, DateTimeOffset changedAt)
    {
        var value = new byte[12];
        value[0] = enabled ? (byte)0x02 : (byte)0x03;
        if (!enabled)
        {
            BitConverter.GetBytes(changedAt.ToFileTime()).CopyTo(value, 4);
        }

        return value;
    }
}
