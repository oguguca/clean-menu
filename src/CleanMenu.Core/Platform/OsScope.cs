namespace CleanMenu.Core.Platform;

/// <summary>
/// Declares which Windows versions a feature, tool, setting or cleanup category applies to.
/// </summary>
[Flags]
public enum OsScope
{
    None = 0,
    Windows10 = 1,
    Windows11 = 2,
    All = Windows10 | Windows11,
}

public static class OsScopeExtensions
{
    /// <summary>
    /// Returns true when an item declared with <paramref name="scope"/> must be shown for <paramref name="version"/>.
    /// </summary>
    public static bool AppliesTo(this OsScope scope, WindowsVersion version) => version switch
    {
        WindowsVersion.Windows10 => scope.HasFlag(OsScope.Windows10),
        WindowsVersion.Windows11 => scope.HasFlag(OsScope.Windows11),
        _ => throw new ArgumentOutOfRangeException(nameof(version), version, "Unknown Windows version."),
    };
}
