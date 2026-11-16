using CleanMenu.Core.Maintenance;
using CleanMenu.Core.Platform;
using Microsoft.Win32;

namespace CleanMenu.Infrastructure.Maintenance;

/// <summary>Reads privacy and background settings from the registry. Read-only.</summary>
public sealed class WindowsSettingsReader : IWindowsSettingsReader
{
    public IReadOnlyList<WindowsSettingInfo> Read(WindowsVersion version)
    {
        var diagnostic = WindowsSettingText.DiagnosticData(
            ReadInt(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry"),
            ReadInt(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry"));

        var activity = WindowsSettingText.OnOff(
            ReadInt(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities"), defaultOn: true);

        var settings = new List<WindowsSettingInfo>
        {
            new("Dados de diagnóstico",
                "Informações que o Windows envia à Microsoft sobre o funcionamento do computador.",
                diagnostic, "ms-settings:privacy-feedback", OsScope.All),
            new("Histórico de atividades",
                "Lista dos programas e arquivos que você usou, guardada pelo Windows.",
                activity, "ms-settings:privacy-activityhistory", OsScope.All),
        };

        if (version == WindowsVersion.Windows10)
        {
            var disabled = ReadInt(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled");
            settings.Add(new("Aplicativos em segundo plano",
                "Permite que aplicativos da Loja continuem rodando mesmo fechados. Desligar economiza memória e bateria.",
                disabled == 1 ? "Desligado" : "Ligado", "ms-settings:privacy-backgroundapps", OsScope.Windows10));
        }
        else
        {
            settings.Add(new("Aplicativos em segundo plano",
                "No Windows 11, cada aplicativo da Loja tem sua própria opção em Aplicativos > Aplicativos instalados > Opções avançadas.",
                "Definido por aplicativo", "ms-settings:appsfeatures", OsScope.Windows11));
        }

        return settings;
    }

    private static int? ReadInt(RegistryKey hive, string path, string name)
    {
        try
        {
            using var key = hive.OpenSubKey(path);
            return key?.GetValue(name) is int value ? value : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
