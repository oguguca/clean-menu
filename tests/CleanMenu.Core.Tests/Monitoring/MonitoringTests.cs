using CleanMenu.Core.Formatting;
using CleanMenu.Core.Monitoring;

namespace CleanMenu.Core.Tests.Monitoring;

public class MonitoringTests
{
    private const long Gb = 1024L * 1024 * 1024;

    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(1, "1 byte")]
    [InlineData(1023, "1023 bytes")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1,5 KB")]
    [InlineData(10L * 1024 * 1024, "10 MB")]
    [InlineData(1288490189L, "1,2 GB")]
    [InlineData(500L * 1024 * 1024 * 1024, "500 GB")]
    [InlineData(2L * 1024 * 1024 * 1024 * 1024, "2 TB")]
    public void ByteSize_FormatsLikeExplorer_InPortuguese(long bytes, string expected)
    {
        Assert.Equal(expected, ByteSize.Format(bytes));
    }

    [Theory]
    [InlineData(50, HealthLevel.Good)]
    [InlineData(75, HealthLevel.Good)]
    [InlineData(80, HealthLevel.Attention)]
    [InlineData(95, HealthLevel.Critical)]
    [InlineData(double.NaN, HealthLevel.Unknown)]
    public void MemoryThresholds(double usedPercent, HealthLevel expected)
    {
        Assert.Equal(expected, HealthThresholds.ForMemoryUsedPercent(usedPercent));
    }

    [Theory]
    [InlineData(500, 200, HealthLevel.Good)]       // 40% free
    [InlineData(500, 50, HealthLevel.Attention)]   // 10% free
    [InlineData(500, 20, HealthLevel.Critical)]    // 4% free
    [InlineData(32, 1.5, HealthLevel.Critical)]    // under 2 GB
    [InlineData(0, 0, HealthLevel.Unknown)]
    public void DiskThresholds(double totalGb, double freeGb, HealthLevel expected)
    {
        Assert.Equal(expected, HealthThresholds.ForDiskFree((long)(totalGb * Gb), (long)(freeGb * Gb)));
    }

    [Fact]
    public void MemoryStatus_ComputesUsage()
    {
        var status = new MemoryStatus(16 * Gb, 4 * Gb);

        Assert.Equal(12 * Gb, status.UsedBytes);
        Assert.Equal(75, status.UsedPercent, 3);
        Assert.Equal(25, status.AvailablePercent, 3);
        Assert.Equal(HealthLevel.Good, status.Health);
    }

    [Fact]
    public void MemoryStatus_Reserved_OnlyWhenInstalledKnown()
    {
        var status = new MemoryStatus(15 * Gb, 4 * Gb);

        Assert.Equal(1 * Gb, status.ReservedBytes(16 * Gb));
        Assert.Null(status.ReservedBytes(null));
        Assert.Null(status.ReservedBytes(8 * Gb));
    }

    [Fact]
    public void CpuTimes_ComputesUsageBetweenReadings()
    {
        var before = new CpuTimes(Idle: 1000, Kernel: 2000, User: 1000);
        var after = new CpuTimes(Idle: 1300, Kernel: 2400, User: 1600); // total 1000, idle 300

        Assert.Equal(70, CpuTimes.UsagePercent(before, after), 3);
    }

    [Fact]
    public void CpuTimes_InvalidIntervals_AreNaN()
    {
        var reading = new CpuTimes(10, 20, 30);

        Assert.True(double.IsNaN(CpuTimes.UsagePercent(reading, reading)));
        Assert.True(double.IsNaN(CpuTimes.UsagePercent(new CpuTimes(50, 50, 50), reading)));
    }

    [Fact]
    public void SampleHistory_KeepsLatestValues()
    {
        var history = new SampleHistory(3);
        foreach (var value in new double[] { 1, 2, 3, 4, 5 })
        {
            history.Add(value);
        }

        Assert.Equal([3d, 4d, 5d], history.Snapshot());
        Assert.Equal(3, history.Count);
    }

    [Fact]
    public void SampleHistory_RejectsZeroCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleHistory(0));
    }
}
