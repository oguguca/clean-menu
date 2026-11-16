using System.Runtime.InteropServices;
using CleanMenu.Core.Monitoring;
using CleanMenu.Core.SystemInfo;
using CleanMenu.Infrastructure.Wmi;
using Microsoft.Win32;

namespace CleanMenu.Infrastructure.SystemInfo;

/// <summary>Reads hardware and Windows facts through WMI and the registry (read-only).</summary>
public sealed class WindowsSystemInfoProvider(IMemoryStatusProvider memory) : ISystemInfoProvider
{
    public Task<SystemInformation> GetAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Read(cancellationToken), cancellationToken);

    private SystemInformation Read(CancellationToken cancellationToken)
    {
        var computerRow = WmiReader.QueryFirst(WmiReader.Cimv2, "SELECT Manufacturer, Model FROM Win32_ComputerSystem");
        cancellationToken.ThrowIfCancellationRequested();
        var osRow = WmiReader.QueryFirst(WmiReader.Cimv2, "SELECT Caption, InstallDate, LastBootUpTime FROM Win32_OperatingSystem");
        cancellationToken.ThrowIfCancellationRequested();
        var cpuRow = WmiReader.QueryFirst(WmiReader.Cimv2, "SELECT Name, NumberOfCores, MaxClockSpeed FROM Win32_Processor");
        cancellationToken.ThrowIfCancellationRequested();
        var modules = WmiReader.Query(WmiReader.Cimv2, "SELECT Capacity, ConfiguredClockSpeed, Speed FROM Win32_PhysicalMemory");
        cancellationToken.ThrowIfCancellationRequested();
        var gpus = WmiReader.Query(WmiReader.Cimv2, "SELECT Name, DriverVersion, AdapterRAM FROM Win32_VideoController");

        var osVersion = Environment.OSVersion.Version;
        var computer = new ComputerDetails(
            Environment.MachineName,
            CleanVendor(computerRow?.String("Manufacturer")),
            CleanVendor(computerRow?.String("Model")));

        var os = new OperatingSystemDetails(
            FixCaption(osRow?.String("Caption"), osVersion.Build),
            ReadDisplayVersion(),
            osVersion.ToString(),
            osVersion.Build,
            RuntimeInformation.OSArchitecture.ToString(),
            osRow?.Date("InstallDate"),
            osRow?.Date("LastBootUpTime"));

        var processor = cpuRow?.String("Name") is { } cpuName
            ? new ProcessorDetails(cpuName, cpuRow.Int("NumberOfCores"), Environment.ProcessorCount, cpuRow.Int("MaxClockSpeed"))
            : null;

        var installed = modules.Select(m => m.Long("Capacity")).Where(c => c > 0).Sum(c => c!.Value);
        var speed = modules.Select(m => m.Int("ConfiguredClockSpeed") ?? m.Int("Speed")).FirstOrDefault(s => s > 0);
        var memoryDetails = new MemoryDetails(
            installed > 0 ? installed : null,
            memory.GetStatus().TotalBytes,
            modules.Count > 0 ? modules.Count : null,
            speed);

        var graphics = gpus
            .Where(g => g.String("Name") is not null)
            .Select(g => new GraphicsDetails(g.String("Name")!, g.String("DriverVersion"), g.Long("AdapterRAM") is > 0 and var ram ? ram : null))
            .ToList();

        return new SystemInformation(computer, os, processor, memoryDetails, graphics);
    }

    /// <summary>Windows 11 can report "Windows 10" in older WMI captions; trust the build number.</summary>
    private static string? FixCaption(string? caption, int build) =>
        caption is not null && build >= 22000 ? caption.Replace("Windows 10", "Windows 11", StringComparison.Ordinal) : caption;

    /// <summary>Manufacturers often leave placeholder strings in firmware.</summary>
    private static string? CleanVendor(string? value) =>
        value is null || value.Contains("To be filled", StringComparison.OrdinalIgnoreCase)
                      || value.Contains("System Product Name", StringComparison.OrdinalIgnoreCase)
                      || value.Equals("Default string", StringComparison.OrdinalIgnoreCase)
            ? null
            : value;

    private static string? ReadDisplayVersion()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            return (key?.GetValue("DisplayVersion") ?? key?.GetValue("ReleaseId")) as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
