using CleanMenu.App.Services;
using CleanMenu.App.ViewModels;
using CleanMenu.Core.Disks;
using CleanMenu.Core.Monitoring;
using CleanMenu.Core.Platform;
using CleanMenu.UI;

namespace CleanMenu.App.Tests.ViewModels;

public class DashboardViewModelTests
{
    private static DashboardViewModel Create(
        FakeLiveMonitor? monitor = null,
        FakeSystemInfo? info = null,
        FakeVolumes? volumes = null,
        string machine = "PC-TESTE",
        int build = 26100) =>
        new(new OsProfile(WindowsVersion.Windows11, null),
            monitor ?? new FakeLiveMonitor(),
            info ?? new FakeSystemInfo(FakeSystemInfo.Sample),
            volumes ?? new FakeVolumes(),
            new ImmediateUiDispatcher(),
            machine,
            build);

    [Fact]
    public void AnalyzeCommand_IsDisabled_UntilAnalysisExists()
    {
        var vm = Create();

        Assert.False(vm.IsAnalysisAvailable);
        Assert.False(vm.AnalyzeCommand.CanExecute(null));
    }

    [Fact]
    public void AnalysisCards_ShowNoInventedValues()
    {
        var vm = Create();

        foreach (var card in new[] { vm.HealthCard, vm.FreeableCard, vm.TempFilesCard, vm.StartupCard })
        {
            Assert.Equal("—", card.Value);
            Assert.Equal(StatusLevel.Neutral, card.Status);
        }

        Assert.Equal("Nunca", vm.LastCleanupCard.Value);
        Assert.All(vm.SummaryCards, card => Assert.False(string.IsNullOrWhiteSpace(card.HelpText)));
    }

    [Fact]
    public void SummaryCards_CoverRequiredTopics()
    {
        var titles = Create().SummaryCards.Select(c => c.Title).ToList();

        Assert.Equal(["Saúde geral", "Espaço para liberar", "Memória livre", "Arquivos temporários", "Programas ao ligar", "Última limpeza"], titles);
    }

    [Fact]
    public void LiveSample_UpdatesCpuMemoryAndGpu()
    {
        var monitor = new FakeLiveMonitor { CpuHistory = [10, 20], MemoryHistory = [40, 50], GpuHistory = [5, 7] };
        var vm = Create(monitor);

        monitor.Raise(new LiveSample(DateTimeOffset.Now, 23.4, 7, new MemoryStatus(Gb.Of(16), Gb.Of(8))));

        Assert.Equal("23%", vm.CpuCard.Value);
        Assert.Equal("50%", vm.MemoryCard.Value);
        Assert.Equal("8 GB de 16 GB", vm.MemoryCard.Caption);
        Assert.Equal(StatusLevel.Good, vm.MemoryCard.Status);
        Assert.Equal("8 GB", vm.FreeMemoryCard.Value);
        Assert.Equal("7%", vm.GpuCard.Value);
        Assert.Equal(2, vm.CpuCard.History?.Count);
    }

    [Fact]
    public void FullMemory_IsCritical()
    {
        var monitor = new FakeLiveMonitor();
        var vm = Create(monitor);

        monitor.Raise(new LiveSample(DateTimeOffset.Now, 5, null, new MemoryStatus(Gb.Of(8), Gb.Of(0.4))));

        Assert.Equal(StatusLevel.Critical, vm.MemoryCard.Status);
        Assert.Equal(StatusLevel.Critical, vm.FreeMemoryCard.Status);
    }

    [Fact]
    public void MissingGpuCounters_SaysNotAvailable()
    {
        var monitor = new FakeLiveMonitor();
        var vm = Create(monitor);

        monitor.Raise(new LiveSample(DateTimeOffset.Now, 5, null, new MemoryStatus(Gb.Of(8), Gb.Of(4))));

        Assert.Equal("—", vm.GpuCard.Value);
        Assert.StartsWith("Não disponível", vm.GpuCard.Caption, StringComparison.Ordinal);
        Assert.Null(vm.GpuCard.History);
    }

    [Fact]
    public void Temperature_IsHonestlyUnavailable()
    {
        var card = Create().TemperatureCard;

        Assert.Equal("—", card.Value);
        Assert.StartsWith("Não disponível", card.Caption, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemInfo_FillsComputerFacts()
    {
        var vm = Create(machine: "MEU-PC");

        Assert.Equal("MEU-PC", vm.MachineName);
        Assert.Equal("Windows 11 Home", vm.WindowsName);
        Assert.Equal("24H2 (build 26100)", vm.WindowsVersion);
        Assert.Equal("Processador Teste", vm.ProcessorName);
        Assert.Equal("16 GB", vm.InstalledMemory);
        Assert.Equal("Vídeo Teste", vm.GraphicsName);
    }

    [Fact]
    public void SystemInfoFailure_ShowsNotReported()
    {
        var vm = Create(info: new FakeSystemInfo(null));

        Assert.Equal("Não informado", vm.ProcessorName);
        Assert.Equal("Windows 11", vm.WindowsName);
    }

    [Fact]
    public void Volumes_ExcludeRemovableDrives()
    {
        var vm = Create(volumes: new FakeVolumes(
            new VolumeInfo("C:", null, "NTFS", false, true, Gb.Of(500), Gb.Of(100)),
            new VolumeInfo("E:", "PENDRIVE", "FAT32", true, false, Gb.Of(16), Gb.Of(10))));

        var volume = Assert.Single(vm.Volumes);
        Assert.Equal("Disco do Windows (C:)", volume.Title);
        Assert.Equal("100 GB livres", volume.FreeText);
    }
}
