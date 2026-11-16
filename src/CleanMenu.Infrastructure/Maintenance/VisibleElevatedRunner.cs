using System.ComponentModel;
using System.Diagnostics;
using CleanMenu.Core.Maintenance;

namespace CleanMenu.Infrastructure.Maintenance;

/// <summary>
/// Runs a maintenance command in a normal console window, asking for administrator rights through
/// the Windows UAC prompt for this command only. The window shows the exact command and all of
/// its output and stays open at the end. Nothing runs hidden.
/// </summary>
public sealed class VisibleElevatedRunner : IMaintenanceRunner
{
    private const int UserCancelledUac = 1223;

    public bool StartVisibleElevated(MaintenanceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var info = new ProcessStartInfo("cmd.exe", BuildArguments(command))
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Normal,
        };

        try
        {
            using var process = Process.Start(info);
            return process is not null;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == UserCancelledUac)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    /// <summary>"/k" keeps the window open so the user can read the result.</summary>
    public static string BuildArguments(MaintenanceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return $"/k echo Clean Menu - executando: {command.DisplayCommand} & echo. & {command.DisplayCommand}";
    }
}
