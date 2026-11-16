using CleanMenu.Core.Disks;
using CleanMenu.Core.Formatting;
using CleanMenu.Core.Monitoring;
using CleanMenu.UI;

namespace CleanMenu.App.ViewModels;

/// <summary>A drive letter as shown to the user.</summary>
public sealed class VolumeRowViewModel(VolumeInfo volume)
{
    public VolumeInfo Volume { get; } = volume ?? throw new ArgumentNullException(nameof(volume));

    public string Title => Volume.IsSystem
        ? $"Disco do Windows ({Volume.Name})"
        : Volume.IsRemovable
            ? $"Unidade removível ({Volume.Name})"
            : $"{Volume.Label ?? "Disco local"} ({Volume.Name})";

    public string FreeText => $"{ByteSize.Format(Volume.FreeBytes)} livres";

    public string DetailText => $"de {ByteSize.Format(Volume.TotalBytes)} · {HealthDisplay.Percent(Volume.UsedPercent)} usado · {Volume.FileSystem}";

    public double UsedPercent => Volume.UsedPercent;

    public StatusLevel Status => HealthDisplay.ToStatus(Volume.SpaceHealth);

    public string Advice => Volume.SpaceHealth switch
    {
        HealthLevel.Critical => "Quase sem espaço. O computador pode ficar lento ou falhar ao atualizar.",
        HealthLevel.Attention => "Pouco espaço livre. Vale a pena fazer uma limpeza.",
        HealthLevel.Good => "Espaço suficiente.",
        _ => string.Empty,
    };

    /// <summary>Resource key of the bar color.</summary>
    public string BarBrushKey => Status.BrushKey();

    /// <summary>Type of the disk behind this drive letter, when known.</summary>
    public DiskMedia Media { get; init; } = DiskMedia.Unknown;

    /// <summary>Drive letter without colon ("C").</summary>
    public string Letter => Volume.Name.TrimEnd(':');

    public string OptimizeExplanation => Media switch
    {
        DiskMedia.Ssd => "Este disco é SSD: o Windows usa o TRIM, que mantém o SSD rápido. SSD nunca é desfragmentado.",
        DiskMedia.Hdd => "Este disco é HD: o Windows desfragmenta, juntando partes espalhadas dos arquivos.",
        _ => "O Windows identifica o tipo do disco e escolhe a otimização certa (TRIM para SSD, desfragmentação para HD).",
    };
}
