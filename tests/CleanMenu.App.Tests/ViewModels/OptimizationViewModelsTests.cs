using CleanMenu.App.Services;
using CleanMenu.App.ViewModels;
using CleanMenu.Core.Disks;
using CleanMenu.Core.Maintenance;
using CleanMenu.Core.Platform;
using CleanMenu.Core.Processes;
using CleanMenu.Core.Startup;

namespace CleanMenu.App.Tests.ViewModels;

public class OptimizationViewModelsTests
{
    private static readonly StartupEntry UserApp = new("Spotify", "\"C:\\Apps\\Spotify.exe\" /min", @"C:\Apps\Spotify.exe", "Spotify AB", StartupSource.RegistryUser, true);
    private static readonly StartupEntry MachineApp = new("Driver", "C:\\Drv\\drv.exe", @"C:\Drv\drv.exe", "Fabricante", StartupSource.RegistryMachine, true);

    [Fact]
    public async Task Startup_DisableAsksConfirmation_ThenTogglesAndRecords()
    {
        var manager = new FakeStartupManager(UserApp, MachineApp);
        var history = new MemoryHistory();
        var vm = new StartupViewModel(manager, new FakeProcessActions(), new FakeDialogs(true), history, new ImmediateUiDispatcher());
        await vm.RefreshAsync();

        var row = vm.Rows.Single(r => r.Name == "Spotify");
        await vm.ToggleCommand.ExecuteAsync(row);

        Assert.Equal([("Spotify", false)], manager.Changes);
        Assert.False(row.IsEnabled);
        Assert.Equal("Religar", row.ToggleText);
        Assert.Equal("Inicialização: desligado", Assert.Single(history.Records).Type);
        Assert.Contains("1 programa(s)", vm.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_CancelledConfirmation_ChangesNothing()
    {
        var manager = new FakeStartupManager(UserApp);
        var vm = new StartupViewModel(manager, new FakeProcessActions(), new FakeDialogs(false), new MemoryHistory(), new ImmediateUiDispatcher());
        await vm.RefreshAsync();

        await vm.ToggleCommand.ExecuteAsync(vm.Rows[0]);

        Assert.Empty(manager.Changes);
        Assert.True(vm.Rows[0].IsEnabled);
    }

    [Fact]
    public async Task Startup_MachineEntries_AreLocked()
    {
        var manager = new FakeStartupManager(MachineApp);
        var vm = new StartupViewModel(manager, new FakeProcessActions(), new FakeDialogs(true), new MemoryHistory(), new ImmediateUiDispatcher());
        await vm.RefreshAsync();

        var row = vm.Rows[0];
        Assert.False(row.CanToggle);
        Assert.Contains("administrador", row.LockText, StringComparison.Ordinal);
        await vm.ToggleCommand.ExecuteAsync(row);
        Assert.Empty(manager.Changes);
    }

    [Fact]
    public async Task Optimization_RunTool_ConfirmsShowingExactCommand()
    {
        var dialogs = new FakeDialogs(true);
        var runner = new FakeRunner();
        var history = new MemoryHistory();
        var vm = new OptimizationViewModel(new FakePowerPlans(), new FakeSettingsReader(), runner, new FakeActions(), dialogs, history,
            new ImmediateUiDispatcher(), new OsProfile(WindowsVersion.Windows11, null));

        await vm.RunToolCommand.ExecuteAsync(vm.Tools.Single(t => t.Command.Tool == MaintenanceTool.SystemFileCheck));

        Assert.Contains(dialogs.Requests[0].Details, d => d.Contains("sfc.exe /scannow", StringComparison.Ordinal));
        Assert.Equal("sfc.exe", Assert.Single(runner.Started).FileName);
        Assert.Equal("Manutenção do Windows", Assert.Single(history.Records).Type);
    }

    [Fact]
    public async Task Optimization_RunTool_Cancelled_DoesNotStart()
    {
        var runner = new FakeRunner();
        var vm = new OptimizationViewModel(new FakePowerPlans(), new FakeSettingsReader(), runner, new FakeActions(), new FakeDialogs(false),
            new MemoryHistory(), new ImmediateUiDispatcher(), new OsProfile(WindowsVersion.Windows11, null));

        await vm.RunToolCommand.ExecuteAsync(vm.Tools[0]);

        Assert.Empty(runner.Started);
    }

    [Fact]
    public async Task Optimization_ShowsOnlySettingsOfChosenWindows_AndSwitchesPlan()
    {
        var power = new FakePowerPlans();
        var vm = new OptimizationViewModel(power, new FakeSettingsReader(), new FakeRunner(), new FakeActions(), new FakeDialogs(true),
            new MemoryHistory(), new ImmediateUiDispatcher(), new OsProfile(WindowsVersion.Windows11, WindowsVersion.Windows10));

        Assert.Contains(vm.Settings, s => s.Title.EndsWith("(10)", StringComparison.Ordinal));
        Assert.DoesNotContain(vm.Settings, s => s.Title.EndsWith("(11)", StringComparison.Ordinal));

        await vm.ActivatePlanCommand.ExecuteAsync(vm.PowerPlans.Single(p => p.Id == FakePowerPlans.High));
        Assert.Equal(FakePowerPlans.High, power.Active);
    }

    [Fact]
    public async Task Processes_ProtectedCannotBeEnded_UserAppCanAfterConfirmation()
    {
        var source = new FakeProcessSource
        {
            Next = [new ProcessSnapshot(500, "lsass", 100, null, false, null), new ProcessSnapshot(900, "jogo", 100, null, true, "Jogo")],
        };
        var monitor = new ProcessMonitor(source, TimeProvider.System);
        monitor.Refresh();
        var actions = new FakeProcessActions();
        var vm = new ProcessesViewModel(monitor, new ImmediateUiDispatcher(), actions, new FakeDialogs(true), new MemoryHistory());

        vm.SelectedRow = vm.Rows.Single(r => r.Name == "lsass");
        Assert.False(vm.EndSelectedCommand.CanExecute(null));
        Assert.Contains("não pode ser fechado", vm.SelectionHint, StringComparison.Ordinal);

        vm.SelectedRow = vm.Rows.Single(r => r.Name == "jogo");
        await vm.EndSelectedCommand.ExecuteAsync(null);
        Assert.Equal([(900, "jogo")], actions.Ended);
        Assert.Equal("Programa fechado.", vm.Message);
    }

    [Fact]
    public async Task Disks_Optimize_UsesWindowsDefragWithOptimizeFlag_ForThatDrive()
    {
        var runner = new FakeRunner();
        var dialogs = new FakeDialogs(true);
        var vm = new DisksViewModel(
            new FakeVolumes(new VolumeInfo("C:", null, "NTFS", false, true, Gb.Of(500), Gb.Of(200))),
            new FakeDisks(new PhysicalDiskInfo("0", "SSD", DiskMedia.Ssd, "NVMe", Gb.Of(512), ReportedDiskHealth.NoProblemsReported)),
            new ImmediateUiDispatcher(), runner, dialogs, new MemoryHistory());
        await vm.RefreshAsync();

        var volume = Assert.Single(vm.Volumes);
        Assert.Equal(DiskMedia.Ssd, volume.Media);
        await vm.OptimizeCommand.ExecuteAsync(volume);

        var command = Assert.Single(runner.Started);
        Assert.Equal("defrag.exe C: /O /U /V", command.DisplayCommand);
        Assert.Contains(dialogs.Requests[0].Details, d => d.Contains("TRIM", StringComparison.Ordinal) && d.Contains("nunca é desfragmentado", StringComparison.Ordinal));
    }
}
