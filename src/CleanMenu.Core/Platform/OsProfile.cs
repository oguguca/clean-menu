namespace CleanMenu.Core.Platform;

/// <summary>
/// The Windows version Clean Menu adapts to: the one the user chose in Settings or, when no
/// choice was made, the detected one.
/// </summary>
/// <param name="Detected">Version detected from the OS build number; <c>null</c> when unsupported.</param>
/// <param name="UserSelected">Version explicitly chosen by the user; <c>null</c> when not chosen yet.</param>
public sealed record OsProfile(WindowsVersion? Detected, WindowsVersion? UserSelected)
{
    /// <summary>
    /// Version used to decide which features are shown. <c>null</c> means the user must choose.
    /// </summary>
    public WindowsVersion? Effective => UserSelected ?? Detected;

    /// <summary>
    /// True when the user choice differs from what was detected, so the UI can warn that some
    /// features may not work on this machine.
    /// </summary>
    public bool SelectionDiffersFromDetection =>
        UserSelected is not null && Detected is not null && UserSelected != Detected;

    public static OsProfile Detect(IOsVersionProvider provider, WindowsVersion? userSelected = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return new OsProfile(WindowsVersionClassifier.Classify(provider.GetOsVersion()), userSelected);
    }

    /// <summary>
    /// Returns true when an item declared with <paramref name="scope"/> must be shown.
    /// Nothing OS-specific is shown until a version is known.
    /// </summary>
    public bool Shows(OsScope scope) => Effective is { } version && scope.AppliesTo(version);
}
