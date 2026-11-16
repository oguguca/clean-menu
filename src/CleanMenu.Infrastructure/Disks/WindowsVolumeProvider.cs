using CleanMenu.Core.Disks;

namespace CleanMenu.Infrastructure.Disks;

/// <summary>Fixed and removable drive letters that are ready, with space usage.</summary>
public sealed class WindowsVolumeProvider : IVolumeProvider
{
    public IReadOnlyList<VolumeInfo> GetVolumes()
    {
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var result = new List<VolumeInfo>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                {
                    continue;
                }

                result.Add(new VolumeInfo(
                    drive.Name.TrimEnd('\\'),
                    string.IsNullOrWhiteSpace(drive.VolumeLabel) ? null : drive.VolumeLabel,
                    drive.DriveFormat,
                    drive.DriveType == DriveType.Removable,
                    string.Equals(drive.Name, systemRoot, StringComparison.OrdinalIgnoreCase),
                    drive.TotalSize,
                    drive.AvailableFreeSpace));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Drive removed or locked (e.g. BitLocker) while reading.
            }
        }

        return result.OrderByDescending(v => v.IsSystem).ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
