using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using CleanMenu.App.Navigation;
using CleanMenu.App.Services;
using CleanMenu.Core.Advanced;
using CleanMenu.Core.Formatting;
using CleanMenu.Core.History;
using CleanMenu.Core.Maintenance;
using CleanMenu.Core.Network;
using CleanMenu.Core.Processes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CleanMenu.App.ViewModels;

/// <summary>A network adapter, in plain words.</summary>
public sealed class NetworkAdapterRowViewModel(NetworkAdapterInfo adapter)
{
    public NetworkAdapterInfo Adapter { get; } = adapter ?? throw new ArgumentNullException(nameof(adapter));

    public string Title => Adapter.Name;

    public string Description => Adapter.Description;

    public string KindText => Adapter.Kind switch
    {
        AdapterKind.WiFi => "Wi-Fi",
        AdapterKind.Ethernet => "Cabo de rede",
        AdapterKind.Virtual => "Virtual ou VPN",
        _ => "Outro",
    };

    public bool IsConnected => Adapter.IsConnected;

    public string StatusText => Adapter.IsConnected ? "Conectado" : "Desconectado";

    public string SpeedText => Adapter.SpeedBitsPerSecond <= 0
        ? "—"
        : Adapter.SpeedBitsPerSecond >= 1_000_000_000
            ? string.Create(CultureInfo.GetCultureInfo("pt-BR"), $"{Adapter.SpeedBitsPerSecond / 1e9:0.#} Gbps")
            : string.Create(CultureInfo.GetCultureInfo("pt-BR"), $"{Adapter.SpeedBitsPerSecond / 1e6:0} Mbps");

    public string IPv4Text => Join(Adapter.IPv4Addresses);

    public string IPv6Text => Join(Adapter.IPv6Addresses);

    public string GatewayText => Join(Adapter.Gateways);

    public string DnsText => Join(Adapter.DnsServers);

    public string MacText => Adapter.MacAddress ?? "—";

    public string DhcpText => Adapter.DhcpEnabled ? "Automático (DHCP)" : "Manual (IP fixo) ou não usado";

    /// <summary>Text for "Copiar" (similar to ipconfig /all).</summary>
    public string CopyText => string.Join(Environment.NewLine,
        $"{Title} ({Description})",
        $"  Tipo: {KindText} · {StatusText} · {SpeedText}",
        $"  IPv4: {IPv4Text}",
        $"  IPv6: {IPv6Text}",
        $"  Gateway: {GatewayText}",
        $"  DNS: {DnsText}",
        $"  MAC: {MacText}",
        $"  Endereço: {DhcpText}");

    private static string Join(IReadOnlyList<string> values) => values.Count == 0 ? "—" : string.Join(", ", values);
}

/// <summary>
/// Rede e Internet page: adapters and IP configuration (like ipconfig), ping test, real speed
/// test, network reset (visible, with confirmation) and HTTPS-only file download.
/// </summary>
public sealed partial class NetworkViewModel : PageViewModel, IDisposable
{
    private readonly INetworkInfoProvider _network;
    private readonly IConnectivityTester _ping;
    private readonly ISpeedTester _speed;
    private readonly IFileDownloader _downloader;
    private readonly IFileHasher _hasher;
    private readonly ISaveFilePicker _savePicker;
    private readonly IProcessActions _processes;
    private readonly ISystemActions _actions;
    private readonly IOperationHistory _history;
    private readonly MaintenanceLauncher _launcher;
    private readonly string _downloadsFolder;
    private List<NetworkAdapterInfo> _all = [];
    private CancellationTokenSource? _downloadCts;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _showAllAdapters;

    [ObservableProperty]
    private string _pingHost = "google.com";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PingCommand), nameof(SpeedTestCommand), nameof(DownloadCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _pingResult;

    [ObservableProperty]
    private string? _speedProgress;

    [ObservableProperty]
    private string? _downloadSpeed;

    [ObservableProperty]
    private string? _uploadSpeed;

    [ObservableProperty]
    private string? _latency;

    [ObservableProperty]
    private string? _speedNote;

    [ObservableProperty]
    private string? _downloadUrl;

    [ObservableProperty]
    private double _downloadPercent;

    [ObservableProperty]
    private string? _downloadStatus;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private string? _downloadedPath;

    [ObservableProperty]
    private string? _downloadedHash;

    public NetworkViewModel(
        INetworkInfoProvider network,
        IConnectivityTester ping,
        ISpeedTester speed,
        IFileDownloader downloader,
        IFileHasher hasher,
        ISaveFilePicker savePicker,
        IProcessActions processes,
        ISystemActions actions,
        IMaintenanceRunner runner,
        IDialogService dialogs,
        IOperationHistory history,
        string downloadsFolder)
        : base(PageCatalog.Get(AppPage.Network))
    {
        _network = network ?? throw new ArgumentNullException(nameof(network));
        _ping = ping ?? throw new ArgumentNullException(nameof(ping));
        _speed = speed ?? throw new ArgumentNullException(nameof(speed));
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _savePicker = savePicker ?? throw new ArgumentNullException(nameof(savePicker));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _launcher = new MaintenanceLauncher(runner, dialogs, history);
        _downloadsFolder = downloadsFolder ?? throw new ArgumentNullException(nameof(downloadsFolder));
        Refresh();
    }

    public ObservableCollection<NetworkAdapterRowViewModel> Adapters { get; } = [];

    public int HiddenAdapters => _all.Count - Adapters.Count;

    public string SpeedServer => _speed.ServerName;

    public string SpeedDataText => $"O teste usa cerca de {ByteSize.Format(_speed.ApproximateDataBytes)} da sua internet.";

    partial void OnShowAllAdaptersChanged(bool value) => ApplyFilter();

    [RelayCommand]
    internal void Refresh()
    {
        try
        {
            _all = [.. _network.GetAdapters()];
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _all = [];
            Message = "Não foi possível ler as conexões de rede.";
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Adapters.Clear();
        foreach (var adapter in _all.Where(a => ShowAllAdapters || (a.IsConnected && a.Kind != AdapterKind.Virtual && a.IPv4Addresses.Count > 0)))
        {
            Adapters.Add(new NetworkAdapterRowViewModel(adapter));
        }

        OnPropertyChanged(nameof(HiddenAdapters));
    }

    [RelayCommand]
    private void CopyAdapters()
    {
        _actions.CopyToClipboard(string.Join(Environment.NewLine + Environment.NewLine, _all.Select(a => new NetworkAdapterRowViewModel(a).CopyText)));
        Message = "As informações de rede foram copiadas.";
    }

    private bool CanTest() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task PingAsync()
    {
        var host = PingHost?.Trim();
        if (!DownloadRules.IsValidHost(host))
        {
            PingResult = "Digite um endereço válido, por exemplo google.com.";
            return;
        }

        IsBusy = true;
        PingResult = $"Testando a conexão com {host}…";
        try
        {
            var replies = await _ping.PingAsync(host!, 4).ConfigureAwait(true);
            var summary = PingSummary.From(host!, replies);
            PingResult = summary.Received == 0
                ? summary.Verdict
                : $"{summary.Verdict} Respostas: {summary.Received} de {summary.Sent} · mínimo {summary.MinMs} ms · máximo {summary.MaxMs} ms.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            PingResult = "Não foi possível fazer o teste de conexão.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task SpeedTestAsync()
    {
        IsBusy = true;
        DownloadSpeed = UploadSpeed = Latency = SpeedNote = null;
        try
        {
            var result = await _speed.RunAsync(new Progress<string>(p => SpeedProgress = p)).ConfigureAwait(true);
            Latency = result.LatencyMs is { } ms ? $"{ms} ms" : "—";
            DownloadSpeed = result.Download.Text;
            UploadSpeed = result.Upload?.Text ?? "—";
            SpeedNote = $"Medido agora com o servidor {result.Server}. O resultado muda conforme o horário, o Wi-Fi e outros aparelhos usando a internet.";
            await _history.RecordActionAsync("Teste de velocidade", $"Download {DownloadSpeed} · Upload {UploadSpeed} · Resposta {Latency}", true).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            SpeedNote = "Não foi possível fazer o teste. Verifique se o computador está conectado à internet.";
        }
        finally
        {
            SpeedProgress = null;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResetNetworkAsync()
    {
        var message = await _launcher.RunAsync(MaintenanceCommands.Build(MaintenanceTool.ResetNetwork), "Rede", destructive: true).ConfigureAwait(true);
        if (message is not null)
        {
            Message = message;
        }
    }

    [RelayCommand]
    private void OpenNetworkConnections()
    {
        if (!_actions.OpenTool("ncpa.cpl"))
        {
            Message = "Não foi possível abrir as Conexões de Rede.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task DownloadAsync()
    {
        var url = DownloadRules.ParseHttpsUrl(DownloadUrl, out var error);
        if (url is null)
        {
            DownloadStatus = error;
            return;
        }

        var destination = _savePicker.Pick(_downloadsFolder, DownloadRules.SuggestFileName(url));
        if (destination is null)
        {
            return;
        }

        IsBusy = true;
        IsDownloading = true;
        DownloadedPath = DownloadedHash = null;
        DownloadPercent = 0;
        DownloadStatus = $"Baixando de {url.Host}…";
        _downloadCts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<DownloadProgress>(p =>
            {
                DownloadPercent = p.Total is > 0 ? 100.0 * p.Received / p.Total.Value : 0;
                DownloadStatus = p.Total is > 0
                    ? $"Baixando de {url.Host}: {ByteSize.Format(p.Received)} de {ByteSize.Format(p.Total.Value)}"
                    : $"Baixando de {url.Host}: {ByteSize.Format(p.Received)}";
            });
            var bytes = await _downloader.DownloadAsync(url, destination, progress, _downloadCts.Token).ConfigureAwait(true);
            DownloadPercent = 100;
            DownloadedPath = destination;
            DownloadedHash = await _hasher.Sha256Async(destination, CancellationToken.None).ConfigureAwait(true);
            DownloadStatus = DownloadRules.IsRunnable(destination)
                ? $"Pronto: {ByteSize.Format(bytes)} salvos. Atenção: este arquivo pode executar programas. Só abra se você confia no site de onde ele veio."
                : $"Pronto: {ByteSize.Format(bytes)} salvos.";
            await _history.RecordActionAsync("Download de arquivo", $"{url.Host} → {Path.GetFileName(destination)}", true).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            DownloadStatus = "Download cancelado. Nenhum arquivo incompleto foi deixado.";
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            DownloadStatus = $"O download falhou: {ex.Message}";
            await _history.RecordActionAsync("Download de arquivo", url.Host, false, ex.Message).ConfigureAwait(true);
        }
        finally
        {
            _downloadCts.Dispose();
            _downloadCts = null;
            IsDownloading = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelDownload() => _downloadCts?.Cancel();

    public void Dispose() => _downloadCts?.Dispose();

    [RelayCommand]
    private void ShowDownloaded()
    {
        if (DownloadedPath is not null)
        {
            _processes.OpenFileLocation(DownloadedPath);
        }
    }

    [RelayCommand]
    private void CopyHash()
    {
        if (DownloadedHash is not null)
        {
            _actions.CopyToClipboard(DownloadedHash);
            Message = "Código SHA-256 copiado.";
        }
    }
}
