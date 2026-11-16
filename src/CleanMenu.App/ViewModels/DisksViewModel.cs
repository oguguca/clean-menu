using System.Collections.ObjectModel;
using CleanMenu.App.Navigation;
using CleanMenu.App.Services;
using CleanMenu.Core.Disks;
using CleanMenu.Core.Formatting;
using CleanMenu.Core.History;
using CleanMenu.Core.Maintenance;
using CleanMenu.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CleanMenu.App.ViewModels;

/// <summary>A physical disk as shown to the user.</summary>
public sealed class PhysicalDiskRowViewModel(PhysicalDiskInfo disk)
{
    public PhysicalDiskInfo Disk { get; } = disk ?? throw new ArgumentNullException(nameof(disk));

    public string Title => Disk.Model;

    public string TypeText => DiskText.MediaName(Disk.Media);

    public string Details => $"{ByteSize.Format(Disk.SizeBytes)}{(Disk.BusType is { } bus ? $" · conexão {bus}" : string.Empty)}";

    public string HealthText => DiskText.HealthText(Disk.Health);

    public StatusLevel Status => Disk.Health switch
    {
        ReportedDiskHealth.Warning => StatusLevel.Warning,
        ReportedDiskHealth.Unhealthy => StatusLevel.Critical,
        // "No problems reported" is not proof of good health, so it stays neutral.
        _ => StatusLevel.Neutral,
    };

    public string TypeExplanation => Disk.Media switch
    {
        DiskMedia.Ssd => "SSD é rápido e não tem peças móveis. Não precisa de desfragmentação.",
        DiskMedia.Hdd => "HD tem um disco que gira. Pode ficar mais lento com o tempo e se beneficia de desfragmentação.",
        _ => "O Windows não informou se este disco é HD ou SSD.",
    };
}

/// <summary>Discos page: space per drive letter and the physical disks with what Windows reports.</summary>
public sealed partial class DisksViewModel : PageViewModel
{
    private readonly IVolumeProvider _volumes;
    private readonly IPhysicalDiskProvider _disks;
    private readonly IUiDispatcher _dispatcher;

    [ObservableProperty]
    private bool _isLoading = true;

    private readonly IMaintenanceRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly IOperationHistory _history;

    [ObservableProperty]
    private string? _message;

    public DisksViewModel(
        IVolumeProvider volumes,
        IPhysicalDiskProvider disks,
        IUiDispatcher dispatcher,
        IMaintenanceRunner runner,
        IDialogService dialogs,
        IOperationHistory history)
        : base(PageCatalog.Get(AppPage.Disks))
    {
        _volumes = volumes ?? throw new ArgumentNullException(nameof(volumes));
        _disks = disks ?? throw new ArgumentNullException(nameof(disks));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _ = RefreshAsync();
    }

    public ObservableCollection<VolumeRowViewModel> Volumes { get; } = [];

    public ObservableCollection<PhysicalDiskRowViewModel> PhysicalDisks { get; } = [];

    [RelayCommand]
    internal async Task RefreshAsync()
    {
        IsLoading = true;
        IReadOnlyList<PhysicalDiskInfo> disks = [];
        IReadOnlyDictionary<string, DiskMedia> media = new Dictionary<string, DiskMedia>();
        try
        {
            disks = await _disks.GetDisksAsync().ConfigureAwait(false);
            media = await _disks.GetVolumeMediaAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Physical disk information is optional; volumes are still shown.
        }

        var volumes = _volumes.GetVolumes();
        _dispatcher.Post(() =>
        {
            Volumes.Clear();
            foreach (var volume in volumes)
            {
                Volumes.Add(new VolumeRowViewModel(volume)
                {
                    Media = media.TryGetValue(volume.Name.TrimEnd(':'), out var type) ? type : DiskMedia.Unknown,
                });
            }

            PhysicalDisks.Clear();
            foreach (var disk in disks)
            {
                PhysicalDisks.Add(new PhysicalDiskRowViewModel(disk));
            }

            IsLoading = false;
        });
    }

    [RelayCommand]
    private Task OptimizeAsync(VolumeRowViewModel? volume) =>
        RunAsync(volume, MaintenanceTool.OptimizeDrive, volume?.OptimizeExplanation);

    [RelayCommand]
    private Task AnalyzeAsync(VolumeRowViewModel? volume) => RunAsync(volume, MaintenanceTool.AnalyzeDrive, null);

    [RelayCommand]
    private Task CheckAsync(VolumeRowViewModel? volume) => RunAsync(volume, MaintenanceTool.CheckDiskScan, null);

    private async Task RunAsync(VolumeRowViewModel? volume, MaintenanceTool tool, string? extra)
    {
        if (volume is null)
        {
            return;
        }

        var command = MaintenanceCommands.Build(tool, volume.Letter);
        var details = new List<string> { $"Comando que será executado: {command.DisplayCommand}", command.Duration };
        if (extra is not null)
        {
            details.Insert(0, extra);
        }

        details.Add("O Windows vai pedir permissão de administrador só para este comando.");

        if (!_dialogs.Confirm(new ConfirmationRequest($"{command.Title}?", command.Explanation, details, "Executar", IsDestructive: false)))
        {
            return;
        }

        var started = _runner.StartVisibleElevated(command);
        Message = started
            ? $"\"{command.Title}\" foi iniciado numa janela própria. O resultado aparece lá."
            : "O comando não foi iniciado (a permissão de administrador foi negada ou o Windows recusou).";
        await _history.RecordActionAsync("Manutenção de disco", command.Title, started, started ? null : "Não iniciado").ConfigureAwait(true);
    }
}
