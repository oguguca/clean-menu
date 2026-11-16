namespace CleanMenu.Core.Processes;

/// <summary>
/// Resource use of one program, adding up all its processes. Browsers and editors run many
/// processes with the same name; users think of them as one program.
/// </summary>
public sealed record ProgramUsage(string Name, int ProcessCount, long MemoryBytes, double CpuPercent, ProcessKind Kind, int MainPid)
{
    public string Advice => ProcessClassifier.Advice(Kind);

    public bool IsProtected => ProcessClassifier.IsProtected(Name, MainPid);

    public static IReadOnlyList<ProgramUsage> Group(IEnumerable<ProcessUsage> processes)
    {
        ArgumentNullException.ThrowIfNull(processes);

        return processes
            .Where(p => p.Pid != 0)
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var largest = g.MaxBy(p => p.MemoryBytes)!;
                var cpuValues = g.Select(p => p.CpuPercent).Where(c => !double.IsNaN(c)).ToList();

                // A program with any window counts as opened by the user, unless it belongs to Windows.
                var kind = g.Select(p => p.Kind).Contains(ProcessKind.UserApp) && largest.Kind is ProcessKind.Background or ProcessKind.UserApp
                    ? ProcessKind.UserApp
                    : largest.Kind;

                return new ProgramUsage(
                    largest.Name,
                    g.Count(),
                    g.Sum(p => p.MemoryBytes),
                    cpuValues.Count == 0 ? double.NaN : Math.Min(100, cpuValues.Sum()),
                    kind,
                    largest.Pid);
            })
            .OrderByDescending(p => p.MemoryBytes)
            .ToList();
    }
}
