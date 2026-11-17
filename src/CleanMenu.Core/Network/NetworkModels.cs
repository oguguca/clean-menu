using System.Globalization;

namespace CleanMenu.Core.Network;

/// <summary>Kind of network connection, in plain words.</summary>
public enum AdapterKind
{
    Other,
    Ethernet,
    WiFi,
    Virtual,
}

/// <summary>A network adapter as reported by Windows (what "ipconfig /all" shows).</summary>
public sealed record NetworkAdapterInfo(
    string Name,
    string Description,
    AdapterKind Kind,
    bool IsConnected,
    long SpeedBitsPerSecond,
    string? MacAddress,
    IReadOnlyList<string> IPv4Addresses,
    IReadOnlyList<string> IPv6Addresses,
    IReadOnlyList<string> Gateways,
    IReadOnlyList<string> DnsServers,
    bool DhcpEnabled);

/// <summary>Reads network adapters. Read-only.</summary>
public interface INetworkInfoProvider
{
    IReadOnlyList<NetworkAdapterInfo> GetAdapters();
}

/// <summary>One ping reply; null round trip means the reply did not arrive.</summary>
public sealed record PingReplyInfo(long? RoundTripMs);

/// <summary>Summary of a ping test, like the last lines of "ping google.com".</summary>
public sealed record PingSummary(string Host, int Sent, int Received, long? MinMs, long? MaxMs, long? AverageMs)
{
    public int LostPercent => Sent == 0 ? 0 : (int)Math.Round(100.0 * (Sent - Received) / Sent);

    public static PingSummary From(string host, IReadOnlyList<PingReplyInfo> replies)
    {
        ArgumentNullException.ThrowIfNull(replies);
        var times = replies.Where(r => r.RoundTripMs is not null).Select(r => r.RoundTripMs!.Value).ToList();
        return times.Count == 0
            ? new PingSummary(host, replies.Count, 0, null, null, null)
            : new PingSummary(host, replies.Count, times.Count, times.Min(), times.Max(), (long)Math.Round(times.Average()));
    }

    /// <summary>Plain-language verdict.</summary>
    public string Verdict => Received == 0
        ? $"Sem resposta de {Host}. Verifique se o computador está conectado à internet."
        : LostPercent > 0
            ? $"Conectado, mas {LostPercent}% dos pacotes se perderam. A conexão está instável."
            : AverageMs <= 60
                ? $"Conectado. Resposta rápida ({AverageMs} ms em média)."
                : AverageMs <= 150
                    ? $"Conectado. Resposta normal ({AverageMs} ms em média)."
                    : $"Conectado, mas a resposta está lenta ({AverageMs} ms em média).";
}

/// <summary>Sends pings. Real ICMP echo requests.</summary>
public interface IConnectivityTester
{
    Task<IReadOnlyList<PingReplyInfo>> PingAsync(string host, int count, CancellationToken cancellationToken = default);
}

/// <summary>Result of a measured transfer.</summary>
public sealed record TransferMeasurement(long Bytes, TimeSpan Elapsed)
{
    public double MegabitsPerSecond => Elapsed <= TimeSpan.Zero ? 0 : Bytes * 8 / Elapsed.TotalSeconds / 1_000_000;

    public string Text => string.Create(CultureInfo.GetCultureInfo("pt-BR"), $"{MegabitsPerSecond:0.0} Mbps");
}

/// <summary>Result of the internet speed test.</summary>
public sealed record SpeedTestResult(long? LatencyMs, TransferMeasurement Download, TransferMeasurement? Upload, string Server);

/// <summary>Measures real download and upload speed against a public test server.</summary>
public interface ISpeedTester
{
    /// <summary>Server name shown to the user before the test.</summary>
    string ServerName { get; }

    /// <summary>Approximate data used by one test, in bytes.</summary>
    long ApproximateDataBytes { get; }

    Task<SpeedTestResult> RunAsync(IProgress<string>? progress, CancellationToken cancellationToken = default);
}
