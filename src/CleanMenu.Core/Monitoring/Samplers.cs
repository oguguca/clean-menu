namespace CleanMenu.Core.Monitoring;

/// <summary>Reads the current physical memory status.</summary>
public interface IMemoryStatusProvider
{
    MemoryStatus GetStatus();
}

/// <summary>Reads cumulative system CPU times.</summary>
public interface ICpuTimesProvider
{
    CpuTimes GetTimes();
}

/// <summary>
/// Samples GPU usage. Returns null when unsupported (no GPU counters) and NaN on the first call.
/// </summary>
public interface IGpuUsageSampler
{
    double? Sample();
}
