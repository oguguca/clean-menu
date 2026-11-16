using System.Globalization;
using System.Text;
using CleanMenu.Core.Disks;
using CleanMenu.Core.Formatting;

namespace CleanMenu.Core.SystemInfo;

/// <summary>
/// Plain-text report the user can copy and send to someone who is helping them.
/// Contains no personal files, only hardware and Windows facts.
/// </summary>
public static class SystemReport
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Build(SystemInformation info, IReadOnlyList<VolumeInfo> volumes, IReadOnlyList<PhysicalDiskInfo> disks)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(volumes);
        ArgumentNullException.ThrowIfNull(disks);

        var sb = new StringBuilder();
        sb.AppendLine("Informações do computador (gerado pelo Clean Menu)");
        sb.AppendLine();
        sb.AppendLine(PtBr, $"Computador: {info.Computer.MachineName}");
        Line(sb, "Fabricante", info.Computer.Manufacturer);
        Line(sb, "Modelo", info.Computer.Model);
        sb.AppendLine();
        Line(sb, "Windows", info.OperatingSystem.Caption);
        Line(sb, "Versão", info.OperatingSystem.DisplayVersion);
        sb.AppendLine(PtBr, $"Build: {info.OperatingSystem.Build} ({info.OperatingSystem.Architecture})");
        sb.AppendLine();
        Line(sb, "Processador", info.Processor?.Name);
        if (info.Processor is { } cpu)
        {
            sb.AppendLine(PtBr, $"Núcleos: {cpu.Cores?.ToString(PtBr) ?? "?"} / Threads: {cpu.LogicalProcessors}");
        }

        Line(sb, "Memória instalada", info.Memory.InstalledBytes is { } installed ? ByteSize.Format(installed) : null);
        sb.AppendLine(PtBr, $"Memória utilizável: {ByteSize.Format(info.Memory.UsableBytes)}");
        foreach (var gpu in info.Graphics)
        {
            Line(sb, "Placa de vídeo", gpu.Name);
        }

        sb.AppendLine();
        foreach (var disk in disks)
        {
            sb.AppendLine(PtBr, $"Disco: {disk.Model} — {DiskText.MediaName(disk.Media)} — {ByteSize.Format(disk.SizeBytes)}");
        }

        foreach (var volume in volumes)
        {
            sb.AppendLine(PtBr, $"Unidade {volume.Name}: {ByteSize.Format(volume.FreeBytes)} livres de {ByteSize.Format(volume.TotalBytes)} ({volume.FileSystem})");
        }

        return sb.ToString();
    }

    private static void Line(StringBuilder sb, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            sb.AppendLine(PtBr, $"{label}: {value}");
        }
    }
}
