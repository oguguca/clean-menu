using System.Diagnostics;
using CleanMenu.Core.Maintenance;
using CleanMenu.Core.Processes;
using CleanMenu.Core.Startup;
using CleanMenu.Infrastructure.Maintenance;
using CleanMenu.Infrastructure.Processes;
using CleanMenu.Infrastructure.Startup;

namespace CleanMenu.Infrastructure.Tests.Maintenance;

/// <summary>
/// Read-only checks against the real machine. Nothing is changed: no plan is activated, no startup
/// entry is toggled, no maintenance command is started. The only process ended is one the test
/// itself starts.
/// </summary>
public class MaintenanceProviderTests
{
    [Fact]
    public async Task PowerPlans_ListsPlans_WithExactlyOneActive()
    {
        var plans = await new WindowsPowerPlanService().GetPlansAsync();

        Assert.NotEmpty(plans);
        Assert.Single(plans, p => p.IsActive);
        Assert.All(plans, p => Assert.False(string.IsNullOrWhiteSpace(p.Name)));
    }

    [Fact]
    public async Task Startup_ReadsWithoutChanging()
    {
        var entries = await new WindowsStartupManager(TimeProvider.System).GetEntriesAsync();

        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Name)));
    }

    [Fact]
    public void Startup_RefusesMachineEntries()
    {
        var manager = new WindowsStartupManager(TimeProvider.System);
        var machine = new StartupEntry("x", "x", null, null, StartupSource.RegistryMachine, true);

        Assert.Throws<InvalidOperationException>(() => manager.SetEnabled(machine, false));
    }

    [Fact]
    public void SettingsReader_ReturnsOsSpecificSettings()
    {
        var reader = new WindowsSettingsReader();

        Assert.Contains(reader.Read(Core.Platform.WindowsVersion.Windows10), s => s.SettingsUri == "ms-settings:privacy-backgroundapps");
        Assert.Contains(reader.Read(Core.Platform.WindowsVersion.Windows11), s => s.SettingsUri == "ms-settings:appsfeatures");
    }

    [Fact]
    public void Runner_ShowsCommandAndKeepsWindowOpen()
    {
        var args = VisibleElevatedRunner.BuildArguments(MaintenanceCommands.Build(MaintenanceTool.SystemFileCheck));

        Assert.StartsWith("/k ", args, StringComparison.Ordinal);
        Assert.Contains("echo Clean Menu - executando: sfc.exe /scannow & sfc.exe /scannow", args, StringComparison.Ordinal);
        Assert.Contains("echo Clean Menu - terminado", args, StringComparison.Ordinal);
    }

    [Fact]
    public void Runner_EchoNeverRunsOperators()
    {
        Assert.Equal("a ^&^& b ^| c ^> d", VisibleElevatedRunner.EscapeForEcho("a && b | c > d"));
        Assert.Equal("x \"C:\\a & b\" ^& y", VisibleElevatedRunner.EscapeForEcho("x \"C:\\a & b\" & y"));
    }

    [Fact]
    public void Runner_FindsWindowsPrograms()
    {
        var runner = new VisibleElevatedRunner();
        var sfc = MaintenanceCommands.Build(MaintenanceTool.SystemFileCheck);

        Assert.True(runner.IsAvailable(sfc));
        Assert.False(runner.IsAvailable(sfc with { Program = "nao-existe-123.exe" }));
    }

    [Fact]
    public void ProcessActions_RefuseProtectedProcesses()
    {
        var actions = new WindowsProcessActions();

        Assert.Equal(ProcessEndResult.Protected, actions.EndProcess(4, "System"));
        Assert.Equal(ProcessEndResult.Protected, actions.EndProcess(12345, "lsass"));
    }

    [Fact]
    public void ProcessActions_DetectPidReuse_AndEndOwnChild()
    {
        using var child = Process.Start(new ProcessStartInfo("ping.exe", "-n 60 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false })!;
        var actions = new WindowsProcessActions();

        Assert.Equal(ProcessEndResult.ChangedIdentity, actions.EndProcess(child.Id, "outro-nome"));
        Assert.False(child.HasExited);

        Assert.Equal(ProcessEndResult.Ended, actions.EndProcess(child.Id, "PING"));
        Assert.True(child.HasExited);
    }
}
