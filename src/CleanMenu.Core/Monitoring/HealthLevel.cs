namespace CleanMenu.Core.Monitoring;

/// <summary>
/// Judgement of a measured value. <see cref="Unknown"/> is used whenever there is not enough real data.
/// </summary>
public enum HealthLevel
{
    Unknown,
    Good,
    Attention,
    Critical,
}

/// <summary>
/// Thresholds used to judge real measurements. Kept in one place so they are documented and tested.
/// </summary>
public static class HealthThresholds
{
    /// <summary>Memory: up to 75% used is fine, up to 90% deserves attention, above is critical.</summary>
    public static HealthLevel ForMemoryUsedPercent(double usedPercent) => usedPercent switch
    {
        double.NaN => HealthLevel.Unknown,
        < 0 => HealthLevel.Unknown,
        <= 75 => HealthLevel.Good,
        <= 90 => HealthLevel.Attention,
        _ => HealthLevel.Critical,
    };

    /// <summary>
    /// Disk: Windows warns below ~10% free. More than 15% free is fine, 5–15% deserves attention,
    /// less than 5% (or under 2 GB on the system drive) is critical.
    /// </summary>
    public static HealthLevel ForDiskFree(long totalBytes, long freeBytes)
    {
        if (totalBytes <= 0 || freeBytes < 0)
        {
            return HealthLevel.Unknown;
        }

        const long twoGigabytes = 2L * 1024 * 1024 * 1024;
        var freePercent = 100.0 * freeBytes / totalBytes;
        if (freePercent < 5 || freeBytes < twoGigabytes)
        {
            return HealthLevel.Critical;
        }

        return freePercent < 15 ? HealthLevel.Attention : HealthLevel.Good;
    }

    /// <summary>CPU: sustained use is judged by the caller; a single sample above 90% only deserves attention.</summary>
    public static HealthLevel ForCpuPercent(double percent) => percent switch
    {
        double.NaN => HealthLevel.Unknown,
        < 0 => HealthLevel.Unknown,
        <= 80 => HealthLevel.Good,
        _ => HealthLevel.Attention,
    };
}
