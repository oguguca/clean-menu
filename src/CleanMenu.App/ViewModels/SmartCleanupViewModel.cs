using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CleanMenu.App.Navigation;
using CleanMenu.App.Services;
using CleanMenu.Core.Cleanup;
using CleanMenu.Core.Formatting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CleanMenu.App.ViewModels;

/// <summary>
/// Limpeza Inteligente: analyze, review what will be removed, confirm, clean (or simulate).
/// Nothing is deleted without an analysis, a selection and an explicit confirmation.
/// </summary>
public sealed partial class SmartCleanupViewModel : PageViewModel, IDisposable
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly CleanupSession _session;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _dispatcher;
    private CancellationTokenSource? _cancellation;

    public void Dispose() => _cancellation?.Dispose();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand), nameof(CleanCommand), nameof(CancelCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private double _progressPercent = double.NaN;

    [ObservableProperty]
    private bool _isDryRun;

    [ObservableProperty]
    private string? _resultMessage;

    [ObservableProperty]
    private bool _resultIsWarning;

    [ObservableProperty]
    private string _selectionSummary = "Clique em Analisar para ver quanto espaço pode ser liberado.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CleanCommand))]
    private bool _hasAnalysis;

    public SmartCleanupViewModel(CleanupSession session, IDialogService dialogs, IUiDispatcher dispatcher)
        : base(PageCatalog.Get(AppPage.SmartCleanup))
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        foreach (var category in _session.Categories)
        {
            var row = new CleanupCategoryRowViewModel(category) { IsSelected = _session.IsSelectedByDefault(category) };
            row.PropertyChanged += OnRowChanged;
            Rows.Add(row);
        }

        _session.AnalysisChanged += (_, _) => _dispatcher.Post(ApplySessionAnalysis);
        ApplySessionAnalysis();
    }

    public ObservableCollection<CleanupCategoryRowViewModel> Rows { get; } = [];

    public long SelectedBytes => Rows.Where(r => r.IsSelected && r.CanSelect).Sum(r => r.RecoverableBytes);

    public int SelectedCount => Rows.Where(r => r.IsSelected && r.CanSelect).Sum(r => r.RecoverableCount);

    private bool CanAnalyze() => !IsBusy;

    private bool CanClean() => !IsBusy && HasAnalysis && Rows.Any(r => r.IsSelected && r.CanSelect && r.RecoverableCount > 0);

    private bool CanCancel() => IsBusy;

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        ResultMessage = null;
        await RunBusyAsync(async (progress, token) =>
        {
            await _session.AnalyzeAsync(progress, token).ConfigureAwait(true);
        }, "Análise cancelada. Nada foi alterado.").ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanClean))]
    private async Task CleanAsync()
    {
        var selected = Rows.Where(r => r.IsSelected && r.CanSelect && r.Analysis is not null && r.RecoverableCount > 0).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var dryRun = IsDryRun;
        if (!dryRun && _session.ConfirmBeforeCleaning && !_dialogs.Confirm(BuildConfirmation(selected)))
        {
            return;
        }

        ResultMessage = null;
        CleanupReport? report = null;
        await RunBusyAsync(async (progress, token) =>
        {
            report = await _session.CleanAsync(selected.Select(r => r.Analysis!).ToList(), dryRun, progress, token).ConfigureAwait(true);
        }, "Limpeza cancelada.").ConfigureAwait(true);

        if (report is not null)
        {
            ShowReport(report);
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cancellation?.Cancel();

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var row in Rows.Where(r => r.CanSelect))
        {
            row.IsSelected = true;
        }
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var row in Rows)
        {
            row.IsSelected = false;
        }
    }

    [RelayCommand]
    private static void TogglePreview(CleanupCategoryRowViewModel? row)
    {
        if (row is not null)
        {
            row.IsPreviewOpen = !row.IsPreviewOpen;
        }
    }

    internal static ConfirmationRequest BuildConfirmation(IReadOnlyList<CleanupCategoryRowViewModel> selected)
    {
        var bytes = selected.Sum(r => r.RecoverableBytes);
        var count = selected.Sum(r => r.RecoverableCount);
        var details = selected.Select(r => $"{r.Title}: {ByteSize.Format(r.RecoverableBytes)}").ToList();
        details.Add("Seus documentos, fotos, senhas e cookies não são tocados.");
        details.Add("Arquivos que estiverem em uso serão mantidos.");
        if (selected.Any(r => r.Category.Kind == CleanupKind.RecycleBin))
        {
            details.Add("A Lixeira será esvaziada: esses arquivos não poderão ser recuperados.");
        }

        return new ConfirmationRequest(
            $"Liberar {ByteSize.Format(bytes)}?",
            $"Vamos apagar {count.ToString("N0", PtBr)} arquivos que não são necessários. Isso não pode ser desfeito.",
            details,
            "Limpar agora");
    }

    private async Task RunBusyAsync(Func<IProgress<OperationProgress>, CancellationToken, Task> action, string cancelledMessage)
    {
        IsBusy = true;
        ProgressText = "Preparando…";
        ProgressPercent = double.NaN;
        _cancellation = new CancellationTokenSource();
        var progress = new Progress<OperationProgress>(p =>
        {
            ProgressText = p.Total is { } total ? $"{p.Step} ({p.Processed.ToString("N0", PtBr)} de {total.ToString("N0", PtBr)})" : p.Step;
            ProgressPercent = p.Fraction is { } fraction ? fraction * 100 : double.NaN;
        });

        try
        {
            await action(progress, _cancellation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            ShowMessage(cancelledMessage, warning: false);
        }
        catch (InvalidOperationException ex)
        {
            ShowMessage(ex.Message, warning: true);
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            IsBusy = false;
            ProgressText = string.Empty;
        }
    }

    private void ShowReport(CleanupReport report)
    {
        var verb = report.DryRun ? "seriam liberados" : "liberados";
        var message = $"{ByteSize.Format(report.DeletedBytes)} {verb} · {report.DeletedCount.ToString("N0", PtBr)} arquivos" +
                      (report.DryRun ? " (simulação: nada foi apagado)" : " removidos");
        if (report.SkippedCount > 0)
        {
            message += $". {report.SkippedCount.ToString("N0", PtBr)} arquivos foram mantidos porque estavam em uso ou protegidos.";
        }

        if (report.Cancelled)
        {
            message += " A limpeza foi interrompida a pedido.";
        }

        var errors = report.Errors.ToList();
        if (errors.Count > 0)
        {
            message += " Avisos: " + string.Join("; ", errors);
        }

        ShowMessage(message, warning: errors.Count > 0);
    }

    private void ShowMessage(string message, bool warning)
    {
        ResultMessage = message;
        ResultIsWarning = warning;
    }

    private void ApplySessionAnalysis()
    {
        var analyses = _session.LastAnalysis?.ToDictionary(a => a.Category.Id);
        foreach (var row in Rows)
        {
            row.Analysis = analyses is not null && analyses.TryGetValue(row.Category.Id, out var analysis) ? analysis : null;
            row.IsPreviewOpen = false;
        }

        HasAnalysis = analyses is not null;
        UpdateSummary();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CleanupCategoryRowViewModel.IsSelected) or nameof(CleanupCategoryRowViewModel.Analysis))
        {
            UpdateSummary();
            CleanCommand.NotifyCanExecuteChanged();
        }
    }

    private void UpdateSummary()
    {
        OnPropertyChanged(nameof(SelectedBytes));
        OnPropertyChanged(nameof(SelectedCount));
        SelectionSummary = !HasAnalysis
            ? "Clique em Analisar para ver quanto espaço pode ser liberado. A análise não apaga nada."
            : $"Selecionado: {ByteSize.Format(SelectedBytes)} em {SelectedCount.ToString("N0", PtBr)} arquivos";
    }
}
