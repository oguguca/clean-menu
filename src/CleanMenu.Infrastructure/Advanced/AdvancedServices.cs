using System.Security.Cryptography;
using CleanMenu.Core.Advanced;
using Microsoft.Win32;

namespace CleanMenu.Infrastructure.Advanced;

/// <summary>Streaming SHA-256; the file is opened read-only and shared with other programs.</summary>
public sealed class Sha256FileHasher : IFileHasher
{
    public async Task<string?> Sha256Async(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16, useAsync: true);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            return Convert.ToHexString(hash);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>System folders that Limpeza Avançada never scans or recycles from.</summary>
public static class AdvancedProtectedFolders
{
    public static IReadOnlyList<string> Get() =>
        new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            // Everything programs keep per user (VS Code, Discord, Spotify, games, browsers...).
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData"),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        }
        .Concat(InstallLocations())
        .Where(f => !string.IsNullOrWhiteSpace(f) && Path.IsPathFullyQualified(f))
        .Select(Path.TrimEndingDirectorySeparator)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    /// <summary>Folders where installed programs live, from the Uninstall registry keys (InstallLocation).</summary>
    private static List<string> InstallLocations()
    {
        var result = new List<string>();
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var keyPath in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
            {
                try
                {
                    using var key = hive.OpenSubKey(keyPath);
                    foreach (var name in key?.GetSubKeyNames() ?? [])
                    {
                        using var app = key!.OpenSubKey(name);
                        if (app?.GetValue("InstallLocation") is string location && location.Trim().Trim('"').Length > 3)
                        {
                            result.Add(location.Trim().Trim('"'));
                        }
                    }
                }
                catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
                {
                }
            }
        }

        return result;
    }
}

/// <summary>Installed programs (Uninstall registry keys) and AppData folders. Read-only.</summary>
public sealed class WindowsLeftoverSource : ILeftoverSource
{
    private static readonly string[] UninstallKeys =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    ];

    public IReadOnlyList<InstalledProgram> GetInstalledPrograms()
    {
        var programs = new List<InstalledProgram>();
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var path in UninstallKeys)
            {
                try
                {
                    using var key = hive.OpenSubKey(path);
                    if (key is null)
                    {
                        continue;
                    }

                    foreach (var name in key.GetSubKeyNames())
                    {
                        using var app = key.OpenSubKey(name);
                        if (app?.GetValue("DisplayName") is string displayName && !string.IsNullOrWhiteSpace(displayName))
                        {
                            programs.Add(new InstalledProgram(displayName, app.GetValue("Publisher") as string));
                        }
                    }
                }
                catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
                {
                }
            }
        }

        return programs;
    }

    public IReadOnlyList<LeftoverCandidate> GetAppDataFolders()
    {
        var result = new List<LeftoverCandidate>();
        foreach (var root in new[] { Environment.SpecialFolder.ApplicationData, Environment.SpecialFolder.LocalApplicationData })
        {
            var folder = Environment.GetFolderPath(root);
            try
            {
                foreach (var dir in new DirectoryInfo(folder).EnumerateDirectories("*", new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System, IgnoreInaccessible = true }))
                {
                    result.Add(new LeftoverCandidate(dir.FullName, dir.Name, new DateTimeOffset(dir.LastWriteTimeUtc, TimeSpan.Zero)));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return result;
    }
}
