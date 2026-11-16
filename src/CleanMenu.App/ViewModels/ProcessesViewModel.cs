using System.Collections.ObjectModel;
using CleanMenu.App.Navigation;
using CleanMenu.App.Services;
using CleanMenu.Core.Processes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CleanMenu.App.ViewModels;

public enum ProcessSortMode
{
    Memory,
    Cpu,
    Name,
}

/// <summary>
/// Processos page: every running process with CPU and memory, sortable and searchable.
/// Read-only in this version (ending processes arrives with the protection rules in Etapa 5).
/// </summary>
public sealed partial class ProcessesViewModel : PageViewModel
{
    private readonly ProcessMonitor _processes;
    private readonly IUiDispatcher _dispatcher;
    private readonly Dictionary<int, ProcessRowViewModel> _rows = [];

    [ObservableProperty]
    private ProcessSortMode _sortMode = ProcessSortMode.Memory;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _summary = "Carregando…";

    [ObservableProperty]
    private bool _isRefreshing;

    public ProcessesViewModel(ProcessMonitor processes, IUiDispatcher dispatcher)
        : base(PageCatalog.Get(AppPage.Processes))
    {
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _processes.Updated += (_, _) => _dispatcher.Post(Rebuild);
        Rebuild();
    }

    public ObservableCollection<ProcessRowViewModel> Rows { get; } = [];

    partial void OnSortModeChanged(ProcessSortMode value) => Rebuild();

    partial void OnFilterTextChanged(string value) => Rebuild();

    [RelayCommand]
    private void SortBy(ProcessSortMode mode) => SortMode = mode;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            await Task.Run(_processes.Refresh).ConfigureAwait(true);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    internal void Rebuild()
    {
        var latest = _processes.Latest.Where(p => p.Pid != 0).ToList();
        foreach (var pid in _rows.Keys.Except(latest.Select(p => p.Pid)).ToList())
        {
            _rows.Remove(pid);
        }

        foreach (var usage in latest)
        {
            if (_rows.TryGetValue(usage.Pid, out var row) && row.Name == usage.Name)
            {
                row.Update(usage);
            }
            else
            {
                _rows[usage.Pid] = new ProcessRowViewModel(usage);
            }
        }

        var filter = FilterText.Trim();
        IEnumerable<ProcessRowViewModel> query = _rows.Values;
        if (filter.Length > 0)
        {
            query = query.Where(r => r.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = (SortMode switch
        {
            ProcessSortMode.Cpu => query.OrderByDescending(r => double.IsNaN(r.CpuPercent) ? -1 : r.CpuPercent).ThenByDescending(r => r.MemoryBytes),
            ProcessSortMode.Name => query.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(r => r.Pid),
            _ => query.OrderByDescending(r => r.MemoryBytes),
        }).ToList();

        CollectionSync.Apply(Rows, ordered);
        Summary = latest.Count == 0
            ? "Carregando…"
            : $"{latest.Count} processos em execução · {latest.Count(p => p.Kind == ProcessKind.UserApp)} programas abertos por você";
    }
}
