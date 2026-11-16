using CleanMenu.Core.Disks;
using CleanMenu.Infrastructure.Wmi;

namespace CleanMenu.Infrastructure.Disks;

/// <summary>
/// Physical disks from the Windows Storage WMI provider (MSFT_PhysicalDisk). Works without admin
/// rights. SMART counters (temperature, wear) need admin and are read separately.
/// </summary>
public sealed class WindowsPhysicalDiskProvider : IPhysicalDiskProvider
{
    public Task<IReadOnlyList<PhysicalDiskInfo>> GetDisksAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<PhysicalDiskInfo>>(() =>
        {
            var rows = WmiReader.Query(
                WmiReader.Storage,
                "SELECT DeviceId, FriendlyName, MediaType, BusType, Size, HealthStatus FROM MSFT_PhysicalDisk");

            return rows
                .Select(row => new PhysicalDiskInfo(
                    row.String("DeviceId") ?? "?",
                    row.String("FriendlyName") ?? "Disco",
                    DiskText.MediaFromWmi(row.Int("MediaType")),
                    DiskText.BusFromWmi(row.Int("BusType")),
                    row.Long("Size") ?? 0,
                    DiskText.HealthFromWmi(row.Int("HealthStatus"))))
                .OrderBy(d => d.Id, StringComparer.Ordinal)
                .ToList();
        }, cancellationToken);
}
