using CleanMenu.App.Services;
using CleanMenu.Core.Disks;
using CleanMenu.Core.Monitoring;
using CleanMenu.Core.Processes;
using CleanMenu.Core.SystemInfo;

namespace CleanMenu.App.Tests;

internal sealed class FakeLiveMonitor : ILiveMonitor
{
    public event EventHandler<LiveSample>? SampleTaken;

    public LiveSample? Latest { get; private set; }

    public TimeSpan Interval => TimeSpan.FromSeconds(2);

    public IReadOnlyList<double> CpuHistory { get; set; } = [];

    public IReadOnlyList<double> MemoryHistory { get; set; } = [];

    public IReadOnlyList<double> GpuHistory { get; set; } = [];

    public void Raise(LiveSample sample)
    {
        Latest = sample;
        SampleTaken?.Invoke(this, sample);
    }
}

internal sealed class FakeSystemInfo(SystemInformation? info = null) : ISystemInfoProvider
{
    public static SystemInformation Sample { get; } = new(
        new ComputerDetails("PC-TESTE", "Fabricante X", "Modelo Y"),
        new OperatingSystemDetails("Microsoft Windows 11 Home", "24H2", "10.0.26100", 26100, "X64", null, null),
        new ProcessorDetails("Processador Teste", 4, 8, 3200),
        new MemoryDetails(16L * 1024 * 1024 * 1024, 15L * 1024 * 1024 * 1024, 2, 3200),
        [new GraphicsDetails("Vídeo Teste", "1.0", null)]);

    public Task<SystemInformation> GetAsync(CancellationToken cancellationToken = default) =>
        info is null ? Task.FromException<SystemInformation>(new InvalidOperationException("no wmi")) : Task.FromResult(info);
}

internal sealed class FakeVolumes(params VolumeInfo[] volumes) : IVolumeProvider
{
    public IReadOnlyList<VolumeInfo> GetVolumes() => volumes;
}

internal sealed class FakeDisks(params PhysicalDiskInfo[] disks) : IPhysicalDiskProvider
{
    public Task<IReadOnlyList<PhysicalDiskInfo>> GetDisksAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PhysicalDiskInfo>>(disks);
}

internal sealed class FakeProcessSource : IProcessSource
{
    public IReadOnlyList<ProcessSnapshot> Next { get; set; } = [];

    public IReadOnlyList<ProcessSnapshot> Capture() => Next;
}

internal sealed class FakeActions : ISystemActions
{
    public bool OpenResult { get; set; } = true;

    public List<string> Opened { get; } = [];

    public string? Clipboard { get; private set; }

    public bool OpenTool(string fileName, string? arguments = null)
    {
        Opened.Add(fileName);
        return OpenResult;
    }

    public void CopyToClipboard(string text) => Clipboard = text;
}

internal sealed class ManualTime(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class Gb
{
    public static long Of(double gigabytes) => (long)(gigabytes * 1024 * 1024 * 1024);
}
