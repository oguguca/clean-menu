using CleanMenu.Core.Platform;

namespace CleanMenu.App.ViewModels;

/// <summary>Plain-language labels for the Windows version Clean Menu is adapted to.</summary>
public static class OsDisplay
{
    public static string Name(WindowsVersion? version) => version switch
    {
        WindowsVersion.Windows10 => "Windows 10",
        WindowsVersion.Windows11 => "Windows 11",
        _ => "Windows não reconhecido",
    };

    public static string Origin(OsProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.UserSelected is not null)
        {
            return "Escolhido por você";
        }

        return profile.Detected is not null ? "Detectado automaticamente" : "Escolha nas Configurações";
    }
}
