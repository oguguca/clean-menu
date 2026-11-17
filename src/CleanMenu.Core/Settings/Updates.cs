namespace CleanMenu.Core.Settings;

/// <summary>Outcome of checking the project's GitHub page for a newer version.</summary>
public enum UpdateStatus
{
    UpToDate,
    NewVersion,
    NoReleasesYet,
    Failed,
}

public sealed record UpdateCheckResult(UpdateStatus Status, string? LatestVersion);

/// <summary>Checks for new versions. Only reads public release information; never downloads or installs anything.</summary>
public interface IUpdateChecker
{
    /// <summary>Public page with all versions.</summary>
    Uri ReleasesPage { get; }

    Task<UpdateCheckResult> CheckAsync(string currentVersion, CancellationToken cancellationToken = default);
}

public static class VersionText
{
    /// <summary>Parses "v1.2.3" or "1.2" into a version; null when not a version.</summary>
    public static Version? Parse(string? text)
    {
        var trimmed = text?.Trim().TrimStart('v', 'V');
        var dash = trimmed?.IndexOf('-', StringComparison.Ordinal) ?? -1;
        if (dash > 0)
        {
            trimmed = trimmed![..dash];
        }

        return Version.TryParse(trimmed, out var version) ? version : null;
    }

    /// <summary>True when <paramref name="latest"/> is a higher version than <paramref name="current"/>.</summary>
    public static bool IsNewer(string? latest, string? current) =>
        Parse(latest) is { } l && Parse(current) is { } c && Normalize(l) > Normalize(c);

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
}
