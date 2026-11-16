using CleanMenu.App.Services;
using CleanMenu.App.ViewModels;
using CleanMenu.Core.Monitoring;
using CleanMenu.Core.Processes;
using CleanMenu.UI;

namespace CleanMenu.App.Tests.ViewModels;

public class MemoryAndProcessesViewModelTests
{
    private static ProcessSnapshot Proc(int pid, string name, double gb, bool window = false, double cpuSeconds = 0) =>
        new(pid, name, Gb.Of(gb), TimeSpan.FromSeconds(cpuSeconds), window, window ? $"{name} janela" : null);

    private static (ProcessMonitor Monitor, FakeProcessSource Source, ManualTime Time) CreateMonitor()
    {
        var source = new FakeProcessSource();
        var time = new ManualTime(DateTimeOffset.Parse("2026-11-16T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        return (new ProcessMonitor(source, time), source, time);
    }

    [Fact]
    public void Memory_ShowsRealValues_AndReservedFromInstalled()
    {
        var live = new FakeLiveMonitor();
        var (monitor, _, _) = CreateMonitor();
        var vm = new MemoryViewModel(live, monitor, new FakeSystemInfo(FakeSystemInfo.Sample), new FakeActions(), new ImmediateUiDispatcher());

        live.Raise(new LiveSample(DateTimeOffset.Now, 1, null, new MemoryStatus(Gb.Of(15), Gb.Of(3))));

        Assert.Equal("80%", vm.UsedPercentText);
        Assert.Equal(StatusLevel.Warning, vm.Status);
        Assert.Equal("12 GB", vm.UsedText);
        Assert.StartsWith("1 GB", vm.ReservedText, StringComparison.Ordinal);
    }

    [Fact]
    public void Memory_TopPrograms_GroupedAndOrderedByMemory_WithAdvice()
    {
        var (monitor, source, _) = CreateMonitor();
        source.Next =
        [
            Proc(10, "chrome", 1.5, window: true), Proc(13, "chrome", 1), Proc(14, "chrome", 1),
            Proc(11, "svchost", 0.2), Proc(12, "jogo", 3, window: true),
        ];
        monitor.Refresh();

        var vm = new MemoryViewModel(new FakeLiveMonitor(), monitor, new FakeSystemInfo(null), new FakeActions(), new ImmediateUiDispatcher());

        Assert.Equal(["chrome", "jogo", "svchost"], vm.TopProcesses.Select(p => p.Name));
        Assert.Equal("chrome (3 partes)", vm.TopProcesses[0].DisplayName);
        Assert.Equal("3,5 GB", vm.TopProcesses[0].MemoryText);
        Assert.Equal(ProcessKind.UserApp, vm.TopProcesses[0].Kind);
        Assert.Equal(ProcessKind.CriticalSystem, vm.TopProcesses[2].Kind);
        Assert.Contains("Não feche", vm.TopProcesses[2].Advice, StringComparison.Ordinal);
    }

    [Fact]
    public void Memory_OpenTaskManager_ReportsFailure()
    {
        var actions = new FakeActions { OpenResult = false };
        var (monitor, _, _) = CreateMonitor();
        var vm = new MemoryViewModel(new FakeLiveMonitor(), monitor, new FakeSystemInfo(null), actions, new ImmediateUiDispatcher());

        vm.OpenTaskManagerCommand.Execute(null);

        Assert.Equal(["taskmgr.exe"], actions.Opened);
        Assert.NotNull(vm.ActionMessage);
    }

    [Fact]
    public void Processes_SortAndFilter()
    {
        var (monitor, source, time) = CreateMonitor();
        source.Next = [Proc(1, "alpha", 0.5, cpuSeconds: 0), Proc(2, "beta", 2, cpuSeconds: 0), Proc(3, "gamma", 1, cpuSeconds: 0)];
        monitor.Refresh();
        time.Now = time.Now.AddSeconds(1);
        source.Next = [Proc(1, "alpha", 0.5, cpuSeconds: 0.5), Proc(2, "beta", 2, cpuSeconds: 0), Proc(3, "gamma", 1, cpuSeconds: 0.1)];
        monitor.Refresh();

        var vm = new ProcessesViewModel(monitor, new ImmediateUiDispatcher(), new FakeProcessActions(), new FakeDialogs(true), new MemoryHistory());
        Assert.Equal(["beta", "gamma", "alpha"], vm.Rows.Select(r => r.Name));

        vm.SortMode = ProcessSortMode.Cpu;
        Assert.Equal("alpha", vm.Rows[0].Name);

        vm.SortMode = ProcessSortMode.Name;
        Assert.Equal(["alpha", "beta", "gamma"], vm.Rows.Select(r => r.Name));

        vm.FilterText = "AMM";
        Assert.Equal(["gamma"], vm.Rows.Select(r => r.Name));
    }

    [Fact]
    public void Processes_RefreshKeepsSameRowInstances()
    {
        var (monitor, source, _) = CreateMonitor();
        source.Next = [Proc(1, "alpha", 0.5), Proc(2, "beta", 2)];
        monitor.Refresh();
        var vm = new ProcessesViewModel(monitor, new ImmediateUiDispatcher(), new FakeProcessActions(), new FakeDialogs(true), new MemoryHistory());
        var beta = vm.Rows[0];

        source.Next = [Proc(1, "alpha", 0.6), Proc(2, "beta", 2.5)];
        monitor.Refresh();

        Assert.Same(beta, vm.Rows[0]);
        Assert.Equal(Gb.Of(2.5), beta.MemoryBytes);
    }
}
