using CleanMenu.Core.Platform;

namespace CleanMenu.Core.Tests.Platform;

public class OsProfileTests
{
    private sealed class FakeProvider(Version version) : IOsVersionProvider
    {
        public Version GetOsVersion() => version;
    }

    [Fact]
    public void Detect_Windows11Build_EffectiveIsWindows11()
    {
        var profile = OsProfile.Detect(new FakeProvider(new Version(10, 0, 26100)));

        Assert.Equal(WindowsVersion.Windows11, profile.Detected);
        Assert.Equal(WindowsVersion.Windows11, profile.Effective);
        Assert.False(profile.SelectionDiffersFromDetection);
    }

    [Fact]
    public void UserSelection_OverridesDetection()
    {
        var profile = OsProfile.Detect(new FakeProvider(new Version(10, 0, 26100)), WindowsVersion.Windows10);

        Assert.Equal(WindowsVersion.Windows10, profile.Effective);
        Assert.True(profile.SelectionDiffersFromDetection);
    }

    [Fact]
    public void UnsupportedOsWithoutSelection_EffectiveIsNull_AndHidesOsSpecificItems()
    {
        var profile = OsProfile.Detect(new FakeProvider(new Version(6, 1, 7601)));

        Assert.Null(profile.Effective);
        Assert.False(profile.Shows(OsScope.All));
        Assert.False(profile.SelectionDiffersFromDetection);
    }

    [Theory]
    [InlineData(WindowsVersion.Windows10, OsScope.Windows10, true)]
    [InlineData(WindowsVersion.Windows10, OsScope.Windows11, false)]
    [InlineData(WindowsVersion.Windows10, OsScope.All, true)]
    [InlineData(WindowsVersion.Windows11, OsScope.Windows10, false)]
    [InlineData(WindowsVersion.Windows11, OsScope.Windows11, true)]
    [InlineData(WindowsVersion.Windows11, OsScope.All, true)]
    [InlineData(WindowsVersion.Windows11, OsScope.None, false)]
    public void Shows_RespectsScope(WindowsVersion selected, OsScope scope, bool expected)
    {
        var profile = new OsProfile(Detected: null, UserSelected: selected);

        Assert.Equal(expected, profile.Shows(scope));
    }
}
