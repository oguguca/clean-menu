using CleanMenu.Core.Monitoring;
using CleanMenu.Infrastructure.Disks;
using CleanMenu.Infrastructure.Monitoring;
using CleanMenu.Infrastructure.Processes;
using CleanMenu.Infrastructure.SystemInfo;

namespace CleanMenu.Infrastructure.Tests.Monitoring;

/// <summary>
/// Read-only integration tests against the real machine. They only read information;
/// nothing is created, changed or deleted.
/// </summary>
public class MonitoringProviderTests
{
    [Fact]
    public void GpuAggregator_SumsPerEngine_AndReturnsBusiest()
    {
        var instances = new Dictionary<string, double>
        {
            ["pid_100_luid_0x00000000_0x0000D1A2_phys_0_eng_0_engtype_3D"] = 20,
            ["pid_200_luid_0x00000000_0x0000D1A2_phys_0_eng_0_engtype_3D"] = 15,
            ["pid_100_luid_0x00000000_0x0000D1A2_phys_0_eng_3_engtype_VideoDecode"] = 30,
            ["garbage"] = 99,
        };

        Assert.Equal(35, GpuEngineAggregator.BusiestEnginePercent(instances));
    }

    [Fact]
    public void GpuAggregator_NoParsableInstances_IsNull()
    {
        Assert.Null(GpuEngineAggregator.BusiestEnginePercent([new("x", 10)]));
    }

    [Fact]
    public void Memory_ReturnsPlausibleValues()
    {
        var status = new WindowsMemoryStatusProvider().GetStatus();

        Assert.True(status.TotalBytes > 512L * 1024 * 1024);
        Assert.InRange(status.AvailableBytes, 0, status.TotalBytes);
    }

    [Fact]
    public void CpuTimes_Increase()
    {
        var provider = new WindowsCpuTimesProvider();
        var first = provider.GetTimes();
        Thread.Sleep(50);
        var second = provider.GetTimes();

        var usage = CpuTimes.UsagePercent(first, second);
        Assert.True(double.IsNaN(usage) || usage is >= 0 and <= 100);
        Assert.True(second.Kernel >= first.Kernel);
    }

    [Fact]
    public void GpuSampler_NeverThrows()
    {
        var sampler = new WindowsGpuUsageSampler();
        var first = sampler.Sample();
        var second = sampler.Sample();

        Assert.True(first is null || double.IsNaN(first.Value));
        Assert.True(second is null or (>= 0 and <= 100));
    }

    [Fact]
    public void Volumes_IncludeSystemDrive()
    {
        var volumes = new WindowsVolumeProvider().GetVolumes();

        var system = Assert.Single(volumes, v => v.IsSystem);
        Assert.True(system.TotalBytes > 0);
        Assert.Same(system, volumes[0]);
    }

    [Fact]
    public void Processes_IncludeCurrentProcess()
    {
        var snapshots = new WindowsProcessSource().Capture();

        Assert.Contains(snapshots, s => s.Pid == Environment.ProcessId && s.MemoryBytes > 0);
    }

    [Fact]
    public async Task SystemInfo_ReadsProcessorAndMemory()
    {
        var info = await new WindowsSystemInfoProvider(new WindowsMemoryStatusProvider()).GetAsync();

        Assert.Equal(Environment.MachineName, info.Computer.MachineName);
        Assert.True(info.Memory.UsableBytes > 0);
        Assert.True(info.OperatingSystem.Build >= 17763);
    }

    [Fact]
    public async Task PhysicalDisks_DoNotThrow()
    {
        var disks = await new WindowsPhysicalDiskProvider().GetDisksAsync();

        Assert.All(disks, d => Assert.False(string.IsNullOrWhiteSpace(d.Model)));
    }
}
