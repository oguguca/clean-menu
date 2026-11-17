using CleanMenu.Core.History;
using CleanMenu.Core.Maintenance;

namespace CleanMenu.App.Services;

/// <summary>
/// Shared flow for every maintenance command: show the exact command and its warnings, ask for
/// confirmation, start it in a visible window and record the result in the history.
/// </summary>
public sealed class MaintenanceLauncher(IMaintenanceRunner runner, IDialogService dialogs, IOperationHistory history)
{
    private readonly IMaintenanceRunner _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    private readonly IDialogService _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    private readonly IOperationHistory _history = history ?? throw new ArgumentNullException(nameof(history));

    public bool IsAvailable(MaintenanceCommand command) => _runner.IsAvailable(command);

    /// <summary>Returns the message to show, or null when the user cancelled.</summary>
    public async Task<string?> RunAsync(MaintenanceCommand command, string historyType, string? extra = null, bool destructive = false)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_runner.IsAvailable(command))
        {
            return $"\"{command.Title}\" não está disponível neste computador ({command.Program} não foi encontrado).";
        }

        var details = new List<string>();
        if (extra is not null)
        {
            details.Add(extra);
        }

        details.AddRange(command.Warnings);
        if (command.Steps.Count == 1)
        {
            details.Add($"Comando que será executado: {command.DisplayCommand}");
        }
        else
        {
            details.Add("Comandos que serão executados, nesta ordem:");
            details.AddRange(command.Steps.Select((step, i) => $"{i + 1}. {step}"));
        }

        details.Add(command.Duration);
        details.Add(command.RequiresAdmin
            ? "O Windows vai pedir permissão de administrador só para este comando."
            : "Não precisa de permissão de administrador.");
        details.Add("Uma janela vai abrir mostrando o andamento. Não feche até terminar.");

        if (!_dialogs.Confirm(new ConfirmationRequest($"{command.Title}?", command.Explanation, details, "Executar", destructive)))
        {
            return null;
        }

        var started = _runner.StartVisible(command);
        await _history.RecordActionAsync(historyType, command.Title, started, started ? null : "Não iniciado").ConfigureAwait(true);
        return started
            ? $"\"{command.Title}\" foi iniciado numa janela própria. O resultado aparece lá."
            : command.RequiresAdmin
                ? "O comando não foi iniciado (a permissão de administrador foi negada ou o Windows recusou)."
                : "O Windows não conseguiu iniciar o comando.";
    }
}
