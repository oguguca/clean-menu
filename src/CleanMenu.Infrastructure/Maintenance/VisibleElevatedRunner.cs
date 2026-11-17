using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using CleanMenu.Core.Maintenance;

namespace CleanMenu.Infrastructure.Maintenance;

/// <summary>
/// Runs a maintenance command in a normal console window. Commands that need administrator
/// rights ask through the Windows UAC prompt, for this command only. The window shows each exact
/// command before running it, all of its output, and stays open at the end. Nothing runs hidden.
/// </summary>
public sealed class VisibleElevatedRunner : IMaintenanceRunner
{
    private const int UserCancelledUac = 1223;

    public bool StartVisible(MaintenanceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var info = new ProcessStartInfo("cmd.exe", BuildArguments(command))
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal,
        };
        if (command.RequiresAdmin)
        {
            info.Verb = "runas";
        }

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

    public bool IsAvailable(MaintenanceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var candidates = new[]
        {
            Path.Combine(Environment.SystemDirectory, command.Program),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", command.Program),
        };
        return candidates.Any(File.Exists);
    }

    /// <summary>
    /// "/k" keeps the window open so the user can read the result. Before each step the window
    /// prints the step itself (escaped so that printing never runs anything).
    /// </summary>
    public static string BuildArguments(MaintenanceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var builder = new StringBuilder("/k title Clean Menu");
        foreach (var step in command.Steps)
        {
            builder.Append(" & echo. & echo Clean Menu - executando: ").Append(EscapeForEcho(step)).Append(" & ").Append(step);
        }

        builder.Append(" & echo. & echo Clean Menu - terminado. Leia o resultado acima e depois feche esta janela.");
        return builder.ToString();
    }

    /// <summary>Escapes cmd.exe operators outside quotes so <c>echo</c> prints them instead of running them.</summary>
    public static string EscapeForEcho(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new StringBuilder(text.Length + 8);
        var inQuotes = false;
        foreach (var c in text)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes && c is '&' or '|' or '<' or '>' or '^' or '(' or ')')
            {
                builder.Append('^');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }
}
