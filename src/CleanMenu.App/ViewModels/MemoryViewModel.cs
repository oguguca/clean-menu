using System.Collections.ObjectModel;
using CleanMenu.App.Navigation;
using CleanMenu.App.Services;
using CleanMenu.Core.Formatting;
using CleanMenu.Core.Monitoring;
using CleanMenu.Core.Processes;
using CleanMenu.Core.SystemInfo;
using CleanMenu.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CleanMenu.App.ViewModels;

/// <summary>
/// Memória RAM page: real usage, history, the programs using the most memory and evidence-based
/// warnings about memory that keeps growing. It never "frees" memory artificially.
/// </summary>
public sealed partial class MemoryViewModel : PageViewModel
{
    public const int TopCount = 10;

    private readonly ILiveMonitor _monitor;
    private readonly ProcessMonitor _processes;
    private readonly ISystemActions _actions;
    private readonly IUiDispatcher _dispatcher;
    private readonly Dictionary<string, ProgramRowViewModel> _rows = new(StringComparer.OrdinalIgnoreCase);
    private long? _installedBytes;

    [ObservableProperty]
    private double _usedPercent = double.NaN;

    [ObservableProperty]
    private string _usedPercentText = "—";

    [ObservableProperty]
    private string _totalText = "—";

    [ObservableProperty]
    private string _usedText = "—";

    [ObservableProperty]
    private string _availableText = "—";

    [ObservableProperty]
    private string _reservedText = "Não identificado";

    [ObservableProperty]
    private string _statusText = "Medindo…";

    [ObservableProperty]
    private StatusLevel _status = StatusLevel.Neutral;

    [ObservableProperty]
    private IReadOnlyList<double>? _history;

    [ObservableProperty]
    private string? _actionMessage;

    public MemoryViewModel(
        ILiveMonitor monitor,
        ProcessMonitor processes,
        ISystemInfoProvider systemInfo,
        ISystemActions actions,
        IUiDispatcher dispatcher)
        : base(PageCatalog.Get(AppPage.Memory))
    {
        ArgumentNullException.ThrowIfNull(systemInfo);
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        _monitor.SampleTaken += (_, sample) => _dispatcher.Post(() => Apply(sample));
        _processes.Updated += (_, _) => _dispatcher.Post(ApplyProcesses);

        if (_monitor.Latest is { } latest)
        {
            Apply(latest);
        }

        ApplyProcesses();
        _ = LoadInstalledAsync(systemInfo);
    }

    public ObservableCollection<ProgramRowViewModel> TopProcesses { get; } = [];

    public ObservableCollection<string> GrowthWarnings { get; } = [];

    public string NoFreeButtonExplanation { get; } =
        "O Windows já usa a memória livre como cache para abrir programas mais rápido e devolve esse espaço " +
        "na hora em que um programa precisa. Programas que \"esvaziam\" a memória à força só deixam o computador " +
        "mais lento logo depois. O jeito certo de liberar memória é fechar os programas que você não está usando.";

    [RelayCommand]
    private void OpenTaskManager() =>
        ActionMessage = _actions.OpenTool("taskmgr.exe") ? null : "Não foi possível abrir o Gerenciador de Tarefas.";

    [RelayCommand]
    private async Task RefreshProcessesAsync() => await Task.Run(_processes.Refresh).ConfigureAwait(false);

    internal void Apply(LiveSample sample)
    {
        var memory = sample.Memory;
        UsedPercent = memory.UsedPercent;
        UsedPercentText = HealthDisplay.Percent(memory.UsedPercent);
        TotalText = ByteSize.Format(memory.TotalBytes);
        UsedText = ByteSize.Format(memory.UsedBytes);
        AvailableText = ByteSize.Format(memory.AvailableBytes);
        Status = HealthDisplay.ToStatus(memory.Health);
        StatusText = memory.Health switch
        {
            HealthLevel.Good => "Tudo certo. Há memória livre suficiente.",
            HealthLevel.Attention => "A memória está ficando cheia. Feche programas que não estiver usando.",
            HealthLevel.Critical => "Memória quase cheia. O computador pode ficar lento. Feche alguns programas.",
            _ => "Medindo…",
        };
        History = _monitor.MemoryHistory;
        UpdateReserved(memory);
    }

    internal void ApplyProcesses()
    {
        var top = ProgramUsage.Group(_processes.Latest).Take(TopCount).ToList();

        var rows = new List<ProgramRowViewModel>(top.Count);
        foreach (var usage in top)
        {
            if (_rows.TryGetValue(usage.Name, out var row))
            {
                row.Update(usage);
            }
            else
            {
                row = new ProgramRowViewModel(usage);
                _rows[usage.Name] = row;
            }

            rows.Add(row);
        }

        foreach (var name in _rows.Keys.Except(top.Select(t => t.Name), StringComparer.OrdinalIgnoreCase).ToList())
        {
            _rows.Remove(name);
        }

        CollectionSync.Apply(TopProcesses, rows);

        GrowthWarnings.Clear();
        foreach (var suspect in _processes.GrowthSuspects)
        {
            GrowthWarnings.Add(
                $"{suspect.Name}: a memória só aumentou nos últimos {HealthDisplay.Duration(suspect.ObservedFor)} " +
                $"(de {ByteSize.Format(suspect.StartBytes)} para {ByteSize.Format(suspect.CurrentBytes)}). " +
                "Pode ser um vazamento de memória. Fechar e abrir o programa de novo costuma resolver.");
        }
    }

    private void UpdateReserved(MemoryStatus memory)
    {
        if (memory.ReservedBytes(_installedBytes) is { } reserved && reserved > 0)
        {
            ReservedText = $"{ByteSize.Format(reserved)} (usada pelo hardware, como o vídeo integrado)";
        }
    }

    private async Task LoadInstalledAsync(ISystemInfoProvider systemInfo)
    {
        try
        {
            var info = await systemInfo.GetAsync().ConfigureAwait(false);
            _dispatcher.Post(() =>
            {
                _installedBytes = info.Memory.InstalledBytes;
                if (_monitor.Latest is { } latest)
                {
                    UpdateReserved(latest.Memory);
                }
            });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Reserved memory stays "Não identificado".
        }
    }
}
