namespace CleanMenu.Core.Processes;

/// <summary>A process whose memory only kept growing during the observation window.</summary>
public sealed record MemoryGrowthSuspect(int Pid, string Name, long StartBytes, long CurrentBytes, TimeSpan ObservedFor)
{
    public long GrowthBytes => CurrentBytes - StartBytes;
}

/// <summary>
/// Flags possible memory leaks only with evidence: during at least <see cref="MinimumObservation"/>
/// the process memory grew by at least <see cref="MinimumGrowthBytes"/> and at least 50%, and it
/// grew (or stayed equal) in at least 90% of the readings. A program that simply uses a lot of
/// memory is never flagged.
/// </summary>
public sealed class MemoryGrowthDetector
{
    public static readonly TimeSpan MinimumObservation = TimeSpan.FromMinutes(10);
    public const long MinimumGrowthBytes = 300L * 1024 * 1024;
    private const int MaxReadingsPerProcess = 720;

    private readonly Dictionary<int, Series> _series = [];

    public void Record(IEnumerable<ProcessUsage> processes, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(processes);

        var seen = new HashSet<int>();
        foreach (var process in processes)
        {
            seen.Add(process.Pid);
            if (!_series.TryGetValue(process.Pid, out var series) || series.Name != process.Name)
            {
                series = new Series(process.Name);
                _series[process.Pid] = series;
            }

            series.Add(now, process.MemoryBytes);
        }

        // Forget processes that ended (their pid may be reused).
        foreach (var pid in _series.Keys.Where(pid => !seen.Contains(pid)).ToList())
        {
            _series.Remove(pid);
        }
    }

    public IReadOnlyList<MemoryGrowthSuspect> Suspects()
    {
        var result = new List<MemoryGrowthSuspect>();
        foreach (var (pid, series) in _series)
        {
            if (series.Readings.Count < 3)
            {
                continue;
            }

            var first = series.Readings[0];
            var last = series.Readings[^1];
            var observed = last.Time - first.Time;
            var growth = last.Bytes - first.Bytes;
            if (observed < MinimumObservation || growth < MinimumGrowthBytes || last.Bytes < first.Bytes * 1.5)
            {
                continue;
            }

            var steps = series.Readings.Count - 1;
            var nonDecreasing = 0;
            for (var i = 1; i < series.Readings.Count; i++)
            {
                if (series.Readings[i].Bytes >= series.Readings[i - 1].Bytes)
                {
                    nonDecreasing++;
                }
            }

            if (nonDecreasing >= steps * 0.9)
            {
                result.Add(new MemoryGrowthSuspect(pid, series.Name, first.Bytes, last.Bytes, observed));
            }
        }

        return result.OrderByDescending(s => s.GrowthBytes).ToList();
    }

    private sealed class Series(string name)
    {
        public string Name { get; } = name;

        public List<(DateTimeOffset Time, long Bytes)> Readings { get; } = [];

        public void Add(DateTimeOffset time, long bytes)
        {
            Readings.Add((time, bytes));
            if (Readings.Count > MaxReadingsPerProcess)
            {
                Readings.RemoveAt(0);
            }
        }
    }
}
