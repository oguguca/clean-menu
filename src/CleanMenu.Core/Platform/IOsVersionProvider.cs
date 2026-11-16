namespace CleanMenu.Core.Platform;

/// <summary>
/// Supplies the version of the running operating system.
/// </summary>
public interface IOsVersionProvider
{
    Version GetOsVersion();
}
