using System.Collections.ObjectModel;
using CleanMenu.App.Navigation;
using CleanMenu.App.Services;
using CleanMenu.Core.Disks;
using CleanMenu.Core.Formatting;
using CleanMenu.Core.SystemInfo;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CleanMenu.App.ViewModels;

/// <summary>A labeled fact ("Processador": "Intel Core i5…").</summary>
public sealed record FactItem(string Label, string Value);

/// <summary>A titled group of facts.</summary>
public sealed record FactGroup(string Title, string Glyph, IReadOnlyList<FactItem> Facts);

/// <summary>Informações do Sistema page: hardware and Windows facts plus a copyable report.</summary>
public sealed partial class SystemInfoViewModel : PageViewModel
{
    private const string NotReported = "Não informado";

    private readonly ISystemInfoProvider _systemInfo;
    private readonly IVolumeProvider _volumes;
    private readonly IPhysicalDiskProvider _disks;
    private readonly ISystemActions _actions;
    private readonly IUiDispatcher _dispatcher;
    private string? _report;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string? _message;

    public SystemInfoViewModel(
        ISystemInfoProvider systemInfo,
        IVolumeProvider volumes,
        IPhysicalDiskProvider disks,
        ISystemActions actions,
        IUiDispatcher dispatcher)
        : base(PageCatalog.Get(AppPage.SystemInfo))
    {
        _systemInfo = systemInfo ?? throw new ArgumentNullException(nameof(systemInfo));
        _volumes = volumes ?? throw new ArgumentNullException(nameof(volumes));
        _disks = disks ?? throw new ArgumentNullException(nameof(disks));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _ = LoadAsync();
    }

    public ObservableCollection<FactGroup> Groups { get; } = [];

    [RelayCommand]
    private void CopyReport()
    {
        if (_report is null)
        {
            return;
        }

        _actions.CopyToClipboard(_report);
        Message = "Informações copiadas. Agora é só colar (Ctrl+V) na conversa com quem está te ajudando.";
    }

    internal async Task LoadAsync()
    {
        try
        {
            var infoTask = _systemInfo.GetAsync();
            var disksTask = _disks.GetDisksAsync();
            var volumes = _volumes.GetVolumes();
            var info = await infoTask.ConfigureAwait(false);
            var disks = await disksTask.ConfigureAwait(false);
            var groups = BuildGroups(info, volumes, disks);
            var report = SystemReport.Build(info, volumes, disks);

            _dispatcher.Post(() =>
            {
                Groups.Clear();
                foreach (var group in groups)
                {
                    Groups.Add(group);
                }

                _report = report;
                IsLoading = false;
            });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _dispatcher.Post(() =>
            {
                IsLoading = false;
                Message = "Não foi possível ler as informações do sistema agora. Tente abrir esta página de novo.";
            });
        }
    }

    internal static IReadOnlyList<FactGroup> BuildGroups(SystemInformation info, IReadOnlyList<VolumeInfo> volumes, IReadOnlyList<PhysicalDiskInfo> disks)
    {
        var os = info.OperatingSystem;
        var uptime = os.LastBoot is { } boot ? DateTimeOffset.Now - boot : (TimeSpan?)null;

        var groups = new List<FactGroup>
        {
            new("Computador", UI.Glyphs.Laptop,
            [
                new("Nome", info.Computer.MachineName),
                new("Fabricante", info.Computer.Manufacturer ?? NotReported),
                new("Modelo", info.Computer.Model ?? NotReported),
            ]),
            new("Windows", UI.Glyphs.Home,
            [
                new("Edição", os.Caption?.Replace("Microsoft ", string.Empty, StringComparison.Ordinal) ?? NotReported),
                new("Versão", os.DisplayVersion ?? NotReported),
                new("Build", $"{os.Build} ({os.Architecture})"),
                new("Instalado em", HealthDisplay.Date(os.InstalledOn)),
                new("Ligado há", uptime is { } span ? HealthDisplay.Duration(span) : NotReported),
            ]),
            new("Processador", UI.Glyphs.Diagnostic,
            [
                new("Modelo", info.Processor?.Name ?? NotReported),
                new("Núcleos", info.Processor?.Cores?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? NotReported),
                new("Threads (tarefas ao mesmo tempo)", info.Processor?.LogicalProcessors.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? NotReported),
                new("Velocidade base", info.Processor?.MaxClockMHz is { } mhz ? $"{HealthDisplay.Number(mhz / 1000.0, "0.0#")} GHz" : NotReported),
            ]),
            new("Memória RAM", UI.Glyphs.Memory,
            [
                new("Instalada", info.Memory.InstalledBytes is { } installed ? ByteSize.Format(installed) : NotReported),
                new("Utilizável pelo Windows", ByteSize.Format(info.Memory.UsableBytes)),
                new("Pentes de memória", info.Memory.ModuleCount?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? NotReported),
                new("Velocidade", info.Memory.SpeedMHz is { } speed ? $"{speed} MHz" : NotReported),
            ]),
        };

        groups.Add(new FactGroup("Placa de vídeo", UI.Glyphs.Chart,
            info.Graphics.Count == 0
                ? [new("Placa", NotReported)]
                : info.Graphics.Select(g => new FactItem(g.Name, g.DriverVersion is { } driver ? $"Driver {driver}" : "Driver não informado")).ToList()));

        var storage = disks.Select(d => new FactItem(d.Model, $"{DiskText.MediaName(d.Media)} · {ByteSize.Format(d.SizeBytes)}{(d.BusType is { } bus ? $" · {bus}" : string.Empty)}"))
            .Concat(volumes.Select(v => new FactItem($"Unidade {v.Name}", $"{ByteSize.Format(v.FreeBytes)} livres de {ByteSize.Format(v.TotalBytes)} ({v.FileSystem})")))
            .ToList();
        groups.Add(new FactGroup("Armazenamento", UI.Glyphs.HardDrive, storage.Count > 0 ? storage : [new("Discos", NotReported)]));

        return groups;
    }
}
