using System.Diagnostics;
using CleanMenu.Core.Startup;
using Microsoft.Win32;

namespace CleanMenu.Infrastructure.Startup;

/// <summary>
/// Startup programs from the registry Run keys, the Startup folders and logon scheduled tasks.
/// Enabling/disabling writes the StartupApproved value under HKCU, exactly like Task Manager and
/// Windows Settings do, so the change is reversible and nothing is ever deleted.
/// </summary>
public sealed class WindowsStartupManager(TimeProvider time) : IStartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKey32 = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRoot = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    public Task<IReadOnlyList<StartupEntry>> GetEntriesAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<StartupEntry>>(() =>
        {
            var entries = new List<StartupEntry>();
            entries.AddRange(ReadRun(Registry.CurrentUser, RunKey, "Run", StartupSource.RegistryUser));
            entries.AddRange(ReadRun(Registry.LocalMachine, RunKey, "Run", StartupSource.RegistryMachine));
            entries.AddRange(ReadRun(Registry.LocalMachine, RunKey32, "Run32", StartupSource.RegistryMachine));
            entries.AddRange(ReadFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), Registry.CurrentUser, StartupSource.StartupFolderUser));
            entries.AddRange(ReadFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), Registry.LocalMachine, StartupSource.StartupFolderCommon));
            cancellationToken.ThrowIfCancellationRequested();
            entries.AddRange(LogonTaskReader.Read());
            return entries.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }, cancellationToken);

    public void SetEnabled(StartupEntry entry, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!entry.CanToggle)
        {
            throw new InvalidOperationException("Este item precisa de permissão de administrador para ser alterado.");
        }

        var subKey = entry.Source == StartupSource.RegistryUser ? "Run" : "StartupFolder";
        using var key = Registry.CurrentUser.CreateSubKey($@"{ApprovedRoot}\{subKey}", writable: true);
        key.SetValue(entry.Name, StartupCommandParser.BuildApprovedValue(enabled, time.GetUtcNow()), RegistryValueKind.Binary);
    }

    private static List<StartupEntry> ReadRun(RegistryKey hive, string path, string approvedName, StartupSource source)
    {
        var result = new List<StartupEntry>();
        try
        {
            using var key = hive.OpenSubKey(path);
            using var approved = hive.OpenSubKey($@"{ApprovedRoot}\{approvedName}");
            if (key is null)
            {
                return result;
            }

            foreach (var name in key.GetValueNames().Where(n => !string.IsNullOrEmpty(n)))
            {
                var command = key.GetValue(name)?.ToString() ?? string.Empty;
                var exe = StartupCommandParser.ExtractExecutable(command);
                result.Add(new StartupEntry(name, command, exe, Publisher(exe), source,
                    StartupCommandParser.IsApproved(approved?.GetValue(name) as byte[])));
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Key not readable for this user.
        }

        return result;
    }

    private static List<StartupEntry> ReadFolder(string folder, RegistryKey approvedHive, StartupSource source)
    {
        var result = new List<StartupEntry>();
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return result;
        }

        try
        {
            using var approved = approvedHive.OpenSubKey($@"{ApprovedRoot}\StartupFolder");
            foreach (var file in Directory.EnumerateFiles(folder).Where(f => !Path.GetFileName(f).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)))
            {
                var name = Path.GetFileName(file);
                var target = Path.GetExtension(file).Equals(".lnk", StringComparison.OrdinalIgnoreCase) ? ShortcutTarget(file) : file;
                result.Add(new StartupEntry(name, target ?? file, target ?? file, Publisher(target), source,
                    StartupCommandParser.IsApproved(approved?.GetValue(name) as byte[])));
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
        }

        return result;
    }

    private static string? ShortcutTarget(string lnkPath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return null;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(lnkPath);
            string target = shortcut.TargetPath;
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or InvalidCastException)
        {
            return null;
        }
    }

    internal static string? Publisher(string? exePath)
    {
        try
        {
            return exePath is not null && File.Exists(exePath) && FileVersionInfo.GetVersionInfo(exePath).CompanyName is { Length: > 0 } company
                ? company.Trim()
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
