namespace CleanMenu.Core.Processes;

/// <summary>What a process is, in terms a non-technical user can act on.</summary>
public enum ProcessKind
{
    /// <summary>Essential to Windows. Must never be closed by Clean Menu.</summary>
    CriticalSystem,

    /// <summary>Part of Windows but not essential; closing it is possible but not recommended.</summary>
    WindowsComponent,

    /// <summary>A program with a window the user opened.</summary>
    UserApp,

    /// <summary>A program running without a window (updaters, sync tools, drivers' helpers).</summary>
    Background,
}

/// <summary>
/// Classifies processes by name. The critical list is deliberately conservative: when in doubt a
/// Windows process is treated as critical.
/// </summary>
public static class ProcessClassifier
{
    private static readonly HashSet<string> Critical = new(StringComparer.OrdinalIgnoreCase)
    {
        "Idle", "System", "Registry", "Memory Compression", "MemCompression", "Secure System",
        "smss", "csrss", "wininit", "winlogon", "services", "lsass", "lsaiso", "svchost",
        "dwm", "fontdrvhost", "LsaIso", "WUDFHost", "spoolsv", "audiodg", "conhost",
        "MsMpEng", "NisSrv", "SecurityHealthService", "SgrmBroker", "MpDefenderCoreService",
        "wlanext", "dasHost", "sihost", "taskhostw", "ctfmon", "explorer", "StartMenuExperienceHost",
        "ShellExperienceHost", "SearchHost", "SearchIndexer", "TextInputHost", "RuntimeBroker",
        "WmiPrvSE", "dllhost", "CompPkgSrv", "LogonUI", "userinit", "smartscreen",
    };

    private static readonly HashSet<string> WindowsComponents = new(StringComparer.OrdinalIgnoreCase)
    {
        "SearchApp", "Widgets", "WidgetService", "PhoneExperienceHost", "YourPhone", "GameBar",
        "GameBarFTServer", "SystemSettings", "ApplicationFrameHost", "backgroundTaskHost",
        "SecurityHealthSystray", "OneDrive", "msedgewebview2", "Microsoft.Photos", "LockApp",
        "CrossDeviceService", "CrossDeviceResume", "UserOOBEBroker", "SearchProtocolHost", "SearchFilterHost",
    };

    public static ProcessKind Classify(string name, bool hasWindow)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (Critical.Contains(name))
        {
            return ProcessKind.CriticalSystem;
        }

        if (WindowsComponents.Contains(name))
        {
            return ProcessKind.WindowsComponent;
        }

        return hasWindow ? ProcessKind.UserApp : ProcessKind.Background;
    }

    /// <summary>True when Clean Menu must refuse to end the process.</summary>
    public static bool IsProtected(string name, int pid) =>
        pid is 0 or 4 || Critical.Contains(name ?? string.Empty);

    /// <summary>Plain advice shown next to a process.</summary>
    public static string Advice(ProcessKind kind) => kind switch
    {
        ProcessKind.CriticalSystem => "Faz parte do Windows. Não feche.",
        ProcessKind.WindowsComponent => "Recurso do Windows. Pode fechar, mas ele pode voltar a abrir sozinho.",
        ProcessKind.UserApp => "Programa aberto por você. Se não estiver usando, pode fechar normalmente.",
        _ => "Roda em segundo plano. Se não reconhecer, pesquise o nome antes de fechar.",
    };
}
