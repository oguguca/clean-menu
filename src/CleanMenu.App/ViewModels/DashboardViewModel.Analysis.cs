using System.IO;
using System.Globalization;
using CleanMenu.App.Navigation;
using CleanMenu.App.Services;
using CleanMenu.Core.Cleanup;
using CleanMenu.Core.Formatting;
using CleanMenu.Core.History;
using CleanMenu.Core.Monitoring;
using CleanMenu.Core.Startup;
using CleanMenu.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CleanMenu.App.ViewModels;

/// <summary>Services the dashboard needs for "ANALISAR MEU COMPUTADOR".</summary>
public sealed record DashboardAnalysisServices(CleanupSession Session, INavigationService Navigation, IOperationHistory History, IStartupManager? Startup = null);

/// <summary>
/// The computer analysis on the home page. It only reads: it measures what can be cleaned and
/// judges the general state from real measurements (free disk space and memory).
/// </summary>
public sealed partial class DashboardViewModel : IDisposable
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private readonly DashboardAnalysisServices _analysis;
    private CancellationTokenSource? _analysisCancellation;

    public void Dispose() => _analysisCancellation?.Dispose();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand), nameof(CancelAnalysisCommand))]
    private bool _isAnalyzing;

    [ObservableProperty]
    private bool _hasAnalysis;

    [ObservableProperty]
    private string _analysisHeadline = "Seu computador ainda não foi analisado";

    [ObservableProperty]
    private string _analysisHint = "A análise só olha o computador. Nada é apagado ou alterado sem você confirmar.";

    [ObservableProperty]
    private double _analysisProgress = double.NaN;

    [ObservableProperty]
    private double _healthRingValue = double.NaN;

    [ObservableProperty]
    private string _healthLabel = MetricCardViewModel.Pending;

    [ObservableProperty]
    private StatusLevel _status = StatusLevel.Neutral;

    private bool CanAnalyze() => !IsAnalyzing;

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        IsAnalyzing = true;
        AnalysisHeadline = "Analisando seu computador…";
        AnalysisProgress = double.NaN;
        _analysisCancellation = new CancellationTokenSource();
        var progress = new Progress<OperationProgress>(p =>
        {
            AnalysisHint = p.Step;
            AnalysisProgress = p.Fraction is { } f ? f * 100 : double.NaN;
        });

        try
        {
            await _analysis.Session.AnalyzeAsync(progress, _analysisCancellation.Token).ConfigureAwait(true);
            await UpdateStartupCountAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            AnalysisHeadline = "Análise cancelada";
            AnalysisHint = "Nada foi alterado. Você pode analisar de novo quando quiser.";
        }
        catch (InvalidOperationException ex)
        {
            AnalysisHeadline = "Não foi possível analisar agora";
            AnalysisHint = ex.Message;
        }
        finally
        {
            _analysisCancellation.Dispose();
            _analysisCancellation = null;
            IsAnalyzing = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsAnalyzing))]
    private void CancelAnalysis() => _analysisCancellation?.Cancel();

    [RelayCommand]
    private void OpenCleanup() => _analysis.Navigation.NavigateTo(AppPage.SmartCleanup);

    private async Task UpdateStartupCountAsync()
    {
        if (_analysis.Startup is null)
        {
            return;
        }

        try
        {
            var entries = await _analysis.Startup.GetEntriesAsync().ConfigureAwait(true);
            var enabled = entries.Count(e => e.IsEnabled);
            StartupCard.With(enabled.ToString(PtBr), "abrem sozinhos ao ligar. Veja em Inicialização",
                enabled > 12 ? StatusLevel.Warning : StatusLevel.Good);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            StartupCard.With(MetricCardViewModel.Pending, "Não foi possível ler a inicialização");
        }
    }

    private void InitializeAnalysis()
    {
        _analysis.Session.AnalysisChanged += (_, _) => _dispatcher.Post(ApplyAnalysis);
        _analysis.History.Changed += (_, _) => _ = LoadLastCleanupAsync();
        StartupCard.With(MetricCardViewModel.Pending, AfterAnalysis);
        ApplyAnalysis();
        _ = LoadLastCleanupAsync();
    }

    internal void ApplyAnalysis()
    {
        var analysis = _analysis.Session.LastAnalysis;
        HasAnalysis = analysis is not null;
        if (analysis is null)
        {
            if (!IsAnalyzing && _analysis.Session.AnalyzedAt is null && HealthLabel != MetricCardViewModel.Pending)
            {
                // A cleanup ran after the last analysis: numbers are outdated.
                AnalysisHeadline = "Limpeza concluída";
                AnalysisHint = "Analise de novo para ver como o computador ficou.";
            }

            return;
        }

        var usable = analysis.Where(a => !a.Category.RequiresAdmin).ToList();
        var freeable = usable.Sum(a => a.EffectiveRecoverableBytes);
        var temp = usable.Where(a => a.Category.Group == CleanupGroups.Temp).ToList();

        FreeableCard.With(ByteSize.Format(freeable), "Veja os detalhes e limpe em Limpeza Inteligente",
            freeable >= 5L * 1024 * 1024 * 1024 ? StatusLevel.Warning : StatusLevel.Good);
        TempFilesCard.With(
            $"{temp.Sum(a => a.EffectiveRecoverableCount).ToString("N0", PtBr)}",
            $"arquivos ocupando {ByteSize.Format(temp.Sum(a => a.EffectiveRecoverableBytes))}");

        var health = EvaluateHealth();
        Status = HealthDisplay.ToStatus(health.Level);
        HealthLabel = health.Label;
        HealthRingValue = health.Level == HealthLevel.Unknown ? double.NaN : 100;
        HealthCard.With(health.Label, health.Reason, Status);

        AnalysisHeadline = freeable > 0
            ? $"Análise concluída: {ByteSize.Format(freeable)} podem ser liberados"
            : "Análise concluída: não há arquivos para limpar";
        AnalysisHint = health.Reason;
    }

    /// <summary>
    /// General state from real measurements only: the worst of system drive free space and memory.
    /// Shown as a word (Boa / Atenção / Crítica), never as an invented score.
    /// </summary>
    internal (HealthLevel Level, string Label, string Reason) EvaluateHealth()
    {
        var disk = Volumes.FirstOrDefault(v => v.Volume.IsSystem)?.Volume.SpaceHealth ?? HealthLevel.Unknown;
        var memory = _monitor.Latest?.Memory.Health ?? HealthLevel.Unknown;
        var level = (HealthLevel)Math.Max((int)disk, (int)memory);
        if (disk == HealthLevel.Unknown && memory == HealthLevel.Unknown)
        {
            level = HealthLevel.Unknown;
        }

        var reasons = new List<string>();
        if (disk is HealthLevel.Attention or HealthLevel.Critical)
        {
            reasons.Add("pouco espaço livre no disco do Windows");
        }

        if (memory is HealthLevel.Attention or HealthLevel.Critical)
        {
            reasons.Add("memória quase cheia");
        }

        var label = level switch
        {
            HealthLevel.Good => "Boa",
            HealthLevel.Attention => "Atenção",
            HealthLevel.Critical => "Crítica",
            _ => MetricCardViewModel.Pending,
        };

        var reason = reasons.Count > 0
            ? $"Motivo: {string.Join(" e ", reasons)}."
            : level == HealthLevel.Good ? "Espaço no disco e memória estão em bom nível." : "Sem dados suficientes para avaliar.";

        return (level, label, reason);
    }

    private async Task LoadLastCleanupAsync()
    {
        IReadOnlyList<OperationRecord> records;
        try
        {
            records = await _analysis.History.GetAllAsync().ConfigureAwait(false);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var last = records.FirstOrDefault(r => !r.DryRun && r.Type == "Limpeza");
        _dispatcher.Post(() =>
        {
            if (last is null)
            {
                LastCleanupCard.With("Nunca", "Nenhuma limpeza feita pelo Clean Menu");
            }
            else
            {
                LastCleanupCard.With(last.Time.ToLocalTime().ToString("dd/MM/yyyy", PtBr),
                    $"Liberou {ByteSize.Format(last.BytesRecovered)}", StatusLevel.Good);
            }
        });
    }
}
