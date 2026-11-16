using CleanMenu.Core.Platform;

namespace CleanMenu.Core.Settings;

public enum ThemePreference
{
    Dark,
    Light,
}

public enum OsPreference
{
    Automatic,
    Windows10,
    Windows11,
}

/// <summary>User preferences. Every property has a safe default.</summary>
public sealed record AppSettings
{
    public static readonly int[] AllowedRefreshSeconds = [1, 2, 5, 10];

    public ThemePreference Theme { get; init; } = ThemePreference.Dark;

    public OsPreference Os { get; init; } = OsPreference.Automatic;

    /// <summary>How often live indicators refresh. Only values in <see cref="AllowedRefreshSeconds"/> are used.</summary>
    public int RefreshSeconds { get; init; } = 2;

    /// <summary>Runs the read-only computer analysis when the app opens.</summary>
    public bool AnalyzeOnStartup { get; init; }

    /// <summary>Ask before deleting files. Strongly recommended; on by default.</summary>
    public bool ConfirmBeforeCleaning { get; init; } = true;

    /// <summary>Categories checked by default; null means the built-in defaults.</summary>
    public IReadOnlyList<string>? DefaultCleanupCategories { get; init; }

    public WindowsVersion? SelectedWindows => Os switch
    {
        OsPreference.Windows10 => WindowsVersion.Windows10,
        OsPreference.Windows11 => WindowsVersion.Windows11,
        _ => null,
    };

    /// <summary>Fixes values that may come from an edited or damaged file.</summary>
    public AppSettings Sanitize() => this with
    {
        RefreshSeconds = AllowedRefreshSeconds.Contains(RefreshSeconds) ? RefreshSeconds : 2,
        Theme = Enum.IsDefined(Theme) ? Theme : ThemePreference.Dark,
        Os = Enum.IsDefined(Os) ? Os : OsPreference.Automatic,
    };
}

/// <summary>Loads and saves <see cref="AppSettings"/>.</summary>
public interface ISettingsStore
{
    /// <summary>Folder with settings, history and logs (shown to the user).</summary>
    string DataFolder { get; }

    AppSettings Load();

    void Save(AppSettings settings);
}

/// <summary>Starting Clean Menu together with Windows (per user, reversible).</summary>
public interface IAutoStartService
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}
