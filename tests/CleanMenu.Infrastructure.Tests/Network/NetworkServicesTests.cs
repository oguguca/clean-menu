using CleanMenu.Infrastructure.Network;

namespace CleanMenu.Infrastructure.Tests.Network;

/// <summary>Read-only and local-only: no internet access, nothing changed.</summary>
public class NetworkServicesTests
{
    [Fact]
    public void Adapters_AreRead_WithoutLoopback()
    {
        var adapters = new WindowsNetworkInfoProvider().GetAdapters();

        Assert.DoesNotContain(adapters, a => a.Description.Contains("Loopback Pseudo", StringComparison.OrdinalIgnoreCase));
        Assert.All(adapters, a => Assert.False(string.IsNullOrWhiteSpace(a.Name)));
    }

    [Fact]
    public async Task Ping_Localhost_Answers()
    {
        var replies = await new PingConnectivityTester().PingAsync("127.0.0.1", 2);

        Assert.Equal(2, replies.Count);
        Assert.All(replies, r => Assert.NotNull(r.RoundTripMs));
    }

    [Fact]
    public async Task Downloader_RefusesHttp()
    {
        using var http = new HttpClient();
        var downloader = new HttpsFileDownloader(http);
        var target = Path.Combine(Path.GetTempPath(), "CleanMenuTests", Guid.NewGuid().ToString("N"), "x.bin");

        await Assert.ThrowsAsync<ArgumentException>(() => downloader.DownloadAsync(new Uri("http://exemplo.com/x.bin"), target, null));
        Assert.False(File.Exists(target));
    }
}
