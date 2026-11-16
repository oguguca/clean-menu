using CleanMenu.Core.Processes;

namespace CleanMenu.App.Services;

/// <summary>
/// Keeps the latest process list with CPU percentages and feeds the memory growth detector.
/// Thread-safe; <see cref="Refresh"/> may be called from any thread.
/// </summary>
public sealed class ProcessMonitor(IProcessSource source, TimeProvider time)
{
    private readonly ProcessUsageTracker _tracker = new(Environment.ProcessorCount);
    private readonly MemoryGrowthDetector _growth = new();
    private readonly Lock _lock = new();
    private IReadOnlyList<ProcessUsage> _latest = [];

    public event EventHandler? Updated;

    public IReadOnlyList<ProcessUsage> Latest
    {
        get
        {
            lock (_lock)
            {
                return _latest;
            }
        }
    }

    public IReadOnlyList<MemoryGrowthSuspect> GrowthSuspects
    {
        get
        {
            lock (_lock)
            {
                return _growth.Suspects();
            }
        }
    }

    public IReadOnlyList<ProcessUsage> Refresh()
    {
        var snapshots = source.Capture();
        IReadOnlyList<ProcessUsage> usage;
        lock (_lock)
        {
            var now = time.GetUtcNow();
            usage = _tracker.Update(snapshots, now);
            _growth.Record(usage, now);
            _latest = usage;
        }

        Updated?.Invoke(this, EventArgs.Empty);
        return usage;
    }
}
