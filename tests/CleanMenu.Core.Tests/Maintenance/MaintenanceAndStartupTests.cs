using CleanMenu.Core.Maintenance;
using CleanMenu.Core.Startup;

namespace CleanMenu.Core.Tests.Maintenance;

public class MaintenanceAndStartupTests
{
    [Theory]
    [InlineData("\"C:\\Program Files\\App\\app.exe\" --min", @"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\Tools\app.exe -silent", @"C:\Tools\app.exe")]
    [InlineData(@"C:\Program Files\App Folder\app.exe /background", @"C:\Program Files\App Folder\app.exe")]
    [InlineData(@"rundll32.exe C:\x\y.dll,Start", "rundll32.exe")]
    [InlineData("\"C:\\a.exe\"", @"C:\a.exe")]
    public void ExtractExecutable(string command, string expected)
    {
        Assert.Equal(expected, StartupCommandParser.ExtractExecutable(command, s => s));
    }

    [Fact]
    public void ExtractExecutable_ExpandsEnvironment()
    {
        var result = StartupCommandParser.ExtractExecutable("%APPS%\\x.exe /a", s => s.Replace("%APPS%", @"D:\Apps", StringComparison.Ordinal));

        Assert.Equal(@"D:\Apps\x.exe", result);
    }

    [Fact]
    public void ExtractExecutable_Empty_IsNull()
    {
        Assert.Null(StartupCommandParser.ExtractExecutable("  "));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(new byte[] { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 0x06, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 0x03, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8 }, false)]
    [InlineData(new byte[] { 0x07, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, false)]
    public void StartupApproved_ReadsState(byte[]? value, bool expected)
    {
        Assert.Equal(expected, StartupCommandParser.IsApproved(value));
    }

    [Fact]
    public void StartupApproved_RoundTrips()
    {
        var now = new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

        Assert.False(StartupCommandParser.IsApproved(StartupCommandParser.BuildApprovedValue(false, now)));
        Assert.True(StartupCommandParser.IsApproved(StartupCommandParser.BuildApprovedValue(true, now)));
        Assert.Equal(12, StartupCommandParser.BuildApprovedValue(false, now).Length);
    }

    [Fact]
    public void OnlyPerUserEntries_CanBeToggled()
    {
        foreach (var source in Enum.GetValues<StartupSource>())
        {
            var entry = new StartupEntry("x", "x", null, null, source, true);
            Assert.Equal(source is StartupSource.RegistryUser or StartupSource.StartupFolderUser, entry.CanToggle);
        }
    }

    [Theory]
    [InlineData(MaintenanceTool.SystemFileCheck, null, "sfc.exe /scannow")]
    [InlineData(MaintenanceTool.DismScanHealth, null, "dism.exe /Online /Cleanup-Image /ScanHealth")]
    [InlineData(MaintenanceTool.DismRestoreHealth, null, "dism.exe /Online /Cleanup-Image /RestoreHealth")]
    [InlineData(MaintenanceTool.CheckDiskScan, "c", "chkdsk.exe C: /scan")]
    [InlineData(MaintenanceTool.OptimizeDrive, "D", "defrag.exe D: /O /U /V")]
    [InlineData(MaintenanceTool.AnalyzeDrive, "C", "defrag.exe C: /A /U /V")]
    public void Commands_AreExact(MaintenanceTool tool, string? drive, string expected)
    {
        Assert.Equal(expected, MaintenanceCommands.Build(tool, drive).DisplayCommand);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CD")]
    [InlineData("C:")]
    [InlineData("C & del")]
    [InlineData("1")]
    public void DriveCommands_RejectAnythingButOneLetter(string? drive)
    {
        Assert.Throws<ArgumentException>(() => MaintenanceCommands.Build(MaintenanceTool.OptimizeDrive, drive));
    }

    [Fact]
    public void EveryTool_HasPlainTexts_AndNeverTouchesSecurityFeatures()
    {
        foreach (var tool in Enum.GetValues<MaintenanceTool>())
        {
            var command = MaintenanceCommands.Build(tool, "C");
            Assert.False(string.IsNullOrWhiteSpace(command.Explanation));
            Assert.False(string.IsNullOrWhiteSpace(command.Duration));
            Assert.DoesNotContain("defender", command.DisplayCommand, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("firewall", command.DisplayCommand, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("wuauserv", command.DisplayCommand, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(null, null, "Padrão do Windows")]
    [InlineData(1, null, "Somente dados obrigatórios")]
    [InlineData(null, 3, "Dados opcionais ativados")]
    [InlineData(0, 3, "Mínimo (definido pela empresa)")]
    public void DiagnosticDataText(int? policy, int? user, string expected)
    {
        Assert.Equal(expected, WindowsSettingText.DiagnosticData(policy, user));
    }
}
