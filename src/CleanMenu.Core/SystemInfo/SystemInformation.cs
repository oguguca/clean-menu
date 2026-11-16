namespace CleanMenu.Core.SystemInfo;

/// <summary>Static hardware and software facts. Any field Windows does not report is null.</summary>
public sealed record SystemInformation(
    ComputerDetails Computer,
    OperatingSystemDetails OperatingSystem,
    ProcessorDetails? Processor,
    MemoryDetails Memory,
    IReadOnlyList<GraphicsDetails> Graphics);

public sealed record ComputerDetails(string MachineName, string? Manufacturer, string? Model);

public sealed record OperatingSystemDetails(
    string? Caption,
    string? DisplayVersion,
    string Version,
    int Build,
    string Architecture,
    DateTimeOffset? InstalledOn,
    DateTimeOffset? LastBoot);

public sealed record ProcessorDetails(string Name, int? Cores, int LogicalProcessors, int? MaxClockMHz);

/// <param name="InstalledBytes">Sum of the memory modules; null when Windows does not list them.</param>
/// <param name="UsableBytes">Memory Windows can use.</param>
/// <param name="ModuleCount">Number of memory modules found.</param>
/// <param name="SpeedMHz">Configured speed of the first module, when reported.</param>
public sealed record MemoryDetails(long? InstalledBytes, long UsableBytes, int? ModuleCount, int? SpeedMHz);

/// <param name="DedicatedMemoryBytes">Video memory; Windows caps this value at 4 GB on many systems, so it is informative only.</param>
public sealed record GraphicsDetails(string Name, string? DriverVersion, long? DedicatedMemoryBytes);

/// <summary>Reads <see cref="SystemInformation"/>. May take a few seconds (WMI); call off the UI thread.</summary>
public interface ISystemInfoProvider
{
    Task<SystemInformation> GetAsync(CancellationToken cancellationToken = default);
}
