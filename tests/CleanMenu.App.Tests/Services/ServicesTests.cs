using System.Collections.ObjectModel;
using CleanMenu.App.Services;
using CleanMenu.App.ViewModels;
using CleanMenu.Core.Monitoring;
using Microsoft.Extensions.Logging.Abstractions;

namespace CleanMenu.App.Tests.Services;

public class ServicesTests
{
    private sealed class FakeCpu : ICpuTimesProvider
    {
        public Queue<CpuTimes> Times { get; } = new();

        public CpuTimes GetTimes() => Times.Dequeue();
    }

    private sealed class FakeGpu(double? value) : IGpuUsageSampler
    {
        public double? Sample() => value;
    }

    private sealed class FakeMemory : IMemoryStatusProvider
    {
        public MemoryStatus GetStatus() => new(Gb.Of(16), Gb.Of(4));
    }

    private sealed class CountingInfo : CleanMenu.Core.SystemInfo.ISystemInfoProvider
    {
        public int Calls { get; private set; }

        public Task<CleanMenu.Core.SystemInfo.SystemInformation> GetAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(FakeSystemInfo.Sample);
        }
    }

    [Fact]
    public void LiveMonitor_FirstCpuSampleIsNaN_ThenComputesUsage()
    {
        var cpu = new FakeCpu();
        cpu.Times.Enqueue(new CpuTimes(Idle: 100, Kernel: 200, User: 100));
        cpu.Times.Enqueue(new CpuTimes(Idle: 150, Kernel: 300, User: 200)); // total 200, idle 50 -> 75%
        var service = new LiveMonitorService(cpu, new FakeGpu(12), new FakeMemory(),
            new ProcessMonitor(new FakeProcessSource(), TimeProvider.System), TimeProvider.System, NullLogger<LiveMonitorService>.Instance);

        var first = service.TakeSample();
        var second = service.TakeSample();

        Assert.True(double.IsNaN(first.CpuPercent));
        Assert.Equal(75, second.CpuPercent, 3);
        Assert.Equal([75d], service.CpuHistory);
        Assert.Equal(2, service.MemoryHistory.Count);
        Assert.Equal(75, service.MemoryHistory[0], 3);
        Assert.Same(second, service.Latest);
    }

    [Fact]
    public async Task CachedSystemInfo_ReadsOnce()
    {
        var inner = new CountingInfo();
        var cached = new CachedSystemInfoProvider(inner);

        await cached.GetAsync();
        await cached.GetAsync();

        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public void CollectionSync_ReordersWithoutReplacingItems()
    {
        var a = new object();
        var b = new object();
        var c = new object();
        var d = new object();
        var collection = new ObservableCollection<object> { a, b, c };

        CollectionSync.Apply(collection, [c, a, d]);

        Assert.Equal([c, a, d], collection);
    }
}
