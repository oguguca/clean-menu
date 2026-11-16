namespace CleanMenu.Core.Platform;

/// <summary>
/// Maps a raw OS version number to a supported <see cref="WindowsVersion"/>.
/// </summary>
/// <remarks>
/// Windows 10 and Windows 11 both report version 10.0. Microsoft distinguishes them by build
/// number: Windows 11 starts at build 22000. Clean Menu supports Windows 10 from 1809 (build 17763).
/// </remarks>
public static class WindowsVersionClassifier
{
    public const int MinimumSupportedBuild = 17763;
    public const int FirstWindows11Build = 22000;

    /// <summary>
    /// Returns the matching Windows version, or <c>null</c> when the version is not supported.
    /// </summary>
    public static WindowsVersion? Classify(Version osVersion)
    {
        ArgumentNullException.ThrowIfNull(osVersion);

        if (osVersion.Major != 10 || osVersion.Minor != 0 || osVersion.Build < MinimumSupportedBuild)
        {
            return null;
        }

        return osVersion.Build >= FirstWindows11Build ? WindowsVersion.Windows11 : WindowsVersion.Windows10;
    }
}
