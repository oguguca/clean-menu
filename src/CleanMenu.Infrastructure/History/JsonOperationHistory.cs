using System.Text.Json;
using CleanMenu.Core.History;

namespace CleanMenu.Infrastructure.History;

/// <summary>
/// Operation history stored as one JSON object per line in a local file
/// (%LOCALAPPDATA%\CleanMenu\history.jsonl by default). Appending never rewrites earlier entries,
/// and a damaged line is skipped instead of breaking the whole history.
/// </summary>
public sealed class JsonOperationHistory(string filePath) : IOperationHistory, IDisposable
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonOperationHistory()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CleanMenu", "history.jsonl"))
    {
    }

    public event EventHandler? Changed;

    public void Dispose() => _gate.Dispose();

    public string FilePath { get; } = filePath;

    public async Task AddAsync(OperationRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var line = JsonSerializer.Serialize(record, Options) + Environment.NewLine;
            await File.AppendAllTextAsync(FilePath, line, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<IReadOnlyList<OperationRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            var records = new List<OperationRecord>();
            foreach (var line in await File.ReadAllLinesAsync(FilePath, cancellationToken).ConfigureAwait(false))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    if (JsonSerializer.Deserialize<OperationRecord>(line, Options) is { } record)
                    {
                        records.Add(record);
                    }
                }
                catch (JsonException)
                {
                    // Damaged line (e.g. power loss while writing). Skip it.
                }
            }

            return records.OrderByDescending(r => r.Time).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }
}
