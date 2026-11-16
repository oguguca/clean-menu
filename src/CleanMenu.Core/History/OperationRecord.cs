namespace CleanMenu.Core.History;

/// <summary>One operation performed by Clean Menu, kept for the Histórico page.</summary>
/// <param name="Id">Unique id.</param>
/// <param name="Time">When it finished.</param>
/// <param name="Type">Plain operation name ("Limpeza", "Simulação de limpeza"…).</param>
/// <param name="Categories">Categories involved.</param>
/// <param name="FilesRemoved">Files removed (or that would be removed, in a simulation).</param>
/// <param name="BytesRecovered">Space recovered (or that would be recovered).</param>
/// <param name="FilesSkipped">Files kept because they were in use, protected or changed.</param>
/// <param name="Errors">Plain error messages.</param>
/// <param name="Result">"Concluída", "Concluída com avisos", "Cancelada" or "Falhou".</param>
/// <param name="DryRun">True for simulations (nothing deleted).</param>
public sealed record OperationRecord(
    Guid Id,
    DateTimeOffset Time,
    string Type,
    IReadOnlyList<string> Categories,
    int FilesRemoved,
    long BytesRecovered,
    int FilesSkipped,
    IReadOnlyList<string> Errors,
    string Result,
    bool DryRun);

/// <summary>Persistent list of operations.</summary>
public interface IOperationHistory
{
    Task AddAsync(OperationRecord record, CancellationToken cancellationToken = default);

    /// <summary>All records, newest first. Damaged entries are skipped, never fatal.</summary>
    Task<IReadOnlyList<OperationRecord>> GetAllAsync(CancellationToken cancellationToken = default);

    event EventHandler? Changed;
}

public static class OperationRecords
{
    public static OperationRecord FromCleanup(Cleanup.CleanupReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var errors = report.Errors.ToList();
        var result = report.Cancelled ? "Cancelada" : errors.Count > 0 ? "Concluída com avisos" : "Concluída";
        return new OperationRecord(
            Guid.NewGuid(),
            report.FinishedAt,
            report.DryRun ? "Simulação de limpeza" : "Limpeza",
            report.Categories.Select(c => c.CategoryTitle).ToList(),
            report.DeletedCount,
            report.DeletedBytes,
            report.SkippedCount,
            errors,
            result,
            report.DryRun);
    }
}
