using System.Collections.ObjectModel;
using CleanMenu.App.Navigation;
using CleanMenu.App.Services;
using CleanMenu.Core.Disks;
using CleanMenu.Core.Formatting;
using CleanMenu.Core.Monitoring;
using CleanMenu.Core.Platform;
using CleanMenu.Core.SystemInfo;
using CleanMenu.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CleanMenu.App.ViewModels;

/// <summary>
/// Home page. Shows only real data: live indicators, computer facts and drive space. Metrics that
/// depend on the computer analysis stay empty ("—") until that analysis runs.
/// </summary>
public sealed partial class DashboardViewModel : PageViewModel
{
    private const string AfterAnalysis = "Aparece depois da análise";

    private readonly ISystemInfoProvider _systemInfo;
    private readonly IVolumeProvider _volumes;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILiveMonitor _monitor;

    [ObservableProperty]
    private string _windowsName;

    [ObservableProperty]
    private string _windowsVersion;

    [ObservableProperty]
    private string _processorName = "Carregando…";

    [ObservableProperty]
    private string _installedMemory = "Carregando…";

    [ObservableProperty]
    private string _graphicsName = "Carregando…";

    public DashboardViewModel(
        OsProfile osProfile,
        ILiveMonitor monitor,
        ISystemInfoProvider systemInfo,
        IVolumeProvider volumes,
        IUiDispatcher dispatcher,
        DashboardAnalysisServices analysisServices,
        string machineName,
        int osBuild)
        : base(PageCatalog.Get(AppPage.Dashboard))
    {
        ArgumentNullException.ThrowIfNull(osProfile);
        _analysis = analysisServices ?? throw new ArgumentNullException(nameof(analysisServices));
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _systemInfo = systemInfo ?? throw new ArgumentNullException(nameof(systemInfo));
        _volumes = volumes ?? throw new ArgumentNullException(nameof(volumes));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        MachineName = string.IsNullOrWhiteSpace(machineName) ? MetricCardViewModel.Pending : machineName;
        _windowsName = OsDisplay.Name(osProfile.Effective);
        _windowsVersion = osBuild > 0 ? $"Build {osBuild}" : MetricCardViewModel.Pending;

        HealthCard = new MetricCardViewModel("Saúde geral", Glyphs.CheckMark,
            "Um resumo de como está o computador, calculado a partir do que a análise encontrar: espaço livre, memória e programas que abrem sozinhos.")
            .With(MetricCardViewModel.Pending, AfterAnalysis);
        FreeableCard = new MetricCardViewModel("Espaço para liberar", Glyphs.Delete,
            "Quanto espaço os arquivos inúteis (temporários e cache) estão ocupando no seu disco e podem ser apagados com segurança.")
            .With(MetricCardViewModel.Pending, AfterAnalysis);
        FreeMemoryCard = new MetricCardViewModel("Memória livre", Glyphs.Memory,
            "Memória RAM que ainda está livre para os programas. Quando ela acaba, o computador fica lento.")
            .With(MetricCardViewModel.Pending, "Medindo…");
        TempFilesCard = new MetricCardViewModel("Arquivos temporários", Glyphs.Page,
            "Arquivos que os programas criam para uso rápido e depois esquecem de apagar.")
            .With(MetricCardViewModel.Pending, AfterAnalysis);
        StartupCard = new MetricCardViewModel("Programas ao ligar", Glyphs.Power,
            "Programas que abrem sozinhos quando o computador liga. Muitos deles deixam o início lento.")
            .With(MetricCardViewModel.Pending, AfterAnalysis);
        LastCleanupCard = new MetricCardViewModel("Última limpeza", Glyphs.History,
            "Quando o Clean Menu limpou o seu computador pela última vez.")
            .With("Nunca", "Nenhuma limpeza feita pelo Clean Menu");

        SummaryCards = [HealthCard, FreeableCard, FreeMemoryCard, TempFilesCard, StartupCard, LastCleanupCard];

        CpuCard = new MetricCardViewModel("Processador", Glyphs.Diagnostic,
            "Quanto do processador (a parte que faz as contas do computador) está sendo usado agora. Picos rápidos são normais.");
        MemoryCard = new MetricCardViewModel("Memória", Glyphs.Memory,
            "Quanto da memória RAM está ocupada pelos programas abertos.");
        GpuCard = new MetricCardViewModel("Placa de vídeo", Glyphs.Chart,
            "Quanto a placa de vídeo está trabalhando. Sobe com jogos, vídeos e programas de imagem.");
        TemperatureCard = new MetricCardViewModel("Temperatura", Glyphs.Lightning,
            "Temperatura do processador. O Windows não informa esse valor de forma confiável. Ler esse dado exigiria instalar um driver especial no sistema, e o Clean Menu não faz isso por segurança.")
            .With(MetricCardViewModel.Pending, "Não disponível: o Windows não informa a temperatura");

        LiveCards = [CpuCard, MemoryCard, GpuCard, TemperatureCard];

        _monitor.SampleTaken += (_, sample) => _dispatcher.Post(() => Apply(sample));
        if (_monitor.Latest is { } latest)
        {
            Apply(latest);
        }

        LoadVolumes();
        _ = LoadSystemInfoAsync();
        InitializeAnalysis();
    }

    public string MachineName { get; }

    public MetricCardViewModel HealthCard { get; }

    public MetricCardViewModel FreeableCard { get; }

    public MetricCardViewModel FreeMemoryCard { get; }

    public MetricCardViewModel TempFilesCard { get; }

    public MetricCardViewModel StartupCard { get; }

    public MetricCardViewModel LastCleanupCard { get; }

    public IReadOnlyList<MetricCardViewModel> SummaryCards { get; }

    public MetricCardViewModel CpuCard { get; }

    public MetricCardViewModel MemoryCard { get; }

    public MetricCardViewModel GpuCard { get; }

    public MetricCardViewModel TemperatureCard { get; }

    public IReadOnlyList<MetricCardViewModel> LiveCards { get; }

    public ObservableCollection<VolumeRowViewModel> Volumes { get; } = [];

    [RelayCommand]
    private void RefreshVolumes() => LoadVolumes();

    internal void Apply(LiveSample sample)
    {
        var memory = sample.Memory;
        var memoryStatus = HealthDisplay.ToStatus(memory.Health);

        CpuCard.With(HealthDisplay.Percent(sample.CpuPercent),
            double.IsNaN(sample.CpuPercent) ? "Medindo…" : "Últimos 2 minutos",
            HealthDisplay.ToStatus(HealthThresholds.ForCpuPercent(sample.CpuPercent)));
        CpuCard.History = _monitor.CpuHistory;

        MemoryCard.With(HealthDisplay.Percent(memory.UsedPercent),
            $"{ByteSize.Format(memory.UsedBytes)} de {ByteSize.Format(memory.TotalBytes)}", memoryStatus);
        MemoryCard.History = _monitor.MemoryHistory;

        FreeMemoryCard.With(ByteSize.Format(memory.AvailableBytes),
            $"{HealthDisplay.Percent(memory.AvailablePercent)} da memória está livre", memoryStatus);

        if (sample.GpuPercent is null)
        {
            GpuCard.With(MetricCardViewModel.Pending, "Não disponível: o Windows não informa o uso da placa de vídeo neste computador");
            GpuCard.History = null;
        }
        else
        {
            GpuCard.With(HealthDisplay.Percent(sample.GpuPercent.Value),
                double.IsNaN(sample.GpuPercent.Value) ? "Medindo…" : "Últimos 2 minutos");
            GpuCard.History = _monitor.GpuHistory;
        }
    }

    private void LoadVolumes()
    {
        Volumes.Clear();
        foreach (var volume in _volumes.GetVolumes().Where(v => !v.IsRemovable))
        {
            Volumes.Add(new VolumeRowViewModel(volume));
        }
    }

    private async Task LoadSystemInfoAsync()
    {
        SystemInformation? info = null;
        try
        {
            info = await _systemInfo.GetAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Shown as "não informado" below; the dashboard keeps working.
        }

        _dispatcher.Post(() =>
        {
            const string NotReported = "Não informado";
            if (info is null)
            {
                ProcessorName = InstalledMemory = GraphicsName = NotReported;
                return;
            }

            WindowsName = info.OperatingSystem.Caption?.Replace("Microsoft ", string.Empty, StringComparison.Ordinal) ?? WindowsName;
            WindowsVersion = info.OperatingSystem.DisplayVersion is { } display
                ? $"{display} (build {info.OperatingSystem.Build})"
                : $"Build {info.OperatingSystem.Build}";
            ProcessorName = info.Processor?.Name ?? NotReported;
            InstalledMemory = ByteSize.Format(info.Memory.InstalledBytes ?? info.Memory.UsableBytes);
            GraphicsName = info.Graphics.Count > 0 ? string.Join(" + ", info.Graphics.Select(g => g.Name)) : NotReported;
        });
    }
}
