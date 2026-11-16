using System.Collections.ObjectModel;
using CleanMenu.App.Navigation;
using CleanMenu.App.Services;
using CleanMenu.Core.Disks;
using CleanMenu.Core.Formatting;
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

    public DisksViewModel(IVolumeProvider volumes, IPhysicalDiskProvider disks, IUiDispatcher dispatcher)
        : base(PageCatalog.Get(AppPage.Disks))
    {
        _volumes = volumes ?? throw new ArgumentNullException(nameof(volumes));
        _disks = disks ?? throw new ArgumentNullException(nameof(disks));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _ = RefreshAsync();
    }

    public ObservableCollection<VolumeRowViewModel> Volumes { get; } = [];

    public ObservableCollection<PhysicalDiskRowViewModel> PhysicalDisks { get; } = [];

    public IReadOnlyList<string> ComingSoon { get; } =
    [
        "Otimizar cada disco do jeito certo: desfragmentação só para HD e TRIM para SSD",
        "Verificar erros no disco",
        "Ver temperatura e desgaste, quando o disco informar",
        "Encontrar as pastas que mais ocupam espaço",
    ];

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        var volumes = _volumes.GetVolumes();
        Volumes.Clear();
        foreach (var volume in volumes)
        {
            Volumes.Add(new VolumeRowViewModel(volume));
        }

        IReadOnlyList<PhysicalDiskInfo> disks = [];
        try
        {
            disks = await _disks.GetDisksAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Physical disk list is optional; volumes are still shown.
        }

        _dispatcher.Post(() =>
        {
            PhysicalDisks.Clear();
            foreach (var disk in disks)
            {
                PhysicalDisks.Add(new PhysicalDiskRowViewModel(disk));
            }

            IsLoading = false;
        });
    }
}
