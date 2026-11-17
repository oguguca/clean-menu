using System.Runtime.InteropServices;
using CleanMenu.Core.Advanced;

namespace CleanMenu.Infrastructure.Advanced;

/// <summary>
/// Reads Windows' "Recent items" (the shortcuts Explorer keeps in %APPDATA%\Microsoft\Windows\Recent)
/// and resolves where each one points. Read-only: shortcuts are opened, never changed.
/// </summary>
public sealed class WindowsRecentFiles : IRecentFiles
{
    private const int MaxShortcuts = 3000;

    public IReadOnlySet<string> GetRecentlyOpened()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return result;
        }

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            return result;
        }

        object? shell = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return result;
            }

            var shortcuts = new DirectoryInfo(folder).EnumerateFiles("*.lnk")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(MaxShortcuts);

            foreach (var shortcut in shortcuts)
            {
                var target = ResolveTarget(shell, shortcut.FullName);
                if (!string.IsNullOrWhiteSpace(target))
                {
                    result.Add(target);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
        {
            // Best effort: the age rule still protects files when this list cannot be read.
        }
        finally
        {
            if (shell is not null && Marshal.IsComObject(shell))
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        return result;
    }

    private static string? ResolveTarget(dynamic shell, string shortcutPath)
    {
        object? link = null;
        try
        {
            link = shell.CreateShortcut(shortcutPath);
            return ((dynamic)link).TargetPath as string;
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            if (link is not null && Marshal.IsComObject(link))
            {
                Marshal.FinalReleaseComObject(link);
            }
        }
    }
}
