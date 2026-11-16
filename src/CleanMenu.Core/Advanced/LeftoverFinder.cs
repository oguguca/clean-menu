using System.Text;

namespace CleanMenu.Core.Advanced;

/// <summary>A folder in AppData that is not used by any installed program (candidate).</summary>
public sealed record LeftoverCandidate(string Path, string Name, DateTimeOffset LastWriteUtc);

/// <summary>A program listed in Windows "Installed apps".</summary>
public sealed record InstalledProgram(string Name, string? Publisher);

/// <summary>Reads installed programs and AppData folders (read-only).</summary>
public interface ILeftoverSource
{
    IReadOnlyList<InstalledProgram> GetInstalledPrograms();

    /// <summary>Top-level folders of %APPDATA% and %LOCALAPPDATA%.</summary>
    IReadOnlyList<LeftoverCandidate> GetAppDataFolders();
}

/// <summary>
/// Finds AppData folders that probably belong to programs that were uninstalled: the folder name
/// matches no installed program or publisher, it is not a known Windows folder, and it has not been
/// changed for a long time. These are only suggestions; the user decides.
/// </summary>
public static class LeftoverFinder
{
    public static readonly TimeSpan MinimumIdle = TimeSpan.FromDays(180);

    private static readonly HashSet<string> KnownSystemFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft", "Packages", "Temp", "Programs", "Comms", "ConnectedDevicesPlatform", "D3DSCache",
        "CrashDumps", "Publishers", "PlaceholderTileLogoFolder", "VirtualStore", "History", "IsolatedStorage",
        "Adobe", "Google", "Mozilla", "NVIDIA", "NVIDIA Corporation", "AMD", "Intel", "Apple", "Apple Computer",
        "Sun", "Oracle", "Package Cache", "PeerDistRepub", "speech", "fontconfig", "pip", "npm", "npm-cache",
        "Application Data", "Microsoft_Corporation", "CEF", "Diagnostics", "Windows", "Identities", "SquirrelTemp",
        "assembly", "cache", "ElevatedDiagnostics", "DBG", "Symbols", "SolidDocuments", "Downloaded Installations",
    };

    public static IReadOnlyList<LeftoverCandidate> Find(
        IReadOnlyList<LeftoverCandidate> folders,
        IReadOnlyList<InstalledProgram> installed,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(installed);

        var installedKeys = installed
            .SelectMany(p => new[] { p.Name, p.Publisher })
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => Normalize(s!))
            .Where(s => s.Length >= 3)
            .ToList();

        return folders
            .Where(f => !KnownSystemFolders.Contains(f.Name) && !f.Name.StartsWith('.'))
            .Where(f => now - f.LastWriteUtc >= MinimumIdle)
            .Where(f => !MatchesInstalled(Normalize(f.Name), installedKeys))
            .OrderBy(f => f.LastWriteUtc)
            .ToList();
    }

    internal static bool MatchesInstalled(string folderKey, IReadOnlyList<string> installedKeys) =>
        folderKey.Length < 3 || installedKeys.Any(k => k.Contains(folderKey, StringComparison.Ordinal) || folderKey.Contains(k, StringComparison.Ordinal));

    /// <summary>Lowercase letters and digits only ("Epic Games, Inc." → "epicgamesinc").</summary>
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var sb = new StringBuilder(value.Length);
        foreach (var c in value.Normalize(NormalizationForm.FormD))
        {
            if (char.IsLetterOrDigit(c) && c < 128)
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString();
    }
}
