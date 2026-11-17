using CleanMenu.Infrastructure.SystemInfo;

namespace CleanMenu.Infrastructure.Tests.SystemInfo;

/// <summary>Read-only WMI queries on the real machine.</summary>
public class DeviceStatusProviderTests
{
    private readonly WindowsDeviceStatusProvider _provider = new();

    [Fact]
    public void Services_IncludeCoreWindowsServices()
    {
        var services = _provider.GetImportantServices();

        Assert.Contains(services, s => s.Name == "EventLog" && s.IsRunning);
    }

    [Fact]
    public void Antivirus_UsbAndPageFile_AreReadWithoutErrors()
    {
        Assert.All(_provider.GetAntivirus(), a => Assert.False(string.IsNullOrWhiteSpace(a.Name)));
        Assert.All(_provider.GetUsbDevices(), d => Assert.False(string.IsNullOrWhiteSpace(d.Name)));
        Assert.All(_provider.GetPageFiles(), p => Assert.True(p.AllocatedMb >= 0));
    }
}
