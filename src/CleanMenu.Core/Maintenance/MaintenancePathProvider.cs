using System.Globalization;

namespace CleanMenu.Core.Maintenance;

/// <summary>
/// Builds the folders used by commands that save files (event log copies, reports). Everything
/// goes under "Documentos\Clean Menu", in a new folder named after the date and time.
/// </summary>
public sealed class MaintenancePathProvider(string windowsFolder, string documentsFolder, TimeProvider clock)
{
    private readonly string _windowsFolder = windowsFolder ?? throw new ArgumentNullException(nameof(windowsFolder));
    private readonly TimeProvider _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <summary>Root folder shown to the user ("Abrir pasta").</summary>
    public string RootFolder { get; } = Path.Combine(documentsFolder ?? throw new ArgumentNullException(nameof(documentsFolder)), "Clean Menu");

    public MaintenancePaths For(MaintenanceTool tool)
    {
        var now = _clock.GetLocalNow();
        var stamp = now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        var folderStamp = now.ToString("yyyy-MM-dd_HH-mm", CultureInfo.InvariantCulture);
        var kind = tool switch
        {
            MaintenanceTool.GroupPolicyReport => "Relatorios",
            MaintenanceTool.BackupEventLogs or MaintenanceTool.ClearEventLogs => "Registros de eventos",
            _ => "Outros",
        };

        return new MaintenancePaths(_windowsFolder, Path.Combine(RootFolder, kind, folderStamp), stamp);
    }

    /// <summary>Builds a command, adding folders when the tool needs them.</summary>
    public MaintenanceCommand Build(MaintenanceTool tool, string? driveLetter = null) =>
        MaintenanceCommands.Build(tool, driveLetter, For(tool));
}
