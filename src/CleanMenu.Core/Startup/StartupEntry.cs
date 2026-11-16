namespace CleanMenu.Core.Startup;

/// <summary>Where a program that starts with Windows is registered.</summary>
public enum StartupSource
{
    /// <summary>HKCU\...\Run: this user only. Can be toggled without admin rights.</summary>
    RegistryUser,

    /// <summary>HKLM\...\Run: all users. Changing it needs admin rights.</summary>
    RegistryMachine,

    /// <summary>The user's Startup folder.</summary>
    StartupFolderUser,

    /// <summary>The Startup folder shared by all users. Changing it needs admin rights.</summary>
    StartupFolderCommon,

    /// <summary>A scheduled task that runs at logon. Shown for information.</summary>
    ScheduledTask,
}

/// <summary>A program that starts automatically.</summary>
/// <param name="Name">Registry value name, shortcut name or task name.</param>
/// <param name="Command">Full command line as registered.</param>
/// <param name="ExecutablePath">Program file extracted from the command, when it could be found.</param>
/// <param name="Publisher">Company from the file's version information, when available.</param>
/// <param name="Source">Where it is registered.</param>
/// <param name="IsEnabled">Whether Windows will start it at logon.</param>
public sealed record StartupEntry(
    string Name,
    string Command,
    string? ExecutablePath,
    string? Publisher,
    StartupSource Source,
    bool IsEnabled)
{
    /// <summary>Only per-user entries can be changed without admin rights.</summary>
    public bool CanToggle => Source is StartupSource.RegistryUser or StartupSource.StartupFolderUser;

    public string SourceText => Source switch
    {
        StartupSource.RegistryUser => "Registro do Windows (só você)",
        StartupSource.RegistryMachine => "Registro do Windows (todos os usuários)",
        StartupSource.StartupFolderUser => "Pasta Inicializar (só você)",
        StartupSource.StartupFolderCommon => "Pasta Inicializar (todos os usuários)",
        _ => "Tarefa agendada",
    };
}

/// <summary>Reads and toggles startup entries. Never deletes them.</summary>
public interface IStartupManager
{
    Task<IReadOnlyList<StartupEntry>> GetEntriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables an entry the same way Windows Task Manager does (StartupApproved),
    /// so the change is reversible and visible in Windows Settings too.
    /// </summary>
    void SetEnabled(StartupEntry entry, bool enabled);
}
