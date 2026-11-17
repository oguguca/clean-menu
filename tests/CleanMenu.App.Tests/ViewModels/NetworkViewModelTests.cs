using CleanMenu.App.Services;
using CleanMenu.App.ViewModels;
using CleanMenu.Core.Advanced;
using CleanMenu.Core.Maintenance;
using CleanMenu.Core.Network;

namespace CleanMenu.App.Tests.ViewModels;

public class NetworkViewModelTests
{
    private static NetworkAdapterInfo Adapter(string name, bool up, AdapterKind kind) =>
        new(name, name + " desc", kind, up, 300_000_000, "AA-BB", ["192.168.0.10"], [], ["192.168.0.1"], ["8.8.8.8"], true);

    private sealed class FakeNetwork : INetworkInfoProvider
    {
        public IReadOnlyList<NetworkAdapterInfo> GetAdapters() =>
            [Adapter("Wi-Fi", true, AdapterKind.WiFi), Adapter("Ethernet", false, AdapterKind.Ethernet), Adapter("vEthernet", true, AdapterKind.Virtual)];
    }

    private sealed class FakePing : IConnectivityTester
    {
        public List<string> Hosts { get; } = [];

        public Task<IReadOnlyList<PingReplyInfo>> PingAsync(string host, int count, CancellationToken cancellationToken = default)
        {
            Hosts.Add(host);
            return Task.FromResult<IReadOnlyList<PingReplyInfo>>(Enumerable.Repeat(new PingReplyInfo(20), count).ToList());
        }
    }

    private sealed class FakeSpeed : ISpeedTester
    {
        public string ServerName => "teste";

        public long ApproximateDataBytes => 35_000_000;

        public Task<SpeedTestResult> RunAsync(IProgress<string>? progress, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedTestResult(15, new TransferMeasurement(12_500_000, TimeSpan.FromSeconds(1)), new TransferMeasurement(2_500_000, TimeSpan.FromSeconds(1)), "teste"));
    }

    private sealed class FakeDownloader : IFileDownloader
    {
        public List<(Uri Url, string Destination)> Calls { get; } = [];

        public Task<long> DownloadAsync(Uri url, string destination, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default)
        {
            Calls.Add((url, destination));
            return Task.FromResult(1234L);
        }
    }

    private sealed class FakeHasher : IFileHasher
    {
        public Task<string?> Sha256Async(string path, CancellationToken cancellationToken) => Task.FromResult<string?>("ABC123");
    }

    private sealed class FakeSavePicker(string? result) : ISaveFilePicker
    {
        public string? Suggested { get; private set; }

        public string? Pick(string initialFolder, string suggestedName)
        {
            Suggested = suggestedName;
            return result;
        }
    }

    private static (NetworkViewModel Vm, FakePing Ping, FakeDownloader Downloader, FakeRunner Runner, FakeDialogs Dialogs, MemoryHistory History) Create(
        string? savePath = @"C:\Users\teste\Downloads\a.zip", bool confirm = true)
    {
        var ping = new FakePing();
        var downloader = new FakeDownloader();
        var runner = new FakeRunner();
        var dialogs = new FakeDialogs(confirm);
        var history = new MemoryHistory();
        var vm = new NetworkViewModel(new FakeNetwork(), ping, new FakeSpeed(), downloader, new FakeHasher(), new FakeSavePicker(savePath),
            new FakeProcessActions(), new FakeActions(), runner, dialogs, history, @"C:\Users\teste\Downloads");
        return (vm, ping, downloader, runner, dialogs, history);
    }

    [Fact]
    public void Adapters_ShowConnectedRealOnes_UnlessAllRequested()
    {
        var (vm, _, _, _, _, _) = Create();

        Assert.Equal(["Wi-Fi"], vm.Adapters.Select(a => a.Title));
        Assert.Equal(2, vm.HiddenAdapters);

        vm.ShowAllAdapters = true;
        Assert.Equal(3, vm.Adapters.Count);
    }

    [Fact]
    public async Task Ping_RejectsInvalidHost_AndReportsSummary()
    {
        var (vm, ping, _, _, _, _) = Create();

        vm.PingHost = "google.com & calc";
        await vm.PingCommand.ExecuteAsync(null);
        Assert.Empty(ping.Hosts);

        vm.PingHost = "google.com";
        await vm.PingCommand.ExecuteAsync(null);
        Assert.Equal(["google.com"], ping.Hosts);
        Assert.Contains("4 de 4", vm.PingResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpeedTest_ShowsMeasuredValues_AndRecordsHistory()
    {
        var (vm, _, _, _, _, history) = Create();

        await vm.SpeedTestCommand.ExecuteAsync(null);

        Assert.Equal("100,0 Mbps", vm.DownloadSpeed);
        Assert.Equal("20,0 Mbps", vm.UploadSpeed);
        Assert.Equal("15 ms", vm.Latency);
        Assert.Equal("Teste de velocidade", Assert.Single(history.Records).Type);
    }

    [Fact]
    public async Task Download_RefusesHttp_AndUsesChosenPath()
    {
        var (vm, _, downloader, _, _, _) = Create();

        vm.DownloadUrl = "http://exemplo.com/a.zip";
        await vm.DownloadCommand.ExecuteAsync(null);
        Assert.Empty(downloader.Calls);
        Assert.Contains("https://", vm.DownloadStatus, StringComparison.Ordinal);

        vm.DownloadUrl = "https://exemplo.com/a.zip";
        await vm.DownloadCommand.ExecuteAsync(null);
        Assert.Equal(@"C:\Users\teste\Downloads\a.zip", Assert.Single(downloader.Calls).Destination);
        Assert.Equal("ABC123", vm.DownloadedHash);
    }

    [Fact]
    public async Task Download_CancelledSaveDialog_DoesNothing()
    {
        var (vm, _, downloader, _, _, _) = Create(savePath: null);

        vm.DownloadUrl = "https://exemplo.com/a.zip";
        await vm.DownloadCommand.ExecuteAsync(null);

        Assert.Empty(downloader.Calls);
    }

    [Fact]
    public async Task ResetNetwork_NeedsConfirmation()
    {
        var (vm, _, _, runner, dialogs, _) = Create(confirm: false);

        await vm.ResetNetworkCommand.ExecuteAsync(null);

        Assert.Single(dialogs.Requests);
        Assert.Contains(dialogs.Requests[0].Details, d => d.Contains("netsh winsock reset", StringComparison.Ordinal));
        Assert.Empty(runner.Started);
        Assert.Equal(MaintenanceTool.ResetNetwork, MaintenanceCommands.Build(MaintenanceTool.ResetNetwork).Tool);
    }
}
