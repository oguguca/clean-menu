namespace CleanMenu.Core.Processes;

public enum ProcessEndResult
{
    Ended,
    Protected,
    NotFound,
    ChangedIdentity,
    AccessDenied,
    StillRunning,
}

/// <summary>Actions on a running process. Ending is refused for protected Windows processes.</summary>
public interface IProcessActions
{
    string? GetExecutablePath(int pid);

    /// <summary>
    /// Ends the process only if it is not protected and the pid still belongs to a process named
    /// <paramref name="expectedName"/> (pids are reused by Windows).
    /// </summary>
    ProcessEndResult EndProcess(int pid, string expectedName);

    bool OpenFileLocation(string path);

    bool ShowFileProperties(string path);
}

public static class ProcessEndText
{
    public static string Describe(ProcessEndResult result) => result switch
    {
        ProcessEndResult.Ended => "Programa fechado.",
        ProcessEndResult.Protected => "Este processo faz parte do Windows e é protegido. Ele não foi fechado.",
        ProcessEndResult.NotFound => "O programa já tinha sido fechado.",
        ProcessEndResult.ChangedIdentity => "O processo mudou desde a última atualização. Nada foi fechado; atualize a lista.",
        ProcessEndResult.AccessDenied => "O Windows não permitiu fechar este processo.",
        _ => "O programa não respondeu e continua aberto.",
    };
}
