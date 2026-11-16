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
}
