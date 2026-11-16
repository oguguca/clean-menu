namespace CleanMenu.Core.Monitoring;

/// <summary>One reading of the live indicators.</summary>
/// <param name="Time">When the reading was taken.</param>
/// <param name="CpuPercent">Total processor use; NaN on the first reading (needs two samples).</param>
/// <param name="GpuPercent">Busiest GPU engine; null when Windows does not expose GPU counters.</param>
/// <param name="Memory">Physical memory status.</param>
public sealed record LiveSample(DateTimeOffset Time, double CpuPercent, double? GpuPercent, MemoryStatus Memory);

/// <summary>
/// Periodically reads CPU, GPU and memory. Raises <see cref="SampleTaken"/> on a background thread.
/// </summary>
public interface ILiveMonitor
{
    event EventHandler<LiveSample>? SampleTaken;

    LiveSample? Latest { get; }

    TimeSpan Interval { get; }

    IReadOnlyList<double> CpuHistory { get; }

    IReadOnlyList<double> MemoryHistory { get; }

    IReadOnlyList<double> GpuHistory { get; }
}
