using CleanMenu.Core.Cleanup;
using CleanMenu.Core.History;
using CleanMenu.Core.Platform;

namespace CleanMenu.App.Services;

/// <summary>
/// Shared state of the cleanup feature: the categories available on this Windows, the latest
/// analysis (so the dashboard and the cleanup page show the same numbers) and the cleaning run.
/// Every cleaning run is written to the operation history.
/// </summary>
public sealed class CleanupSession(ICleanupCatalog catalog, CleanupEngine engine, IOperationHistory history, OsProfile osProfile) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<CleanupCategory>? _categories;

    public event EventHandler? AnalysisChanged;

    public IReadOnlyList<CategoryAnalysis>? LastAnalysis { get; private set; }

    public DateTimeOffset? AnalyzedAt { get; private set; }

    public bool IsBusy => _gate.CurrentCount == 0;

    /// <summary>Category ids checked by default (from Settings); null = built-in defaults.</summary>
    public IReadOnlySet<string>? PreferredSelection { get; set; }

    /// <summary>Whether the cleanup page asks before deleting (Settings; on by default).</summary>
    public bool ConfirmBeforeCleaning { get; set; } = true;

    public bool IsSelectedByDefault(CleanupCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        if (category.RequiresAdmin)
        {
            return false;
        }

        return PreferredSelection?.Contains(category.Id) ?? category.SelectedByDefault;
    }

    public void Dispose() => _gate.Dispose();

    /// <summary>Categories that exist on the Windows version the app is adapted to.</summary>
    public IReadOnlyList<CleanupCategory> Categories =>
        _categories ??= catalog.GetCategories().Where(c => osProfile.Effective is null || osProfile.Shows(c.Os)).ToList();

    public async Task<IReadOnlyList<CategoryAnalysis>> AnalyzeAsync(IProgress<OperationProgress>? progress, CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Outra operação já está em andamento.");
        }

        try
        {
            var result = await engine.AnalyzeAsync(Categories, progress, cancellationToken).ConfigureAwait(false);
            LastAnalysis = result;
            AnalyzedAt = DateTimeOffset.Now;
        }
        finally
        {
            _gate.Release();
        }

        AnalysisChanged?.Invoke(this, EventArgs.Empty);
        return LastAnalysis!;
    }

    public async Task<CleanupReport> CleanAsync(
        IReadOnlyList<CategoryAnalysis> selected,
        bool dryRun,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selected);
        if (!await _gate.WaitAsync(0, CancellationToken.None).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Outra operação já está em andamento.");
        }

        CleanupReport report;
        try
        {
            report = await engine.CleanAsync(selected, dryRun, progress, cancellationToken).ConfigureAwait(false);
            await history.AddAsync(OperationRecords.FromCleanup(report), CancellationToken.None).ConfigureAwait(false);

            if (!dryRun)
            {
                // The numbers changed; analyze again before cleaning more.
                LastAnalysis = null;
                AnalyzedAt = null;
            }
        }
        finally
        {
            _gate.Release();
        }

        AnalysisChanged?.Invoke(this, EventArgs.Empty);
        return report;
    }
}
