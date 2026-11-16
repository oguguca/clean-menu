using CleanMenu.Core.Monitoring;

namespace CleanMenu.Core.Disks;

/// <summary>Type of storage device.</summary>
public enum DiskMedia
{
    Unknown,
    Hdd,
    Ssd,
}

/// <summary>Health as reported by Windows Storage (MSFT_PhysicalDisk.HealthStatus).</summary>
public enum ReportedDiskHealth
{
    /// <summary>Windows gave no information. Clean Menu must not claim the disk is fine.</summary>
    Unknown,
    NoProblemsReported,
    Warning,
    Unhealthy,
}

/// <summary>A drive letter (volume) with its space usage.</summary>
public sealed record VolumeInfo(
    string Name,
    string? Label,
    string FileSystem,
    bool IsRemovable,
    bool IsSystem,
    long TotalBytes,
    long FreeBytes)
{
    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);

    public double UsedPercent => TotalBytes <= 0 ? double.NaN : 100.0 * UsedBytes / TotalBytes;

    public HealthLevel SpaceHealth => HealthThresholds.ForDiskFree(TotalBytes, FreeBytes);
}

/// <summary>A physical storage device.</summary>
public sealed record PhysicalDiskInfo(
    string Id,
    string Model,
    DiskMedia Media,
    string? BusType,
    long SizeBytes,
    ReportedDiskHealth Health);

/// <summary>Lists volumes (drive letters).</summary>
public interface IVolumeProvider
{
    IReadOnlyList<VolumeInfo> GetVolumes();
}

/// <summary>Lists physical disks. May take a moment (WMI); call off the UI thread.</summary>
public interface IPhysicalDiskProvider
{
    Task<IReadOnlyList<PhysicalDiskInfo>> GetDisksAsync(CancellationToken cancellationToken = default);

    /// <summary>Type of the physical disk behind each drive letter ("C" → SSD).</summary>
    Task<IReadOnlyDictionary<string, DiskMedia>> GetVolumeMediaAsync(CancellationToken cancellationToken = default);
}

/// <summary>Plain-language names for disk facts.</summary>
public static class DiskText
{
    public static string MediaName(DiskMedia media) => media switch
    {
        DiskMedia.Ssd => "SSD",
        DiskMedia.Hdd => "HD (disco rígido)",
        _ => "Tipo não informado",
    };

    public static string HealthText(ReportedDiskHealth health) => health switch
    {
        ReportedDiskHealth.NoProblemsReported => "O Windows não relatou problemas",
        ReportedDiskHealth.Warning => "O Windows relatou um alerta",
        ReportedDiskHealth.Unhealthy => "O Windows relatou falha. Faça backup dos seus arquivos",
        _ => "Sem informações de saúde",
    };

    /// <summary>Maps MSFT_PhysicalDisk.MediaType (3 = HDD, 4 = SSD, 5 = SCM).</summary>
    public static DiskMedia MediaFromWmi(int? mediaType) => mediaType switch
    {
        3 => DiskMedia.Hdd,
        4 or 5 => DiskMedia.Ssd,
        _ => DiskMedia.Unknown,
    };

    /// <summary>Maps MSFT_PhysicalDisk.HealthStatus (0 = Healthy, 1 = Warning, 2 = Unhealthy, 5 = Unknown).</summary>
    public static ReportedDiskHealth HealthFromWmi(int? healthStatus) => healthStatus switch
    {
        0 => ReportedDiskHealth.NoProblemsReported,
        1 => ReportedDiskHealth.Warning,
        2 => ReportedDiskHealth.Unhealthy,
        _ => ReportedDiskHealth.Unknown,
    };

    /// <summary>Maps MSFT_PhysicalDisk.BusType to a readable name.</summary>
    public static string? BusFromWmi(int? busType) => busType switch
    {
        3 => "ATA",
        7 => "USB",
        8 => "RAID",
        10 => "SAS",
        11 => "SATA",
        12 => "Cartão SD",
        17 => "NVMe",
        _ => null,
    };
}
