using CleanMenu.Core.Maintenance;

namespace CleanMenu.Core.Tests.Maintenance;

public class MaintenanceCommandTests
{
    private static readonly MaintenancePaths TestPaths = new(
        @"C:\Windows", @"C:\Users\teste\Documents\Clean Menu\Registros de eventos\2026-11-16_17-00", "20261116-1700");

    [Fact]
    public void EveryTool_HasPlainTexts_AndNeverDisablesSecurityFeatures()
    {
        foreach (var tool in Enum.GetValues<MaintenanceTool>())
        {
            var command = MaintenanceCommands.Build(tool, "C", TestPaths);
            Assert.False(string.IsNullOrWhiteSpace(command.Explanation));
            Assert.False(string.IsNullOrWhiteSpace(command.Duration));
            Assert.NotEmpty(command.Steps);
            foreach (var forbidden in new[] { "defender", "firewall", "sc config", "disabled", " del ", "rmdir", "cl Security" })
            {
                Assert.DoesNotContain(forbidden, command.DisplayCommand, StringComparison.OrdinalIgnoreCase);
            }

            // Every service that is stopped is started again by the same command.
            foreach (var step in command.Steps.Where(s => s.StartsWith("net stop ", StringComparison.Ordinal)))
            {
                Assert.Contains("net start " + step["net stop ".Length..], command.Steps);
            }
        }
    }

    [Fact]
    public void ResetWindowsUpdate_RenamesInsteadOfDeleting_AndRestartsServices()
    {
        var command = MaintenanceCommands.Build(MaintenanceTool.ResetWindowsUpdate, paths: TestPaths);

        Assert.True(command.RequiresAdmin);
        Assert.Equal("net stop wuauserv", command.Steps[0]);
        Assert.Contains("ren \"C:\\Windows\\SoftwareDistribution\" SoftwareDistribution.bak-20261116-1700", command.Steps);
        Assert.Contains("ren \"C:\\Windows\\System32\\catroot2\" catroot2.bak-20261116-1700", command.Steps);
        Assert.Equal("net start wuauserv", command.Steps[^1]);
    }

    [Fact]
    public void ClearEventLogs_BacksUpFirst_AndOnlyClearsWhenBackupSucceeded()
    {
        var command = MaintenanceCommands.Build(MaintenanceTool.ClearEventLogs, paths: TestPaths);
        var clears = command.Steps.Where(s => s.Contains("wevtutil cl", StringComparison.Ordinal)).ToList();

        Assert.Equal(2, clears.Count);
        Assert.All(clears, s => Assert.Matches("^wevtutil epl (\\w+) \".+\\\\\\1\\.evtx\" /ow:true && wevtutil cl \\1$", s));
        Assert.Equal(["Application", "System"], MaintenanceCommands.ClearableEventLogs);
    }

    [Fact]
    public void ProgramUpdates_DoNotNeedAdmin()
    {
        var list = MaintenanceCommands.Build(MaintenanceTool.ListProgramUpdates);

        Assert.False(list.RequiresAdmin);
        Assert.Equal("winget upgrade --accept-source-agreements", list.DisplayCommand);
        Assert.Contains("--all", MaintenanceCommands.Build(MaintenanceTool.UpdateAllPrograms).DisplayCommand, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("relativo")]
    [InlineData(@"\\servidor\share")]
    [InlineData("C:\\Users\\a\" & del x")]
    [InlineData(@"C:\Users\%TEMP%")]
    [InlineData(@"C:\Users\a&b")]
    [InlineData("")]
    public void FolderTools_RejectUnsafePaths(string folder)
    {
        Assert.Throws<ArgumentException>(() =>
            MaintenanceCommands.Build(MaintenanceTool.BackupEventLogs, paths: TestPaths with { OutputFolder = folder }));
        Assert.Throws<ArgumentException>(() => MaintenanceCommands.Build(MaintenanceTool.BackupEventLogs));
    }

    [Fact]
    public void PathProvider_UsesDatedFolderUnderDocuments()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 16, 17, 5, 0, TimeSpan.Zero));
        var provider = new MaintenancePathProvider(@"C:\Windows", @"C:\Users\teste\Documents", clock);

        var paths = provider.For(MaintenanceTool.BackupEventLogs);

        Assert.Equal(@"C:\Users\teste\Documents\Clean Menu\Registros de eventos\2026-11-16_17-05", paths.OutputFolder);
        Assert.Equal("20261116-1705", paths.Stamp);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
