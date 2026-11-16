using CleanMenu.Core.Platform;

namespace CleanMenu.Infrastructure.Platform;

/// <summary>
/// Reads the real OS version. On .NET 5+ <see cref="Environment.OSVersion"/> reports the true
/// version; it is not capped by the application manifest as it was in .NET Framework.
/// </summary>
public sealed class WindowsOsVersionProvider : IOsVersionProvider
{
    public Version GetOsVersion() => Environment.OSVersion.Version;
}
