using CleanMenu.Core.Settings;
using Microsoft.Win32;

namespace CleanMenu.Infrastructure.Settings;

/// <summary>Adds or removes Clean Menu from HKCU\...\Run (this user only, no admin rights).</summary>
public sealed class RegistryAutoStartService(string executablePath, string valueName = "CleanMenu") : IAutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(valueName) is string value && value.Contains(executablePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
        {
            key.SetValue(valueName, $"\"{executablePath}\"", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }
}
