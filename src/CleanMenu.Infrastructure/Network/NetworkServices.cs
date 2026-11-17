using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using CleanMenu.Core.Network;

namespace CleanMenu.Infrastructure.Network;

/// <summary>Reads adapters through .NET's NetworkInterface (same data as "ipconfig /all"). Read-only.</summary>
public sealed class WindowsNetworkInfoProvider : INetworkInfoProvider
{
    public IReadOnlyList<NetworkAdapterInfo> GetAdapters()
    {
        var result = new List<NetworkAdapterInfo>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            IPInterfaceProperties? props = null;
            try
            {
                props = nic.GetIPProperties();
            }
            catch (NetworkInformationException)
            {
                // Adapter disappeared while reading; show it without addresses.
            }

            var mac = nic.GetPhysicalAddress().GetAddressBytes();
            var dhcp = false;
            try
            {
                dhcp = props?.GetIPv4Properties()?.IsDhcpEnabled ?? false;
            }
            catch (NetworkInformationException)
            {
                // IPv4 not bound to this adapter.
            }

            result.Add(new NetworkAdapterInfo(
                nic.Name,
                nic.Description,
                Classify(nic),
                nic.OperationalStatus == OperationalStatus.Up,
                Math.Max(0, nic.Speed),
                mac.Length == 0 ? null : string.Join("-", mac.Select(b => b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture))),
                Addresses(props?.UnicastAddresses.Select(a => a.Address), AddressFamily.InterNetwork),
                Addresses(props?.UnicastAddresses.Select(a => a.Address), AddressFamily.InterNetworkV6),
                Addresses(props?.GatewayAddresses.Select(a => a.Address), null),
                Addresses(props?.DnsAddresses, null),
                dhcp));
        }

        // Connected first, then real (non-virtual) adapters.
        return result.OrderByDescending(a => a.IsConnected).ThenBy(a => a.Kind == AdapterKind.Virtual).ThenBy(a => a.Name, StringComparer.CurrentCulture).ToList();
    }

    private static AdapterKind Classify(NetworkInterface nic)
    {
        var description = nic.Description;
        if (description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
            || description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
            || description.Contains("VPN", StringComparison.OrdinalIgnoreCase)
            || description.Contains("TAP-", StringComparison.OrdinalIgnoreCase)
            || description.Contains("Loopback", StringComparison.OrdinalIgnoreCase)
            || description.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase))
        {
            return AdapterKind.Virtual;
        }

        return nic.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Wireless80211 => AdapterKind.WiFi,
            NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT => AdapterKind.Ethernet,
            _ => AdapterKind.Other,
        };
    }

    private static List<string> Addresses(IEnumerable<System.Net.IPAddress>? addresses, AddressFamily? family) =>
        addresses?.Where(a => family is null || a.AddressFamily == family)
            .Where(a => !a.Equals(System.Net.IPAddress.Any) && !a.Equals(System.Net.IPAddress.IPv6Any))
            .Select(a => a.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];
}

/// <summary>Real ICMP pings (System.Net.NetworkInformation.Ping).</summary>
public sealed class PingConnectivityTester : IConnectivityTester
{
    public async Task<IReadOnlyList<PingReplyInfo>> PingAsync(string host, int count, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        var replies = new List<PingReplyInfo>(count);
        using var ping = new Ping();
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var reply = await ping.SendPingAsync(host, TimeSpan.FromSeconds(3), cancellationToken: cancellationToken).ConfigureAwait(false);
                replies.Add(new PingReplyInfo(reply.Status == IPStatus.Success ? reply.RoundtripTime : null));
            }
            catch (PingException)
            {
                // Host not found or no network: counts as a lost reply.
                replies.Add(new PingReplyInfo(null));
            }

            if (i < count - 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            }
        }

        return replies;
    }
}

/// <summary>
/// Measures real speed against Cloudflare's public test server (speed.cloudflare.com), the same
/// one used by its web speed test. Each phase stops after a time limit so slow connections do not
/// wait forever; speed is computed from the bytes actually transferred.
/// </summary>
public sealed class CloudflareSpeedTester(HttpClient http) : ISpeedTester
{
    private const long DownloadBytes = 25_000_000;
    private const long UploadBytes = 10_000_000;
    private static readonly TimeSpan PhaseLimit = TimeSpan.FromSeconds(15);
    private static readonly Uri Base = new("https://speed.cloudflare.com/");

    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    public string ServerName => "speed.cloudflare.com (Cloudflare)";

    public long ApproximateDataBytes => DownloadBytes + UploadBytes;

    public async Task<SpeedTestResult> RunAsync(IProgress<string>? progress, CancellationToken cancellationToken = default)
    {
        progress?.Report("Medindo o tempo de resposta…");
        var latency = await MeasureLatencyAsync(cancellationToken).ConfigureAwait(false);

        progress?.Report("Medindo a velocidade de download…");
        var download = await MeasureDownloadAsync(cancellationToken).ConfigureAwait(false);

        progress?.Report("Medindo a velocidade de upload…");
        TransferMeasurement? upload = null;
        try
        {
            upload = await MeasureUploadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            // Some networks block uploads to the test server; download is still reported.
        }

        return new SpeedTestResult(latency, download, upload, ServerName);
    }

    private async Task<long?> MeasureLatencyAsync(CancellationToken cancellationToken)
    {
        var times = new List<long>();
        for (var i = 0; i < 5; i++)
        {
            var watch = Stopwatch.StartNew();
            using var response = await _http.GetAsync(new Uri(Base, "__down?bytes=0"), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            watch.Stop();
            if (response.IsSuccessStatusCode && i > 0)
            {
                times.Add(watch.ElapsedMilliseconds); // the first request also opens the connection
            }
        }

        return times.Count == 0 ? null : times.Order().ElementAt(times.Count / 2);
    }

    private async Task<TransferMeasurement> MeasureDownloadAsync(CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(PhaseLimit);
        long received = 0;
        var watch = Stopwatch.StartNew();
        try
        {
            using var response = await _http.GetAsync(new Uri(Base, $"__down?bytes={DownloadBytes}"), HttpCompletionOption.ResponseHeadersRead, limit.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync(limit.Token).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await stream.ReadAsync(buffer, limit.Token).ConfigureAwait(false)) > 0)
                {
                    received += read;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Time limit reached: use what arrived.
        }

        watch.Stop();
        return new TransferMeasurement(received, watch.Elapsed);
    }

    private async Task<TransferMeasurement> MeasureUploadAsync(CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(PhaseLimit);
        var source = new CountingRandomStream(UploadBytes);
        using var content = new StreamContent(source, 65536);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Headers.ContentLength = UploadBytes;
        var watch = Stopwatch.StartNew();
        try
        {
            using var response = await _http.PostAsync(new Uri(Base, "__up"), content, limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Time limit reached: use what was sent.
        }

        watch.Stop();
        return new TransferMeasurement(source.Sent, watch.Elapsed);
    }

    /// <summary>Generates random bytes on the fly and counts how many were read (sent).</summary>
    private sealed class CountingRandomStream(long length) : Stream
    {
        public long Sent { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position { get => Sent; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = (int)Math.Min(count, length - Sent);
            if (n <= 0)
            {
                return 0;
            }

            Random.Shared.NextBytes(buffer.AsSpan(offset, n));
            Sent += n;
            return n;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

/// <summary>HTTPS-only file download to a ".part" file, renamed when complete.</summary>
public sealed class HttpsFileDownloader(HttpClient http) : IFileDownloader
{
    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task<long> DownloadAsync(Uri url, string destination, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        if (url.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Somente https.", nameof(url));
        }

        var partial = destination + ".part";
        long received = 0;
        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.RequestMessage?.RequestUri is { } final && final.Scheme != Uri.UriSchemeHttps)
            {
                throw new HttpRequestException("O site redirecionou para uma conexão sem proteção (http).");
            }

            var total = response.Content.Headers.ContentLength;
            var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (source.ConfigureAwait(false))
            {
                var target = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                await using (target.ConfigureAwait(false))
                {
                    var buffer = new byte[81920];
                    int read;
                    var lastReport = Stopwatch.StartNew();
                    while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        received += read;
                        if (lastReport.ElapsedMilliseconds > 200)
                        {
                            progress?.Report(new DownloadProgress(received, total));
                            lastReport.Restart();
                        }
                    }
                }
            }

            File.Move(partial, destination, overwrite: true);
            progress?.Report(new DownloadProgress(received, total));
            return received;
        }
        catch
        {
            TryDelete(partial);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
