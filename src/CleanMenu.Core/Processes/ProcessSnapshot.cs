namespace CleanMenu.Core.Processes;

/// <summary>Raw information about a running process at one moment.</summary>
/// <param name="Pid">Process id.</param>
/// <param name="Name">Executable name without extension (e.g. "chrome").</param>
/// <param name="MemoryBytes">Working set: physical memory currently used by the process.</param>
/// <param name="CpuTime">Total processor time used so far; null when Windows denies access.</param>
/// <param name="HasWindow">True when the process shows a window (an app the user sees).</param>
/// <param name="WindowTitle">Title of the main window, when there is one.</param>
public sealed record ProcessSnapshot(
    int Pid,
    string Name,
    long MemoryBytes,
    TimeSpan? CpuTime,
    bool HasWindow,
    string? WindowTitle);

/// <summary>Captures the list of running processes.</summary>
public interface IProcessSource
{
    IReadOnlyList<ProcessSnapshot> Capture();
}
