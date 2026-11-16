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
public sealed record MaintenanceToolItem(MaintenanceCommand Command)
{
    public string Title => Command.Title;

    public string Explanation => Command.Explanation;

    public string Duration => Command.Duration;

    public string DisplayCommand => Command.DisplayCommand;
}

/// <summary>
/// Otimização page. Power plan (reversible), privacy/background settings (read here, changed in
/// Windows Settings) and Windows repair tools (run visibly, with administrator rights only for that
/// command). Windows Defender, Firewall and Windows Update are never touched.
/// </summary>
public sealed partial class OptimizationViewModel : PageViewModel
{
    private readonly IPowerPlanService _power;
    private readonly IMaintenanceRunner _runner;
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
        OsProfile osProfile)
        : base(PageCatalog.Get(AppPage.Optimization))
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(osProfile);
        _power = power ?? throw new ArgumentNullException(nameof(power));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        var version = osProfile.Effective ?? WindowsVersion.Windows11;
        foreach (var setting in settings.Read(version).Where(s => osProfile.Effective is null || osProfile.Shows(s.Os)))
        {
            Settings.Add(setting);
        }

        foreach (var tool in new[] { MaintenanceTool.SystemFileCheck, MaintenanceTool.DismScanHealth, MaintenanceTool.DismRestoreHealth, MaintenanceTool.CreateRestorePoint })
        {
            Tools.Add(new MaintenanceToolItem(MaintenanceCommands.Build(tool)));
        }

        _ = LoadPlansAsync();
    }

    public ObservableCollection<PowerPlan> PowerPlans { get; } = [];

    public ObservableCollection<WindowsSettingInfo> Settings { get; } = [];

    public ObservableCollection<MaintenanceToolItem> Tools { get; } = [];

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

        if (!_dialogs.Confirm(new ConfirmationRequest(
                $"{item.Title}?",
                item.Explanation,
                [
                    $"Comando que será executado: {item.DisplayCommand}",
                    item.Duration,
                    "O Windows vai pedir permissão de administrador só para este comando.",
                    "Uma janela vai abrir mostrando o andamento. Não feche até terminar.",
                ],
                "Executar", IsDestructive: false)))
        {
            return;
        }

        var started = _runner.StartVisibleElevated(item.Command);
        Message = started
            ? $"\"{item.Title}\" foi iniciado numa janela própria. O resultado aparece lá."
            : "O comando não foi iniciado (a permissão de administrador foi negada ou o Windows recusou).";
        await _history.RecordActionAsync("Manutenção do Windows", item.Title, started, started ? null : "Não iniciado").ConfigureAwait(true);
    }

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
