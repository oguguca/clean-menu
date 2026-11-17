using System.Collections.ObjectModel;
using CleanMenu.App.Navigation;
using CleanMenu.App.Services;
using CleanMenu.Core.History;
using CleanMenu.Core.Maintenance;
using CleanMenu.Core.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CleanMenu.App.ViewModels;

/// <summary>A maintenance tool card.</summary>
public sealed record MaintenanceToolItem(MaintenanceCommand Command, bool IsAvailable = true)
{
    public string Title => Command.Title;

    public string Explanation => Command.Explanation;

    public string Duration => Command.Duration;

    public string DisplayCommand => Command.DisplayCommand;

    public bool NeedsAdmin => Command.RequiresAdmin;

    public string UnavailableText => $"Não disponível neste computador ({Command.Program} não encontrado).";
}

/// <summary>A titled group of maintenance tool cards.</summary>
public sealed record MaintenanceToolGroup(string Title, string Description, IReadOnlyList<MaintenanceToolItem> Items);

/// <summary>
/// Otimização page. Power plan (reversible), privacy/background settings (read here, changed in
/// Windows Settings) and Windows repair tools (run visibly, with administrator rights only for that
/// command). Windows Defender, Firewall and Windows Update are never touched.
/// </summary>
public sealed partial class OptimizationViewModel : PageViewModel
{
    private readonly IPowerPlanService _power;
    private readonly MaintenanceLauncher _launcher;
    private readonly MaintenancePathProvider _paths;
    private readonly ISystemActions _actions;
    private readonly IDialogService _dialogs;
    private readonly IOperationHistory _history;
    private readonly IUiDispatcher _dispatcher;

    [ObservableProperty]
    private string? _message;

    public OptimizationViewModel(
        IPowerPlanService power,
        IWindowsSettingsReader settings,
        IMaintenanceRunner runner,
        ISystemActions actions,
        IDialogService dialogs,
        IOperationHistory history,
        IUiDispatcher dispatcher,
        OsProfile osProfile,
        MaintenancePathProvider? paths = null)
        : base(PageCatalog.Get(AppPage.Optimization))
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(osProfile);
        _power = power ?? throw new ArgumentNullException(nameof(power));
        _launcher = new MaintenanceLauncher(runner, dialogs, history);
        _paths = paths ?? new MaintenancePathProvider(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            TimeProvider.System);
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        var version = osProfile.Effective ?? WindowsVersion.Windows11;
        foreach (var setting in settings.Read(version).Where(s => osProfile.Effective is null || osProfile.Shows(s.Os)))
        {
            Settings.Add(setting);
        }

        ToolGroups =
        [
            Group("Verificar e reparar o Windows",
                "Ferramentas do próprio Windows. Cada uma abre numa janela que mostra tudo o que está acontecendo. O Windows pede permissão de administrador antes.",
                MaintenanceTool.SystemFileCheck, MaintenanceTool.DismScanHealth, MaintenanceTool.DismRestoreHealth,
                MaintenanceTool.CheckDiskScan, MaintenanceTool.ResetWindowsUpdate, MaintenanceTool.CreateRestorePoint),
            Group("Atualizar programas instalados",
                "Usa o winget, o instalador oficial de programas do Windows, para ver e instalar versões novas.",
                MaintenanceTool.ListProgramUpdates, MaintenanceTool.UpdateAllPrograms),
            Group("Para técnicos",
                "Registros de eventos e políticas de grupo. As cópias e relatórios ficam na pasta Documentos, dentro de \"Clean Menu\".",
                MaintenanceTool.BackupEventLogs, MaintenanceTool.ClearEventLogs, MaintenanceTool.UpdateGroupPolicy, MaintenanceTool.GroupPolicyReport),
        ];

        _ = LoadPlansAsync();
    }

    public ObservableCollection<PowerPlan> PowerPlans { get; } = [];

    public ObservableCollection<WindowsSettingInfo> Settings { get; } = [];

    public IReadOnlyList<MaintenanceToolGroup> ToolGroups { get; }

    /// <summary>All tool cards, in display order.</summary>
    public IReadOnlyList<MaintenanceToolItem> Tools => [.. ToolGroups.SelectMany(g => g.Items)];

    public string OutputFolder => _paths.RootFolder;

    [RelayCommand]
    private async Task ActivatePlanAsync(PowerPlan? plan)
    {
        if (plan is null || plan.IsActive)
        {
            return;
        }

        if (!_dialogs.Confirm(new ConfirmationRequest(
                $"Usar o plano \"{plan.Name}\"?",
                plan.Explanation,
                ["Você pode voltar ao plano anterior a qualquer momento por aqui."],
                "Usar este plano", IsDestructive: false)))
        {
            return;
        }

        var ok = await _power.ActivateAsync(plan.Id).ConfigureAwait(true);
        Message = ok ? $"Plano de energia alterado para \"{plan.Name}\"." : "O Windows não permitiu trocar o plano de energia.";
        await _history.RecordActionAsync("Plano de energia", plan.Name, ok).ConfigureAwait(true);
        await LoadPlansAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void OpenSetting(WindowsSettingInfo? setting)
    {
        if (setting is not null && !_actions.OpenTool(setting.SettingsUri))
        {
            Message = "Não foi possível abrir as Configurações do Windows.";
        }
    }

    [RelayCommand]
    private void OpenVisualEffects()
    {
        if (!_actions.OpenTool("SystemPropertiesPerformance.exe"))
        {
            Message = "Não foi possível abrir as opções de desempenho do Windows.";
        }
    }

    [RelayCommand]
    private async Task RunToolAsync(MaintenanceToolItem? item)
    {
        if (item is null)
        {
            return;
        }

        // Rebuilt at click time so dated folders use the current date and time.
        var command = _paths.Build(item.Command.Tool, "C");
        var message = await _launcher.RunAsync(command, "Manutenção do Windows",
            destructive: command.Tool is MaintenanceTool.ClearEventLogs or MaintenanceTool.ResetWindowsUpdate).ConfigureAwait(true);
        if (message is not null)
        {
            Message = message;
        }
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        System.IO.Directory.CreateDirectory(_paths.RootFolder);
        if (!_actions.OpenTool(_paths.RootFolder))
        {
            Message = "Não foi possível abrir a pasta.";
        }
    }

    private MaintenanceToolGroup Group(string title, string description, params MaintenanceTool[] tools) =>
        new(title, description, tools.Select(t =>
        {
            var command = _paths.Build(t, "C");
            return new MaintenanceToolItem(command, _launcher.IsAvailable(command));
        }).ToList());

    private async Task LoadPlansAsync()
    {
        IReadOnlyList<PowerPlan> plans;
        try
        {
            plans = await _power.GetPlansAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            plans = [];
        }

        _dispatcher.Post(() =>
        {
            PowerPlans.Clear();
            foreach (var plan in plans)
            {
                PowerPlans.Add(plan);
            }
        });
    }
}
