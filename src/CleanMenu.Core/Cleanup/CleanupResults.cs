namespace CleanMenu.Core.Cleanup;

/// <summary>What the analysis found for one category.</summary>
/// <param name="Category">The category.</param>
/// <param name="Files">Files that can be removed (passed every safety rule and the age rule).</param>
/// <param name="TotalFileCount">All files found, including recent ones that will be kept.</param>
/// <param name="TotalBytes">Space used by all files found.</param>
/// <param name="Note">Plain explanation when something could not be read.</param>
public sealed record CategoryAnalysis(
    CleanupCategory Category,
    IReadOnlyList<CleanupFile> Files,
    int TotalFileCount,
    long TotalBytes,
    string? Note = null)
{
    public int RecoverableCount => Files.Count;

    public long RecoverableBytes => Files.Sum(f => f.SizeBytes);

    public static CategoryAnalysis Special(CleanupCategory category, int count, long bytes, string? note = null) =>
        new(category, [], count, bytes, note) { SpecialCount = count, SpecialBytes = bytes };

    /// <summary>For non-file categories (Recycle Bin): what Windows reports.</summary>
    public int SpecialCount { get; init; }

    public long SpecialBytes { get; init; }

    public int EffectiveRecoverableCount => Category.Kind == CleanupKind.Files ? RecoverableCount : SpecialCount;

    public long EffectiveRecoverableBytes => Category.Kind == CleanupKind.Files ? RecoverableBytes : SpecialBytes;
}

/// <summary>A file that was kept, and why.</summary>
public sealed record SkippedFile(string Path, string Reason);

/// <summary>Outcome of cleaning one category.</summary>
public sealed record CategoryCleanupResult(
    string CategoryId,
    string CategoryTitle,
    int DeletedCount,
    long DeletedBytes,
    IReadOnlyList<SkippedFile> Skipped,
    string? Error = null);

/// <summary>Outcome of a whole cleanup run.</summary>
public sealed record CleanupReport(
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    bool DryRun,
    bool Cancelled,
    IReadOnlyList<CategoryCleanupResult> Categories)
{
    public int DeletedCount => Categories.Sum(c => c.DeletedCount);

    public long DeletedBytes => Categories.Sum(c => c.DeletedBytes);

    public int SkippedCount => Categories.Sum(c => c.Skipped.Count);

    public IEnumerable<string> Errors => Categories.Where(c => c.Error is not null).Select(c => $"{c.CategoryTitle}: {c.Error}");
}

/// <summary>Progress message for long operations.</summary>
/// <param name="Step">Plain description of what is happening ("Analisando: Cache do Chrome").</param>
/// <param name="Processed">Items processed so far.</param>
/// <param name="Total">Total items, when known.</param>
public sealed record OperationProgress(string Step, int Processed, int? Total = null)
{
    public double? Fraction => Total is > 0 ? Math.Clamp((double)Processed / Total.Value, 0, 1) : null;
}
