using System.Diagnostics;
using System.Windows;

namespace CleanMenu.App.Services;

/// <summary>Harmless actions that hand control to Windows (open a tool, copy text).</summary>
public interface ISystemActions
{
    /// <summary>Opens a Windows tool such as "taskmgr.exe". Returns false when it could not start.</summary>
    bool OpenTool(string fileName, string? arguments = null);

    void CopyToClipboard(string text);
}

public sealed class SystemActions : ISystemActions
{
    public bool OpenTool(string fileName, string? arguments = null)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments ?? string.Empty) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or System.IO.FileNotFoundException)
        {
            // Includes the user cancelling a UAC prompt.
            return false;
        }
    }

    public void CopyToClipboard(string text) => Clipboard.SetText(text);
}
