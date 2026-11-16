using CleanMenu.Core.Disks;
using CleanMenu.Core.SystemInfo;

namespace CleanMenu.Core.Tests.Disks;

public class DiskAndReportTests
{
    private const long Gb = 1024L * 1024 * 1024;

    [Theory]
    [InlineData(3, DiskMedia.Hdd)]
    [InlineData(4, DiskMedia.Ssd)]
    [InlineData(5, DiskMedia.Ssd)]
    [InlineData(0, DiskMedia.Unknown)]
    [InlineData(null, DiskMedia.Unknown)]
    public void MediaFromWmi(int? mediaType, DiskMedia expected)
    {
        Assert.Equal(expected, DiskText.MediaFromWmi(mediaType));
    }

    [Theory]
    [InlineData(0, ReportedDiskHealth.NoProblemsReported)]
    [InlineData(1, ReportedDiskHealth.Warning)]
    [InlineData(2, ReportedDiskHealth.Unhealthy)]
    [InlineData(5, ReportedDiskHealth.Unknown)]
    [InlineData(null, ReportedDiskHealth.Unknown)]
    public void HealthFromWmi(int? status, ReportedDiskHealth expected)
    {
        Assert.Equal(expected, DiskText.HealthFromWmi(status));
    }

    [Fact]
    public void HealthText_NeverClaimsHealthy()
    {
        foreach (var health in Enum.GetValues<ReportedDiskHealth>())
        {
            Assert.DoesNotContain("saudável", DiskText.HealthText(health), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Volume_ComputesUsage()
    {
        var volume = new VolumeInfo("C:", null, "NTFS", false, true, 100 * Gb, 25 * Gb);

        Assert.Equal(75 * Gb, volume.UsedBytes);
        Assert.Equal(75, volume.UsedPercent, 3);
    }

    [Fact]
    public void Report_ContainsHardwareFacts_AndNoPersonalPaths()
    {
        var info = new SystemInformation(
            new ComputerDetails("PC", "Marca", "Modelo"),
            new OperatingSystemDetails("Microsoft Windows 11 Pro", "24H2", "10.0.26100", 26100, "X64", null, null),
            new ProcessorDetails("CPU X", 6, 12, 4000),
            new MemoryDetails(16 * Gb, 15 * Gb, 2, 3200),
            [new GraphicsDetails("GPU Y", "31.0", null)]);

        var report = SystemReport.Build(
            info,
            [new VolumeInfo("C:", null, "NTFS", false, true, 500 * Gb, 100 * Gb)],
            [new PhysicalDiskInfo("0", "Disco Z", DiskMedia.Ssd, "NVMe", 512 * Gb, ReportedDiskHealth.NoProblemsReported)]);

        Assert.Contains("Processador: CPU X", report, StringComparison.Ordinal);
        Assert.Contains("Memória instalada: 16 GB", report, StringComparison.Ordinal);
        Assert.Contains("Placa de vídeo: GPU Y", report, StringComparison.Ordinal);
        Assert.Contains("Disco: Disco Z — SSD — 512 GB", report, StringComparison.Ordinal);
        Assert.Contains("Unidade C:: 100 GB livres de 500 GB (NTFS)", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Users", report, StringComparison.OrdinalIgnoreCase);
    }
}
