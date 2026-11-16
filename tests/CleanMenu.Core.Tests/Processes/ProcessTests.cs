using CleanMenu.Core.Processes;

namespace CleanMenu.Core.Tests.Processes;

public class ProcessTests
{
    private const long Mb = 1024L * 1024;
    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("lsass", false, ProcessKind.CriticalSystem)]
    [InlineData("csrss", false, ProcessKind.CriticalSystem)]
    [InlineData("svchost", false, ProcessKind.CriticalSystem)]
    [InlineData("explorer", true, ProcessKind.CriticalSystem)]
    [InlineData("MsMpEng", false, ProcessKind.CriticalSystem)]
    [InlineData("OneDrive", false, ProcessKind.WindowsComponent)]
    [InlineData("chrome", true, ProcessKind.UserApp)]
    [InlineData("updater", false, ProcessKind.Background)]
    public void Classify(string name, bool hasWindow, ProcessKind expected)
    {
        Assert.Equal(expected, ProcessClassifier.Classify(name, hasWindow));
    }

    [Theory]
    [InlineData("System", 4, true)]
    [InlineData("Idle", 0, true)]
    [InlineData("winlogon", 900, true)]
    [InlineData("LSASS", 700, true)]          // case-insensitive
    [InlineData("MsMpEng", 3000, true)]       // antivirus is never closed
    [InlineData("notepad", 5000, false)]
    public void IsProtected(string name, int pid, bool expected)
    {
        Assert.Equal(expected, ProcessClassifier.IsProtected(name, pid));
    }

    [Fact]
    public void Advice_IsPlainLanguage_ForEveryKind()
    {
        foreach (var kind in Enum.GetValues<ProcessKind>())
        {
            Assert.False(string.IsNullOrWhiteSpace(ProcessClassifier.Advice(kind)));
        }

        Assert.Contains("Não feche", ProcessClassifier.Advice(ProcessKind.CriticalSystem), StringComparison.Ordinal);
    }

    [Fact]
    public void Tracker_ComputesCpuPercentAcrossCores()
    {
        var tracker = new ProcessUsageTracker(logicalProcessors: 4);
        tracker.Update([new ProcessSnapshot(1, "app", 100, TimeSpan.Zero, true, null)], T0);

        // 2 s of CPU time in 1 s on 4 cores = 50% of total capacity.
        var usage = tracker.Update([new ProcessSnapshot(1, "app", 100, TimeSpan.FromSeconds(2), true, null)], T0.AddSeconds(1));

        Assert.Equal(50, usage[0].CpuPercent, 3);
        Assert.Equal(ProcessKind.UserApp, usage[0].Kind);
    }

    [Fact]
    public void Tracker_FirstSampleAndDeniedAccess_AreNaN()
    {
        var tracker = new ProcessUsageTracker(2);
        var first = tracker.Update([new ProcessSnapshot(1, "a", 1, TimeSpan.Zero, false, null)], T0);
        var denied = tracker.Update([new ProcessSnapshot(1, "a", 1, null, false, null)], T0.AddSeconds(1));

        Assert.True(double.IsNaN(first[0].CpuPercent));
        Assert.True(double.IsNaN(denied[0].CpuPercent));
    }

    [Fact]
    public void Tracker_NewPidWithSameTime_IsNaN()
    {
        var tracker = new ProcessUsageTracker(2);
        tracker.Update([new ProcessSnapshot(1, "a", 1, TimeSpan.Zero, false, null)], T0);
        var usage = tracker.Update([new ProcessSnapshot(2, "b", 1, TimeSpan.FromSeconds(5), false, null)], T0.AddSeconds(1));

        Assert.True(double.IsNaN(usage[0].CpuPercent));
    }

    private static ProcessUsage Usage(int pid, string name, long bytes) =>
        new(pid, name, bytes, 0, ProcessKind.UserApp, null);

    [Fact]
    public void Group_SumsProcessesOfSameProgram()
    {
        var programs = ProgramUsage.Group(
        [
            new ProcessUsage(1, "brave", 300 * Mb, 2, ProcessKind.UserApp, "Site"),
            new ProcessUsage(2, "brave", 200 * Mb, 1, ProcessKind.Background, null),
            new ProcessUsage(3, "Brave", 100 * Mb, double.NaN, ProcessKind.Background, null),
            new ProcessUsage(4, "svchost", 50 * Mb, double.NaN, ProcessKind.CriticalSystem, null),
            new ProcessUsage(0, "Idle", 8 * Mb, 90, ProcessKind.CriticalSystem, null),
        ]);

        Assert.Equal(2, programs.Count);
        var brave = programs[0];
        Assert.Equal(3, brave.ProcessCount);
        Assert.Equal(600 * Mb, brave.MemoryBytes);
        Assert.Equal(3, brave.CpuPercent, 3);
        Assert.Equal(ProcessKind.UserApp, brave.Kind);
        Assert.True(double.IsNaN(programs[1].CpuPercent));
        Assert.True(programs[1].IsProtected);
    }

    [Fact]
    public void Growth_SteadyIncreaseOver10Minutes_IsFlagged()
    {
        var detector = new MemoryGrowthDetector();
        for (var minute = 0; minute <= 12; minute++)
        {
            detector.Record([Usage(1, "vazando", (400 + minute * 50) * Mb)], T0.AddMinutes(minute));
        }

        var suspect = Assert.Single(detector.Suspects());
        Assert.Equal("vazando", suspect.Name);
        Assert.Equal(600 * Mb, suspect.GrowthBytes);
    }

    [Fact]
    public void Growth_HighButStableMemory_IsNotFlagged()
    {
        var detector = new MemoryGrowthDetector();
        for (var minute = 0; minute <= 20; minute++)
        {
            detector.Record([Usage(1, "jogo", (6000 + (minute % 2) * 10) * Mb)], T0.AddMinutes(minute));
        }

        Assert.Empty(detector.Suspects());
    }

    [Fact]
    public void Growth_ShortObservation_IsNotFlagged()
    {
        var detector = new MemoryGrowthDetector();
        for (var minute = 0; minute <= 5; minute++)
        {
            detector.Record([Usage(1, "app", (100 + minute * 200) * Mb)], T0.AddMinutes(minute));
        }

        Assert.Empty(detector.Suspects());
    }

    [Fact]
    public void Growth_UpAndDown_IsNotFlagged()
    {
        var detector = new MemoryGrowthDetector();
        for (var minute = 0; minute <= 20; minute++)
        {
            var bytes = (minute % 2 == 0 ? 300 + minute * 60 : 200 + minute * 30) * Mb;
            detector.Record([Usage(1, "navegador", bytes)], T0.AddMinutes(minute));
        }

        Assert.Empty(detector.Suspects());
    }

    [Fact]
    public void Growth_ForgetsEndedProcesses()
    {
        var detector = new MemoryGrowthDetector();
        for (var minute = 0; minute <= 12; minute++)
        {
            detector.Record([Usage(1, "vazando", (400 + minute * 50) * Mb)], T0.AddMinutes(minute));
        }

        detector.Record([], T0.AddMinutes(13));

        Assert.Empty(detector.Suspects());
    }
}
