namespace CleanMenu.Core.Monitoring;

/// <summary>
/// Physical memory as reported by Windows (GlobalMemoryStatusEx).
/// </summary>
/// <param name="TotalBytes">Memory usable by Windows.</param>
/// <param name="AvailableBytes">Memory that can be given to programs right now (free + standby cache).</param>
public sealed record MemoryStatus(long TotalBytes, long AvailableBytes)
{
    public long UsedBytes => Math.Max(0, TotalBytes - AvailableBytes);

    public double UsedPercent => TotalBytes <= 0 ? double.NaN : 100.0 * UsedBytes / TotalBytes;

    public double AvailablePercent => TotalBytes <= 0 ? double.NaN : 100.0 * AvailableBytes / TotalBytes;

    public HealthLevel Health => HealthThresholds.ForMemoryUsedPercent(UsedPercent);

    /// <summary>
    /// Memory reserved by hardware (e.g. integrated graphics), when the installed amount is known:
    /// installed modules minus what Windows can use. Null when it cannot be determined.
    /// </summary>
    public long? ReservedBytes(long? installedBytes) =>
        installedBytes is { } installed && installed >= TotalBytes ? installed - TotalBytes : null;
}
