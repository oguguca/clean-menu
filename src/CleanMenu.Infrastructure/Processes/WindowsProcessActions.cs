using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CleanMenu.Core.Processes;

namespace CleanMenu.Infrastructure.Processes;

/// <summary>Process actions. Ending a process always re-checks protection and identity first.</summary>
public sealed partial class WindowsProcessActions : IProcessActions
{
    private const uint ShopFilePath = 0x2;

    public string? GetExecutablePath(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.MainModule?.FileName;
        }
        catch (Exception ex) when (ex is ArgumentException or Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    public ProcessEndResult EndProcess(int pid, string expectedName)
    {
        ArgumentNullException.ThrowIfNull(expectedName);
        if (ProcessClassifier.IsProtected(expectedName, pid))
        {
            return ProcessEndResult.Protected;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            if (!process.ProcessName.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return ProcessEndResult.ChangedIdentity;
            }

            if (ProcessClassifier.IsProtected(process.ProcessName, process.Id))
            {
                return ProcessEndResult.Protected;
            }

            process.Kill(entireProcessTree: false);
            return process.WaitForExit(3000) ? ProcessEndResult.Ended : ProcessEndResult.StillRunning;
        }
        catch (ArgumentException)
        {
            return ProcessEndResult.NotFound;
        }
        catch (Win32Exception)
        {
            return ProcessEndResult.AccessDenied;
        }
        catch (InvalidOperationException)
        {
            return ProcessEndResult.NotFound;
        }
    }

    public bool OpenFileLocation(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    public bool ShowFileProperties(string path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path) && SHObjectProperties(IntPtr.Zero, ShopFilePath, path, null);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SHObjectProperties(IntPtr hwnd, uint shopObjectType, string objectName, string? propertyPage);
}
