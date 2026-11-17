namespace CleanMenu.Core.Cleanup;

/// <summary>
/// Finds and removes unnecessary files. Safety rules, applied to every single file both when
/// analyzing and again right before deleting:
/// <list type="number">
/// <item>The path must pass <see cref="PathSafetyValidator"/> for the category root.</item>
/// <item>Symbolic links and junctions are never followed or deleted.</item>
/// <item>System-flagged files are kept.</item>
/// <item>Files newer than the category minimum age are kept (they may still be in use).</item>
/// <item>Files locked by a running program are skipped, never forced.</item>
/// </list>
/// In dry-run mode every check runs but nothing is deleted.
/// </summary>
public sealed class CleanupEngine(
    ICleanupFileSystem fileSystem,
    PathSafetyValidator validator,
    IRecycleBin recycleBin,
    TimeProvider time)
{
    private const int ProgressEvery = 200;

    public Task<IReadOnlyList<CategoryAnalysis>> AnalyzeAsync(
        IReadOnlyList<CleanupCategory> categories,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(categories);
        return Task.Run<IReadOnlyList<CategoryAnalysis>>(() =>
        {
            var results = new List<CategoryAnalysis>(categories.Count);
            for (var i = 0; i < categories.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var category = categories[i];
                progress?.Report(new OperationProgress($"Analisando: {category.Title}", i, categories.Count));
                results.Add(AnalyzeCategory(category, cancellationToken));
            }

            progress?.Report(new OperationProgress("Análise concluída", categories.Count, categories.Count));
            return results;
        }, cancellationToken);
    }

    public CategoryAnalysis AnalyzeCategory(CleanupCategory category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        if (category.Kind == CleanupKind.RecycleBin)
        {
            var info = recycleBin.Query();
            return info is null
                ? CategoryAnalysis.Special(category, 0, 0, "O Windows não informou o conteúdo da Lixeira.")
                : CategoryAnalysis.Special(category, (int)Math.Min(int.MaxValue, info.ItemCount), info.SizeBytes);
        }

        if (category.RequiresAdmin)
        {
            return new CategoryAnalysis(category, [], 0, 0, "Precisa de permissão de administrador.");
        }

        var now = time.GetUtcNow();
        var eligible = new List<CleanupFile>();
        var totalCount = 0;
        long totalBytes = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in category.Targets)
        {
            if (validator.ValidateRoot(target.Root) != PathRejection.None || !fileSystem.DirectoryExists(target.Root))
            {
                continue;
            }

            foreach (var file in fileSystem.EnumerateFiles(target.Root, target.SearchPattern, target.Recursive))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!seen.Add(file.Path))
                {
                    continue;
                }

                totalCount++;
                totalBytes += file.SizeBytes;

                if (IsEligible(file, target.Root, category.MinimumAge, now))
                {
                    eligible.Add(file);
                }
            }
        }

        return new CategoryAnalysis(category, eligible, totalCount, totalBytes, category.Hint);
    }

    public Task<CleanupReport> CleanAsync(
        IReadOnlyList<CategoryAnalysis> selected,
        bool dryRun,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selected);
        return Task.Run(() => Clean(selected, dryRun, progress, cancellationToken), CancellationToken.None);
    }

    private CleanupReport Clean(
        IReadOnlyList<CategoryAnalysis> selected,
        bool dryRun,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var started = time.GetUtcNow();
        var total = selected.Sum(s => s.Category.Kind == CleanupKind.Files ? s.Files.Count : 1);
        var processed = 0;
        var results = new List<CategoryCleanupResult>(selected.Count);
        var cancelled = false;

        foreach (var analysis in selected)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            var category = analysis.Category;
            progress?.Report(new OperationProgress($"{(dryRun ? "Simulando" : "Limpando")}: {category.Title}", processed, total));

            if (category.RequiresAdmin)
            {
                results.Add(new CategoryCleanupResult(category.Id, category.Title, 0, 0, [], "Precisa de permissão de administrador."));
                continue;
            }

            if (category.Kind == CleanupKind.RecycleBin)
            {
                processed++;
                if (dryRun)
                {
                    results.Add(new CategoryCleanupResult(category.Id, category.Title, analysis.SpecialCount, analysis.SpecialBytes, []));
                }
                else
                {
                    var ok = recycleBin.Empty();
                    results.Add(ok
                        ? new CategoryCleanupResult(category.Id, category.Title, analysis.SpecialCount, analysis.SpecialBytes, [])
                        : new CategoryCleanupResult(category.Id, category.Title, 0, 0, [], "O Windows não conseguiu esvaziar a Lixeira."));
                }

                continue;
            }

            var now = time.GetUtcNow();
            var deleted = 0;
            long deletedBytes = 0;
            var skipped = new List<SkippedFile>();
            var roots = category.Targets.Select(t => t.Root).ToList();

            foreach (var file in analysis.Files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }

                processed++;
                if (processed % ProgressEvery == 0)
                {
                    progress?.Report(new OperationProgress($"{(dryRun ? "Simulando" : "Limpando")}: {category.Title}", processed, total));
                }

                // Re-check everything right before deleting: the disk may have changed since the analysis.
                var root = roots.FirstOrDefault(r => validator.Validate(file.Path, r) == PathRejection.None);
                if (root is null)
                {
                    skipped.Add(new SkippedFile(file.Path, "Recusado pela verificação de segurança"));
                    continue;
                }

                var current = fileSystem.GetFile(file.Path);
                if (current is null)
                {
                    continue; // Already gone (e.g. the program removed it itself).
                }

                if (!IsEligible(current, root, category.MinimumAge, now) || current.LastWriteUtc > file.LastWriteUtc)
                {
                    skipped.Add(new SkippedFile(file.Path, "Mudou depois da análise"));
                    continue;
                }

                if (dryRun)
                {
                    deleted++;
                    deletedBytes += file.SizeBytes;
                    continue;
                }

                switch (fileSystem.DeleteFile(file.Path))
                {
                    case DeleteOutcome.Deleted:
                        deleted++;
                        deletedBytes += file.SizeBytes;
                        break;
                    case DeleteOutcome.InUse:
                        skipped.Add(new SkippedFile(file.Path, "Em uso por um programa aberto"));
                        break;
                    case DeleteOutcome.AccessDenied:
                        skipped.Add(new SkippedFile(file.Path, "Sem permissão"));
                        break;
                    case DeleteOutcome.Refused:
                        skipped.Add(new SkippedFile(file.Path, "Recusado pela verificação de segurança"));
                        break;
                    case DeleteOutcome.NotFound:
                        break;
                }
            }

            if (!dryRun)
            {
                foreach (var target in category.Targets.Where(t => t.Recursive && validator.ValidateRoot(t.Root) == PathRejection.None))
                {
                    fileSystem.DeleteEmptyDirectories(target.Root);
                }
            }

            results.Add(new CategoryCleanupResult(category.Id, category.Title, deleted, deletedBytes, skipped));
            if (cancelled)
            {
                break;
            }
        }

        progress?.Report(new OperationProgress(cancelled ? "Cancelado" : "Concluído", processed, total));
        return new CleanupReport(started, time.GetUtcNow(), dryRun, cancelled, results);
    }

    private bool IsEligible(CleanupFile file, string root, TimeSpan minimumAge, DateTimeOffset now) =>
        !file.IsReparsePoint
        && !file.Attributes.HasFlag(FileAttributes.System)
        // A temporary file a program opened recently is still in use by it, even if old.
        && now - file.LastUsedUtc >= minimumAge
        && validator.Validate(file.Path, root) == PathRejection.None;
}
