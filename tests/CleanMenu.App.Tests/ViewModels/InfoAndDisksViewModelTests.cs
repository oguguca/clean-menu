using CleanMenu.App.Services;
using CleanMenu.App.ViewModels;
using CleanMenu.Core.Disks;
using CleanMenu.UI;

namespace CleanMenu.App.Tests.ViewModels;

public class InfoAndDisksViewModelTests
{
    private static readonly PhysicalDiskInfo Ssd = new("0", "SSD Teste", DiskMedia.Ssd, "NVMe", Gb.Of(512), ReportedDiskHealth.NoProblemsReported);
    private static readonly VolumeInfo SystemDrive = new("C:", null, "NTFS", false, true, Gb.Of(476), Gb.Of(20));

    [Fact]
    public async Task SystemInfo_BuildsGroups_AndCopiesReport()
    {
        var actions = new FakeActions();
        var vm = new SystemInfoViewModel(new FakeSystemInfo(FakeSystemInfo.Sample), new FakeVolumes(SystemDrive), new FakeDisks(Ssd), actions, new ImmediateUiDispatcher());
        await vm.LoadAsync();

        Assert.False(vm.IsLoading);
        Assert.Contains(vm.Groups, g => g.Title == "Processador" && g.Facts.Any(f => f.Value == "Processador Teste"));
        Assert.Contains(vm.Groups, g => g.Title == "Armazenamento" && g.Facts.Any(f => f.Label == "SSD Teste"));

        vm.CopyReportCommand.Execute(null);
        Assert.Contains("Processador: Processador Teste", actions.Clipboard, StringComparison.Ordinal);
        Assert.NotNull(vm.Message);
    }

    [Fact]
    public async Task SystemInfo_Failure_ShowsMessage()
    {
        var vm = new SystemInfoViewModel(new FakeSystemInfo(null), new FakeVolumes(), new FakeDisks(), new FakeActions(), new ImmediateUiDispatcher());
        await vm.LoadAsync();

        Assert.False(vm.IsLoading);
        Assert.Empty(vm.Groups);
        Assert.NotNull(vm.Message);
    }

    [Fact]
    public void Disks_ListsVolumesAndPhysicalDisks()
    {
        var vm = new DisksViewModel(new FakeVolumes(SystemDrive), new FakeDisks(Ssd), new ImmediateUiDispatcher(), new FakeRunner(), new FakeDialogs(true), new MemoryHistory());

        var volume = Assert.Single(vm.Volumes);
        Assert.Equal(StatusLevel.Critical, volume.Status); // 20 GB of 476 GB = 4.2% free
        var disk = Assert.Single(vm.PhysicalDisks);
        Assert.Equal("SSD", disk.TypeText);
        Assert.Equal("O Windows não relatou problemas", disk.HealthText);
    }

    [Fact]
    public void Disks_NoProblemsReported_IsNotShownAsHealthyGreen()
    {
        var row = new PhysicalDiskRowViewModel(Ssd);

        // Absence of reported problems is not proof of good health.
        Assert.Equal(StatusLevel.Neutral, row.Status);
    }

    [Fact]
    public void Disks_UnhealthyDisk_IsCritical_AndAsksForBackup()
    {
        var row = new PhysicalDiskRowViewModel(Ssd with { Health = ReportedDiskHealth.Unhealthy });

        Assert.Equal(StatusLevel.Critical, row.Status);
        Assert.Contains("backup", row.HealthText, StringComparison.Ordinal);
    }
}
