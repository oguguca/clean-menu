using CleanMenu.Core.Platform;
using CleanMenu.Infrastructure.Platform;

namespace CleanMenu.Infrastructure.Tests.Platform;

/// <summary>
/// Read-only integration test against the real OS. Does not modify the machine.
/// </summary>
public class WindowsOsVersionProviderTests
{
    [Fact]
    public void GetOsVersion_OnSupportedMachine_ClassifiesAsWindows10Or11()
    {
        var version = new WindowsOsVersionProvider().GetOsVersion();

        Assert.NotNull(WindowsVersionClassifier.Classify(version));
    }
}
