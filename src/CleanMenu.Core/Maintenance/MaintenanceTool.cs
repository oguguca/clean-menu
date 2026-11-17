using System.Text.RegularExpressions;

namespace CleanMenu.Core.Maintenance;

/// <summary>
/// Windows maintenance commands Clean Menu can start. This is a closed list: no free-form command
/// is ever executed. Each runs in its own visible window; administrator rights are requested
/// through the normal Windows prompt (UAC) only for commands that need them.
/// </summary>
public enum MaintenanceTool
{
    SystemFileCheck,
    DismScanHealth,
    DismRestoreHealth,
    CheckDiskScan,
    OptimizeDrive,
    AnalyzeDrive,
    CreateRestorePoint,
    ResetWindowsUpdate,
    ResetNetwork,
    UpdateGroupPolicy,
    GroupPolicyReport,
    BackupEventLogs,
    ClearEventLogs,
    ListProgramUpdates,
    UpdateAllPrograms,
}

/// <summary>A fully built command, ready to show to the user and run.</summary>
/// <param name="Title">Plain name ("Verificar arquivos do Windows").</param>
/// <param name="Explanation">What it does, in plain words.</param>
/// <param name="Duration">Expected duration, in plain words.</param>
/// <param name="Steps">Exact command lines, run in order in the same visible window.</param>
/// <param name="RequiresAdmin">True when Windows must ask for administrator rights (UAC).</param>
/// <param name="Program">Program that must exist for the command to work (availability check).</param>
/// <param name="Warnings">Extra things the user must know before confirming.</param>
public sealed record MaintenanceCommand(
    MaintenanceTool Tool,
    string Title,
    string Explanation,
    string Duration,
    IReadOnlyList<string> Steps,
    bool RequiresAdmin,
    string Program,
    IReadOnlyList<string> Warnings)
{
    /// <summary>The exact command line(s) shown to the user before running.</summary>
    public string DisplayCommand => string.Join(" & ", Steps);
}

/// <summary>Folders some commands write to. Built by the app; validated before use.</summary>
/// <param name="WindowsFolder">Windows folder (e.g. C:\Windows).</param>
/// <param name="OutputFolder">Folder for backups and reports (created by the command).</param>
/// <param name="Stamp">Date/time suffix for renamed folders, e.g. "20261116-1700".</param>
public sealed record MaintenancePaths(string WindowsFolder, string OutputFolder, string Stamp);

public static partial class MaintenanceCommands
{
    [GeneratedRegex("^[A-Za-z]$", RegexOptions.CultureInvariant)]
    private static partial Regex DriveLetter();

    [GeneratedRegex("^[0-9-]{1,20}$", RegexOptions.CultureInvariant)]
    private static partial Regex StampPattern();

    /// <summary>Characters that could change the meaning of a cmd.exe line, even inside quotes.</summary>
    private static readonly System.Buffers.SearchValues<char> UnsafePathChars = System.Buffers.SearchValues.Create("\"%!^&|<>\r\n");

    /// <summary>The event logs Clean Menu may clear. The Security log is never cleared.</summary>
    public static IReadOnlyList<string> ClearableEventLogs { get; } = ["Application", "System"];

    /// <summary>The event logs copied by the backup.</summary>
    public static IReadOnlyList<string> BackedUpEventLogs { get; } = ["Application", "System", "Security"];

    private static readonly string[] WindowsUpdateServices = ["wuauserv", "bits", "cryptsvc"];

    /// <summary>Builds the command. Drive tools need one letter; folder-based tools need <paramref name="paths"/>.</summary>
    public static MaintenanceCommand Build(MaintenanceTool tool, string? driveLetter = null, MaintenancePaths? paths = null)
    {
        string Drive()
        {
            if (driveLetter is null || !DriveLetter().IsMatch(driveLetter))
            {
                throw new ArgumentException("Letra de unidade inválida.", nameof(driveLetter));
            }

            return driveLetter.ToUpperInvariant() + ":";
        }

        MaintenancePaths Paths()
        {
            if (paths is null || !IsSafePath(paths.WindowsFolder) || !IsSafePath(paths.OutputFolder) || !StampPattern().IsMatch(paths.Stamp))
            {
                throw new ArgumentException("Pastas inválidas para este comando.", nameof(paths));
            }

            return paths;
        }

        MaintenanceCommand Admin(string title, string explanation, string duration, string program, string[] steps, params string[] warnings) =>
            new(tool, title, explanation, duration, steps, RequiresAdmin: true, program, warnings);

        MaintenanceCommand User(string title, string explanation, string duration, string program, string[] steps, params string[] warnings) =>
            new(tool, title, explanation, duration, steps, RequiresAdmin: false, program, warnings);

        return tool switch
        {
            MaintenanceTool.SystemFileCheck => Admin("Verificar arquivos do Windows (SFC)",
                "Procura arquivos do Windows danificados ou alterados e os corrige automaticamente.",
                "Leva de 10 a 30 minutos.", "sfc.exe", ["sfc.exe /scannow"]),

            MaintenanceTool.DismScanHealth => Admin("Verificar a imagem do Windows (DISM)",
                "Verifica se a cópia de reserva que o Windows usa para se consertar está boa. Só verifica, não altera nada.",
                "Leva de 5 a 20 minutos.", "dism.exe", ["dism.exe /Online /Cleanup-Image /ScanHealth"]),

            MaintenanceTool.DismRestoreHealth => Admin("Reparar a imagem do Windows (DISM)",
                "Conserta a cópia de reserva do Windows, baixando arquivos do Windows Update se for preciso. Use quando a verificação encontrar problemas.",
                "Leva de 15 a 60 minutos e usa a internet.", "dism.exe", ["dism.exe /Online /Cleanup-Image /RestoreHealth"]),

            MaintenanceTool.CheckDiskScan => Admin($"Verificar erros no disco {Drive()} (CHKDSK)",
                "Procura erros no sistema de arquivos sem precisar reiniciar o computador. Só relata; não altera nada.",
                "Leva de 1 a 10 minutos.", "chkdsk.exe", [$"chkdsk.exe {Drive()} /scan"]),

            MaintenanceTool.OptimizeDrive => Admin($"Otimizar o disco {Drive()}",
                "O Windows escolhe o certo para cada tipo: em SSD envia o comando TRIM (nunca desfragmenta); em HD desfragmenta.",
                "SSD: menos de 1 minuto. HD: pode levar mais de uma hora.", "defrag.exe", [$"defrag.exe {Drive()} /O /U /V"]),

            MaintenanceTool.AnalyzeDrive => Admin($"Analisar fragmentação do disco {Drive()}",
                "Mostra se o disco precisa de otimização. Só analisa; não altera nada.",
                "Leva de 1 a 5 minutos.", "defrag.exe", [$"defrag.exe {Drive()} /A /U /V"]),

            MaintenanceTool.CreateRestorePoint => Admin("Criar ponto de restauração",
                "Guarda o estado atual do Windows para você poder voltar a ele se algo der errado depois. Usa o PowerShell porque o Windows não tem outro comando para isso.",
                "Leva de 1 a 3 minutos.", "powershell.exe",
                ["powershell.exe -NoProfile -Command \"Checkpoint-Computer -Description 'Clean Menu' -RestorePointType MODIFY_SETTINGS\""]),

            MaintenanceTool.ResetWindowsUpdate => Admin("Consertar o Windows Update (reset)",
                "Use quando as atualizações do Windows travam ou dão erro. Para os serviços de atualização, renomeia as pastas de cache (nada é apagado) e liga os serviços de novo. O Windows recria as pastas sozinho.",
                "Leva de 1 a 3 minutos.", "net.exe", ResetWindowsUpdateSteps(Paths()),
                "O Windows Update NÃO é desativado: os serviços são parados e religados em seguida.",
                $"As pastas antigas ficam guardadas com o final \".bak-{paths!.Stamp}\". Depois que as atualizações voltarem a funcionar, você pode apagá-las.",
                "O histórico de atualizações mostrado nas Configurações pode ficar vazio. As atualizações instaladas continuam instaladas."),

            MaintenanceTool.ResetNetwork => Admin("Consertar a internet (reset de rede)",
                "Use quando a internet não funciona mas outros aparelhos na mesma rede funcionam. Limpa a memória de endereços (DNS) e volta as configurações de rede do Windows ao padrão.",
                "Leva menos de 1 minuto. Depois é preciso reiniciar o computador.", "netsh.exe",
                ["ipconfig /flushdns", "netsh winsock reset", "netsh int ip reset"],
                "Você precisa REINICIAR o computador depois para terminar.",
                "Se você configurou um IP fixo, um DNS próprio ou uma VPN manualmente, talvez precise configurar de novo.",
                "As senhas de Wi-Fi salvas NÃO são apagadas."),

            MaintenanceTool.UpdateGroupPolicy => Admin("Atualizar as políticas de grupo (GPO)",
                "Faz o Windows aplicar agora as regras definidas pela empresa ou pelo administrador, sem esperar. Útil em computadores de trabalho.",
                "Leva menos de 2 minutos.", "gpupdate.exe", ["gpupdate.exe /force"],
                "Em computadores de casa normalmente não há políticas e nada muda."),

            MaintenanceTool.GroupPolicyReport => Admin("Relatório de políticas de grupo (gpresult)",
                "Gera uma página com todas as regras (políticas) aplicadas a este computador e ao seu usuário. Útil para técnicos.",
                "Leva menos de 1 minuto.", "gpresult.exe",
                [MakeFolder(Paths().OutputFolder), $"gpresult.exe /h \"{Path.Combine(Paths().OutputFolder, "politicas.html")}\" /f",
                 $"echo Relatorio salvo em: \"{Path.Combine(Paths().OutputFolder, "politicas.html")}\""]),

            MaintenanceTool.BackupEventLogs => Admin("Fazer cópia dos registros de eventos",
                "Salva uma cópia dos registros de eventos do Windows (Aplicativo, Sistema e Segurança). Técnicos usam esses registros para descobrir a causa de erros e travamentos. Nada é apagado.",
                "Leva menos de 1 minuto.", "wevtutil.exe", BackupEventLogSteps(Paths().OutputFolder, BackedUpEventLogs, clear: false),
                $"A cópia será salva em: {paths!.OutputFolder}"),

            MaintenanceTool.ClearEventLogs => Admin("Limpar registros de eventos (Aplicativo e Sistema)",
                "Esvazia só os registros de Aplicativo e de Sistema. Antes de limpar cada um, uma cópia é salva; se a cópia falhar, aquele registro não é limpo. O registro de Segurança nunca é limpo.",
                "Leva menos de 1 minuto.", "wevtutil.exe", BackupEventLogSteps(Paths().OutputFolder, ClearableEventLogs, clear: true),
                $"A cópia será salva em: {paths!.OutputFolder}",
                "Limpar os registros NÃO deixa o computador mais rápido. Faça isso só se um técnico pedir ou para começar um diagnóstico do zero."),

            MaintenanceTool.ListProgramUpdates => User("Ver atualizações de programas (winget)",
                "Mostra a lista de programas instalados que têm versão mais nova disponível. Só mostra; não instala nada.",
                "Leva menos de 1 minuto e usa a internet.", "winget.exe",
                ["winget upgrade --accept-source-agreements"]),

            MaintenanceTool.UpdateAllPrograms => User("Atualizar todos os programas (winget)",
                "Baixa e instala a versão mais nova de todos os programas que o winget conhece. Usa as fontes oficiais de cada programa.",
                "Pode levar de 5 a 30 minutos e usa a internet.", "winget.exe",
                ["winget upgrade --all --accept-source-agreements --accept-package-agreements"],
                "Feche os programas que serão atualizados antes de começar.",
                "Alguns instaladores podem pedir permissão de administrador ou reiniciar o programa.",
                "Veja a lista antes com \"Ver atualizações de programas\"."),

            _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null),
        };
    }

    /// <summary>True for an absolute local path with no characters that cmd.exe could interpret.</summary>
    public static bool IsSafePath(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && Path.IsPathFullyQualified(path)
        && !path.StartsWith(@"\\", StringComparison.Ordinal)
        && path.AsSpan().IndexOfAny(UnsafePathChars) < 0;

    private static string MakeFolder(string folder) => $"if not exist \"{folder}\" mkdir \"{folder}\"";

    private static string[] ResetWindowsUpdateSteps(MaintenancePaths paths)
    {
        var steps = new List<string>();
        steps.AddRange(WindowsUpdateServices.Select(s => $"net stop {s}"));
        steps.Add($"ren \"{Path.Combine(paths.WindowsFolder, "SoftwareDistribution")}\" SoftwareDistribution.bak-{paths.Stamp}");
        steps.Add($"ren \"{Path.Combine(paths.WindowsFolder, "System32", "catroot2")}\" catroot2.bak-{paths.Stamp}");
        steps.AddRange(WindowsUpdateServices.Reverse().Select(s => $"net start {s}"));
        return [.. steps];
    }

    private static string[] BackupEventLogSteps(string folder, IReadOnlyList<string> logs, bool clear)
    {
        var steps = new List<string> { MakeFolder(folder) };
        foreach (var log in logs)
        {
            var export = $"wevtutil epl {log} \"{Path.Combine(folder, log + ".evtx")}\" /ow:true";
            steps.Add(clear ? $"{export} && wevtutil cl {log}" : export);
        }

        return [.. steps];
    }
}

/// <summary>Starts maintenance commands in a visible window.</summary>
public interface IMaintenanceRunner
{
    /// <summary>
    /// Opens a console window that shows each command and its output. When the command requires
    /// administrator rights, Windows asks first (UAC). Returns false when Windows could not start
    /// it, including when the user declined the administrator prompt.
    /// </summary>
    bool StartVisible(MaintenanceCommand command);

    /// <summary>True when the program the command needs exists on this computer.</summary>
    bool IsAvailable(MaintenanceCommand command);
}
