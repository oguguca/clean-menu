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

    public Task<IReadOnlyDictionary<string, DiskMedia>> GetVolumeMediaAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyDictionary<string, DiskMedia>>(() =>
        {
            var media = WmiReader.Query(WmiReader.Storage, "SELECT DeviceId, MediaType FROM MSFT_PhysicalDisk")
                .Where(r => r.String("DeviceId") is not null)
                .ToDictionary(r => r.String("DeviceId")!, r => DiskText.MediaFromWmi(r.Int("MediaType")), StringComparer.Ordinal);

            var result = new Dictionary<string, DiskMedia>(StringComparer.OrdinalIgnoreCase);
            foreach (var partition in WmiReader.Query(WmiReader.Storage, "SELECT DriveLetter, DiskNumber FROM MSFT_Partition"))
            {
                var letter = partition.Int("DriveLetter") is int code and > 0 ? ((char)code).ToString() : partition.String("DriveLetter");
                var disk = partition.Long("DiskNumber")?.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (letter is { Length: 1 } && char.IsLetter(letter[0]) && disk is not null && media.TryGetValue(disk, out var type))
                {
                    result[letter] = type;
                }
            }

            return result;
        }, cancellationToken);
}
