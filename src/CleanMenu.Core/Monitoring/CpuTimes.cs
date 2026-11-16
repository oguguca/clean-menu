namespace CleanMenu.Core.Monitoring;

/// <summary>
/// Cumulative system CPU times (GetSystemTimes), in 100 ns ticks. Kernel time includes idle time.
/// </summary>
public readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User)
{
    /// <summary>
    /// CPU usage in percent between two readings, or NaN when the interval is empty or invalid.
    /// </summary>
    public static double UsagePercent(CpuTimes previous, CpuTimes current)
    {
        if (current.Idle < previous.Idle || current.Kernel < previous.Kernel || current.User < previous.User)
        {
            return double.NaN;
        }

        var idle = current.Idle - previous.Idle;
        var total = current.Kernel - previous.Kernel + (current.User - previous.User);
        if (total == 0 || idle > total)
        {
            return double.NaN;
        }

        return 100.0 * (total - idle) / total;
    }
}
