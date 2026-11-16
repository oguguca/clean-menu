using System.IO;
using System.Collections.ObjectModel;
using System.Globalization;
using CleanMenu.App.Navigation;
using CleanMenu.App.Services;
using CleanMenu.Core.Formatting;
using CleanMenu.Core.History;
using CleanMenu.UI;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CleanMenu.App.ViewModels;

/// <summary>One history entry as shown to the user.</summary>
public sealed class HistoryRowViewModel(OperationRecord record)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public OperationRecord Record { get; } = record ?? throw new ArgumentNullException(nameof(record));

    public string Type => Record.Type;

    public string When => Record.Time.ToLocalTime().ToString("dd/MM/yyyy 'às' HH:mm", PtBr);

    public string Details => Record.BytesRecovered > 0 || Record.FilesRemoved > 0
        ? $"{ByteSize.Format(Record.BytesRecovered)} · {Record.FilesRemoved.ToString("N0", PtBr)} arquivos{(Record.DryRun ? " (simulação, nada apagado)" : string.Empty)}"
        : string.Join(", ", Record.Categories);

    public string Categories => Record.BytesRecovered > 0 || Record.FilesRemoved > 0 ? string.Join(", ", Record.Categories) : string.Empty;

    public string Result => Record.FilesSkipped > 0
        ? $"{Record.Result} · {Record.FilesSkipped.ToString("N0", PtBr)} arquivos mantidos"
        : Record.Result;

    public string? Errors => Record.Errors.Count > 0 ? string.Join(" · ", Record.Errors) : null;

    public StatusLevel Status => Record.Result switch
    {
        "Concluída" => StatusLevel.Good,
        "Falhou" => StatusLevel.Critical,
        "Cancelada" => StatusLevel.Neutral,
        _ => StatusLevel.Warning,
    };
}

/// <summary>Histórico: every operation Clean Menu performed, newest first.</summary>
public sealed partial class HistoryViewModel : PageViewModel
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private readonly IOperationHistory _history;
    private readonly IUiDispatcher _dispatcher;

    [ObservableProperty]
    private string _summary = "Carregando…";

    [ObservableProperty]
    private bool _isEmpty;

    public HistoryViewModel(IOperationHistory history, IUiDispatcher dispatcher)
        : base(PageCatalog.Get(AppPage.History))
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _history.Changed += (_, _) => _ = LoadAsync();
        _ = LoadAsync();
    }

    public ObservableCollection<HistoryRowViewModel> Rows { get; } = [];

    internal async Task LoadAsync()
    {
        IReadOnlyList<OperationRecord> records;
        try
        {
            records = await _history.GetAllAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            records = [];
        }

        _dispatcher.Post(() =>
        {
            Rows.Clear();
            foreach (var record in records)
            {
                Rows.Add(new HistoryRowViewModel(record));
            }

            var cleanups = records.Where(r => !r.DryRun && r.Type == "Limpeza").ToList();
            IsEmpty = records.Count == 0;
            Summary = IsEmpty
                ? "Nenhuma operação ainda. Tudo o que o Clean Menu fizer vai aparecer aqui."
                : $"Total liberado pelo Clean Menu: {ByteSize.Format(cleanups.Sum(r => r.BytesRecovered))} em {cleanups.Count.ToString(PtBr)} limpeza(s) · {records.Count.ToString(PtBr)} operações registradas";
        });
    }
}
