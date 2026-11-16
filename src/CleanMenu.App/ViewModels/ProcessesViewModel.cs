using System.Collections.ObjectModel;
using CleanMenu.App.Navigation;
using CleanMenu.App.Services;
using CleanMenu.Core.History;
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
/// Ending a process asks for confirmation and is refused for protected Windows processes.
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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EndSelectedCommand), nameof(OpenLocationCommand), nameof(ShowPropertiesCommand))]
    [NotifyPropertyChangedFor(nameof(SelectionHint))]
    private ProcessRowViewModel? _selectedRow;

    [ObservableProperty]
    private string? _message;

    private readonly IProcessActions _actions;
    private readonly IDialogService _dialogs;
    private readonly IOperationHistory _history;

    public ProcessesViewModel(ProcessMonitor processes, IUiDispatcher dispatcher, IProcessActions actions, IDialogService dialogs, IOperationHistory history)
        : base(PageCatalog.Get(AppPage.Processes))
    {
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _processes.Updated += (_, _) => _dispatcher.Post(Rebuild);
        Rebuild();
    }

    public string SelectionHint => SelectedRow is null
        ? "Clique num item da lista para ver as ações."
        : SelectedRow.IsProtected
            ? $"{SelectedRow.Name}: faz parte do Windows e não pode ser fechado pelo Clean Menu."
            : $"{SelectedRow.Name}: {SelectedRow.Advice}";

    private bool CanEnd() => SelectedRow is { IsProtected: false };

    private bool HasSelection() => SelectedRow is not null;

    [RelayCommand(CanExecute = nameof(CanEnd))]
    private async Task EndSelectedAsync()
    {
        if (SelectedRow is not { } row || row.IsProtected)
        {
            return;
        }

        if (!_dialogs.Confirm(new ConfirmationRequest(
                $"Fechar \"{row.Name}\"?",
                "O programa será fechado imediatamente. Trabalhos não salvos nele serão perdidos.",
                ["Se for um programa que você está usando, prefira fechá-lo pela própria janela.", $"Processo: {row.Name} (PID {row.Pid})"],
                "Fechar programa")))
        {
            return;
        }

        var result = await Task.Run(() => _actions.EndProcess(row.Pid, row.Name)).ConfigureAwait(true);
        Message = ProcessEndText.Describe(result);
        await _history.RecordActionAsync("Processo finalizado", $"{row.Name} (PID {row.Pid})", result == ProcessEndResult.Ended,
            result == ProcessEndResult.Ended ? null : Message).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenLocation()
    {
        var path = SelectedRow is { } row ? _actions.GetExecutablePath(row.Pid) : null;
        Message = path is not null && _actions.OpenFileLocation(path) ? null : "O Windows não informou onde fica este programa (é comum em processos do sistema).";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ShowProperties()
    {
        var path = SelectedRow is { } row ? _actions.GetExecutablePath(row.Pid) : null;
        Message = path is not null && _actions.ShowFileProperties(path) ? null : "O Windows não informou onde fica este programa (é comum em processos do sistema).";
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
