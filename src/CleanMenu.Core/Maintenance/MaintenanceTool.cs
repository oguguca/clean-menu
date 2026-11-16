using System.Text.RegularExpressions;

namespace CleanMenu.Core.Maintenance;

/// <summary>
/// Windows maintenance commands Clean Menu can start. This is a closed list: no free-form command
/// is ever executed. Each runs in its own visible window with administrator rights requested
/// through the normal Windows prompt (UAC) only for that command.
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
}

/// <summary>A fully built command, ready to show to the user and run.</summary>
/// <param name="Title">Plain name ("Verificar arquivos do Windows").</param>
/// <param name="Explanation">What it does, in plain words.</param>
/// <param name="Duration">Expected duration, in plain words.</param>
/// <param name="FileName">Program to run.</param>
/// <param name="Arguments">Exact arguments.</param>
public sealed record MaintenanceCommand(
    MaintenanceTool Tool,
    string Title,
    string Explanation,
    string Duration,
    string FileName,
    string Arguments)
{
    /// <summary>The exact command line shown to the user before running.</summary>
    public string DisplayCommand => $"{FileName} {Arguments}".Trim();
}

public static partial class MaintenanceCommands
{
    [GeneratedRegex("^[A-Za-z]$", RegexOptions.CultureInvariant)]
    private static partial Regex DriveLetter();

    /// <summary>Builds the command. Drive-based tools require a single drive letter (e.g. "C").</summary>
    public static MaintenanceCommand Build(MaintenanceTool tool, string? driveLetter = null)
    {
        string Drive()
        {
            if (driveLetter is null || !DriveLetter().IsMatch(driveLetter))
            {
                throw new ArgumentException("Letra de unidade inválida.", nameof(driveLetter));
            }

            return driveLetter.ToUpperInvariant() + ":";
        }

        return tool switch
        {
            MaintenanceTool.SystemFileCheck => new(tool, "Verificar arquivos do Windows (SFC)",
                "Procura arquivos do Windows danificados ou alterados e os corrige automaticamente.",
                "Leva de 10 a 30 minutos.", "sfc.exe", "/scannow"),

            MaintenanceTool.DismScanHealth => new(tool, "Verificar a imagem do Windows (DISM)",
                "Verifica se a cópia de reserva que o Windows usa para se consertar está boa. Só verifica, não altera nada.",
                "Leva de 5 a 20 minutos.", "dism.exe", "/Online /Cleanup-Image /ScanHealth"),

            MaintenanceTool.DismRestoreHealth => new(tool, "Reparar a imagem do Windows (DISM)",
                "Conserta a cópia de reserva do Windows, baixando arquivos do Windows Update se for preciso. Use quando a verificação encontrar problemas.",
                "Leva de 15 a 60 minutos e usa a internet.", "dism.exe", "/Online /Cleanup-Image /RestoreHealth"),

            MaintenanceTool.CheckDiskScan => new(tool, $"Verificar erros no disco {Drive()}",
                "Procura erros no sistema de arquivos sem precisar reiniciar o computador. Só relata; não altera nada.",
                "Leva de 1 a 10 minutos.", "chkdsk.exe", $"{Drive()} /scan"),

            MaintenanceTool.OptimizeDrive => new(tool, $"Otimizar o disco {Drive()}",
                "O Windows escolhe o certo para cada tipo: em SSD envia o comando TRIM (nunca desfragmenta); em HD desfragmenta.",
                "SSD: menos de 1 minuto. HD: pode levar mais de uma hora.", "defrag.exe", $"{Drive()} /O /U /V"),

            MaintenanceTool.AnalyzeDrive => new(tool, $"Analisar fragmentação do disco {Drive()}",
                "Mostra se o disco precisa de otimização. Só analisa; não altera nada.",
                "Leva de 1 a 5 minutos.", "defrag.exe", $"{Drive()} /A /U /V"),

            MaintenanceTool.CreateRestorePoint => new(tool, "Criar ponto de restauração",
                "Guarda o estado atual do Windows para você poder voltar a ele se algo der errado depois. Usa o PowerShell porque o Windows não tem outro comando para isso.",
                "Leva de 1 a 3 minutos.", "powershell.exe",
                "-NoProfile -Command \"Checkpoint-Computer -Description 'Clean Menu' -RestorePointType MODIFY_SETTINGS\""),

            _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null),
        };
    }
}

/// <summary>Starts maintenance commands in a visible, elevated window.</summary>
public interface IMaintenanceRunner
{
    /// <summary>
    /// Opens a console window that shows the command and its output. Returns false when Windows
    /// could not start it, including when the user declined the administrator prompt.
    /// </summary>
    bool StartVisibleElevated(MaintenanceCommand command);
}
