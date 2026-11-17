using CleanMenu.Core.SystemInfo;
using CleanMenu.Infrastructure.Wmi;

namespace CleanMenu.Infrastructure.SystemInfo;

/// <summary>Reads Security Center, services, USB devices and page files through WMI. Read-only.</summary>
public sealed class WindowsDeviceStatusProvider : IDeviceStatusProvider
{
    private const string SecurityCenter = @"root\SecurityCenter2";

    public IReadOnlyList<AntivirusProduct> GetAntivirus() =>
        WmiReader.Query(SecurityCenter, "SELECT displayName, productState FROM AntiVirusProduct")
            .Select(r => AntivirusProduct.FromState(r.String("displayName") ?? "Antivírus", r.Long("productState")))
            .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

    public IReadOnlyList<ServiceStatusInfo> GetImportantServices()
    {
        var names = string.Join(" OR ", ImportantServices.All.Select(d => $"Name='{d.Name}'"));
        var rows = WmiReader.Query(WmiReader.Cimv2, $"SELECT Name, State, StartMode FROM Win32_Service WHERE {names}")
            .ToDictionary(r => r.String("Name") ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        return ImportantServices.All
            .Where(d => rows.ContainsKey(d.Name))
            .Select(d =>
            {
                var row = rows[d.Name];
                return new ServiceStatusInfo(d.Name, d.Title, d.Explanation,
                    string.Equals(row.String("State"), "Running", StringComparison.OrdinalIgnoreCase),
                    ImportantServices.ParseStartMode(row.String("StartMode")),
                    d.IsSecurity);
            })
            .ToList();
    }

    public IReadOnlyList<UsbDeviceInfo> GetUsbDevices() =>
        WmiReader.Query(WmiReader.Cimv2,
                "SELECT Name, PNPClass, Status, PNPDeviceID FROM Win32_PnPEntity WHERE PNPDeviceID LIKE 'USB%' OR PNPDeviceID LIKE 'USBSTOR%'")
            .Where(r => r.String("Name") is not null)
            // Hubs, controllers and "composite" parents are plumbing, not devices the user plugged in.
            .Where(r => !string.Equals(r.String("PNPClass"), "USB", StringComparison.OrdinalIgnoreCase))
            .Select(r => new UsbDeviceInfo(
                r.String("Name")!,
                ImportantServices.DeviceKind(r.String("PNPClass")),
                string.Equals(r.String("Status"), "OK", StringComparison.OrdinalIgnoreCase)))
            .DistinctBy(d => (d.Name, d.Kind))
            .OrderBy(d => d.Kind, StringComparer.CurrentCulture)
            .ThenBy(d => d.Name, StringComparer.CurrentCulture)
            .ToList();

    public IReadOnlyList<PageFileInfo> GetPageFiles() =>
        WmiReader.Query(WmiReader.Cimv2, "SELECT Name, AllocatedBaseSize, CurrentUsage, PeakUsage FROM Win32_PageFileUsage")
            .Select(r => new PageFileInfo(
                r.String("Name") ?? "?",
                r.Long("AllocatedBaseSize") ?? 0,
                r.Long("CurrentUsage") ?? 0,
                r.Long("PeakUsage") ?? 0))
            .ToList();
}
