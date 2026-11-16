using System.ComponentModel;
using System.Diagnostics;
using CleanMenu.Core.Processes;

namespace CleanMenu.Infrastructure.Processes;

/// <summary>
/// Lists running processes. Read-only: never starts, stops or modifies a process.
/// Processes of other users or protected ones may deny CPU time; that field is then null.
/// </summary>
public sealed class WindowsProcessSource : IProcessSource
{
    public IReadOnlyList<ProcessSnapshot> Capture()
    {
        var processes = Process.GetProcesses();
        var result = new List<ProcessSnapshot>(processes.Length);
        foreach (var process in processes)
        {
            using (process)
            {
                try
                {
                    TimeSpan? cpu = null;
                    try
                    {
                        cpu = process.TotalProcessorTime;
                    }
                    catch (Exception ex) when (ex is Win32Exception or NotSupportedException or InvalidOperationException)
                    {
                        // Access denied (protected/system process). CPU stays unknown.
                    }

                    var hasWindow = process.MainWindowHandle != IntPtr.Zero;
                    result.Add(new ProcessSnapshot(
                        process.Id,
                        process.ProcessName,
                        process.WorkingSet64,
                        cpu,
                        hasWindow,
                        hasWindow && !string.IsNullOrWhiteSpace(process.MainWindowTitle) ? process.MainWindowTitle : null));
                }
                catch (InvalidOperationException)
                {
                    // The process ended while being read.
                }
            }
        }

        return result;
    }
}
