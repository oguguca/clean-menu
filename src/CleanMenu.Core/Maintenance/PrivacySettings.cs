using CleanMenu.Core.Platform;

namespace CleanMenu.Core.Maintenance;

/// <summary>
/// A Windows setting Clean Menu reads and explains. Changes are made in Windows Settings itself
/// (opened by the app), never by editing the registry behind the user's back.
/// </summary>
/// <param name="Title">Plain name.</param>
/// <param name="Explanation">What the setting does.</param>
/// <param name="CurrentValue">Current state in plain words, or "Não identificado".</param>
/// <param name="SettingsUri">ms-settings page where the user can change it.</param>
/// <param name="Os">Windows versions where it exists.</param>
public sealed record WindowsSettingInfo(string Title, string Explanation, string CurrentValue, string SettingsUri, OsScope Os);

/// <summary>Reads privacy and background settings (read-only).</summary>
public interface IWindowsSettingsReader
{
    IReadOnlyList<WindowsSettingInfo> Read(WindowsVersion version);
}

/// <summary>Plain-language mapping of known registry values.</summary>
public static class WindowsSettingText
{
    /// <summary>AllowTelemetry: 0 security, 1 required/basic, 2 enhanced, 3 optional/full.</summary>
    public static string DiagnosticData(int? policyValue, int? userValue) => (policyValue ?? userValue) switch
    {
        0 => "Mínimo (definido pela empresa)",
        1 => "Somente dados obrigatórios",
        2 => "Dados aprimorados",
        3 => "Dados opcionais ativados",
        _ => "Padrão do Windows",
    };

    public static string OnOff(int? value, bool defaultOn = true) => value switch
    {
        0 => "Desligado",
        1 => "Ligado",
        _ => defaultOn ? "Ligado (padrão do Windows)" : "Desligado (padrão do Windows)",
    };
}
