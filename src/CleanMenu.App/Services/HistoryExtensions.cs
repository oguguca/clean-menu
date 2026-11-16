using CleanMenu.Core.History;

namespace CleanMenu.App.Services;

/// <summary>Records non-cleanup operations (startup changes, maintenance tools…) in the history.</summary>
public static class HistoryExtensions
{
    public static Task RecordActionAsync(this IOperationHistory history, string type, string subject, bool succeeded, string? error = null)
    {
        ArgumentNullException.ThrowIfNull(history);
        return history.AddAsync(new OperationRecord(
            Guid.NewGuid(),
            DateTimeOffset.Now,
            type,
            [subject],
            0,
            0,
            0,
            error is null ? [] : [error],
            succeeded ? "Concluída" : "Falhou",
            false));
    }
}
