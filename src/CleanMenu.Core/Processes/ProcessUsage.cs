namespace CleanMenu.Core.Processes;

/// <summary>A process with its measured resource use.</summary>
/// <param name="CpuPercent">Share of total processor capacity (0–100); NaN until two samples exist or access is denied.</param>
public sealed record ProcessUsage(
    int Pid,
    string Name,
    long MemoryBytes,
    double CpuPercent,
    ProcessKind Kind,
    string? WindowTitle)
{
    public bool IsProtected => ProcessClassifier.IsProtected(Name, Pid);

    public string Advice => ProcessClassifier.Advice(Kind);
}

/// <summary>
/// Turns successive process snapshots into CPU percentages (CPU time delta divided by elapsed time
/// times the number of logical processors, as Task Manager does).
/// </summary>
public sealed class ProcessUsageTracker(int logicalProcessors)
{
    private readonly int _logicalProcessors = Math.Max(1, logicalProcessors);
    private Dictionary<int, TimeSpan> _previousCpu = [];
    private DateTimeOffset? _previousTime;

    public IReadOnlyList<ProcessUsage> Update(IReadOnlyList<ProcessSnapshot> snapshots, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        var elapsed = _previousTime is { } previous ? now - previous : TimeSpan.Zero;
        var capacity = elapsed.TotalMilliseconds * _logicalProcessors;
        var currentCpu = new Dictionary<int, TimeSpan>(snapshots.Count);
        var result = new List<ProcessUsage>(snapshots.Count);

        foreach (var snapshot in snapshots)
        {
            var cpuPercent = double.NaN;
            if (snapshot.CpuTime is { } cpu)
            {
                currentCpu[snapshot.Pid] = cpu;
                if (capacity > 0 && _previousCpu.TryGetValue(snapshot.Pid, out var before) && cpu >= before)
                {
                    cpuPercent = Math.Clamp(100.0 * (cpu - before).TotalMilliseconds / capacity, 0, 100);
                }
            }

            result.Add(new ProcessUsage(
                snapshot.Pid,
                snapshot.Name,
                snapshot.MemoryBytes,
                cpuPercent,
                ProcessClassifier.Classify(snapshot.Name, snapshot.HasWindow),
                snapshot.WindowTitle));
        }

        _previousCpu = currentCpu;
        _previousTime = now;
        return result;
    }
}
