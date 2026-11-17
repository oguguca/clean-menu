using CleanMenu.Core.Network;

namespace CleanMenu.Core.Tests.Network;

public class NetworkRulesTests
{
    [Fact]
    public void PingSummary_ComputesLikePing()
    {
        var summary = PingSummary.From("google.com", [new(10), new(30), new(null), new(20)]);

        Assert.Equal(4, summary.Sent);
        Assert.Equal(3, summary.Received);
        Assert.Equal(10, summary.MinMs);
        Assert.Equal(30, summary.MaxMs);
        Assert.Equal(20, summary.AverageMs);
        Assert.Equal(25, summary.LostPercent);
        Assert.Contains("instável", summary.Verdict, StringComparison.Ordinal);
    }

    [Fact]
    public void PingSummary_NoReplies_SaysNoConnection()
    {
        var summary = PingSummary.From("google.com", [new(null), new(null)]);

        Assert.Equal(0, summary.Received);
        Assert.Null(summary.AverageMs);
        Assert.Contains("Sem resposta", summary.Verdict, StringComparison.Ordinal);
    }

    [Fact]
    public void Transfer_ComputesMegabits()
    {
        var measurement = new TransferMeasurement(12_500_000, TimeSpan.FromSeconds(1));

        Assert.Equal(100, measurement.MegabitsPerSecond, 3);
        Assert.Equal("100,0 Mbps", measurement.Text);
        Assert.Equal(0, new TransferMeasurement(100, TimeSpan.Zero).MegabitsPerSecond);
    }

    [Theory]
    [InlineData("https://exemplo.com/arquivo.zip", true)]
    [InlineData("  https://exemplo.com/a  ", true)]
    [InlineData("http://exemplo.com/arquivo.zip", false)]
    [InlineData("ftp://exemplo.com/a", false)]
    [InlineData("file:///C:/Windows/win.ini", false)]
    [InlineData("https://user:senha@exemplo.com/a", false)]
    [InlineData("exemplo.com/a", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyHttpsUrls_AreAccepted(string? text, bool ok)
    {
        var uri = DownloadRules.ParseHttpsUrl(text, out var error);

        Assert.Equal(ok, uri is not null);
        Assert.Equal(ok, error is null);
    }

    [Theory]
    [InlineData("https://exemplo.com/pasta/arquivo.zip", "arquivo.zip")]
    [InlineData("https://exemplo.com/meu%20arquivo.pdf?x=1", "meu arquivo.pdf")]
    [InlineData("https://exemplo.com/", "download")]
    [InlineData("https://exemplo.com/a%3Ab.txt", "a_b.txt")]
    public void SuggestFileName_IsSafe(string url, string expected)
    {
        Assert.Equal(expected, DownloadRules.SuggestFileName(new Uri(url)));
    }

    [Theory]
    [InlineData("setup.exe", true)]
    [InlineData("script.PS1", true)]
    [InlineData("foto.jpg", false)]
    [InlineData("doc.pdf", false)]
    public void RunnableFiles_AreFlagged(string name, bool runnable)
    {
        Assert.Equal(runnable, DownloadRules.IsRunnable(name));
    }

    [Theory]
    [InlineData("google.com", true)]
    [InlineData("8.8.8.8", true)]
    [InlineData("::1", true)]
    [InlineData("google.com & calc", false)]
    [InlineData("-n 1000 x", false)]
    [InlineData("", false)]
    public void PingHost_Validation(string host, bool ok)
    {
        Assert.Equal(ok, DownloadRules.IsValidHost(host));
    }
}
