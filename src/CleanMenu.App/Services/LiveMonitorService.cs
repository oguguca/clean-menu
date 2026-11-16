using CleanMenu.Core.Monitoring;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CleanMenu.App.Services;

/// <summary>
/// Background loop that reads CPU, GPU and memory every <see cref="Interval"/> and refreshes the
/// process list every few readings (needed to detect memory that keeps growing).
/// All reads are passive: nothing on the computer is changed.
/// </summary>
public sealed partial class LiveMonitorService(
    ICpuTimesProvider cpu,
    IGpuUsageSampler gpu,
    IMemoryStatusProvider memory,
    ProcessMonitor processes,
    TimeProvider time,
    ILogger<LiveMonitorService> logger) : BackgroundService, ILiveMonitor
{
    public const int HistoryLength = 60;
    private const int ProcessRefreshEvery = 5;

    private readonly SampleHistory _cpuHistory = new(HistoryLength);
    private readonly SampleHistory _memoryHistory = new(HistoryLength);
    private readonly SampleHistory _gpuHistory = new(HistoryLength);
    private CpuTimes? _previousCpu;
    private volatile LiveSample? _latest;

    public event EventHandler<LiveSample>? SampleTaken;

    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(2);

    public LiveSample? Latest => _latest;

    public IReadOnlyList<double> CpuHistory => _cpuHistory.Snapshot();

    public IReadOnlyList<double> MemoryHistory => _memoryHistory.Snapshot();

    public IReadOnlyList<double> GpuHistory => _gpuHistory.Snapshot();

    /// <summary>Takes one reading. Public so it can be exercised directly in tests.</summary>
    public LiveSample TakeSample()
    {
        var cpuTimes = cpu.GetTimes();
        var cpuPercent = _previousCpu is { } previous ? CpuTimes.UsagePercent(previous, cpuTimes) : double.NaN;
        _previousCpu = cpuTimes;

        var sample = new LiveSample(time.GetUtcNow(), cpuPercent, gpu.Sample(), memory.GetStatus());

        if (!double.IsNaN(sample.CpuPercent))
        {
            _cpuHistory.Add(sample.CpuPercent);
        }

        if (!double.IsNaN(sample.Memory.UsedPercent))
        {
            _memoryHistory.Add(sample.Memory.UsedPercent);
        }

        if (sample.GpuPercent is { } gpuPercent && !double.IsNaN(gpuPercent))
        {
            _gpuHistory.Add(gpuPercent);
        }

        _latest = sample;
        SampleTaken?.Invoke(this, sample);
        return sample;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        var tick = 0;
        do
        {
            try
            {
                TakeSample();
                if (tick++ % ProcessRefreshEvery == 0)
                {
                    processes.Refresh();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Monitoring must never crash the app; log and try again on the next tick.
                LogSampleFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Live monitoring sample failed")]
    private static partial void LogSampleFailed(ILogger logger, Exception exception);
}
