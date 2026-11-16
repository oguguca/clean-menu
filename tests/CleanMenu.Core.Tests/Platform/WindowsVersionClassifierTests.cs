using CleanMenu.Core.Platform;

namespace CleanMenu.Core.Tests.Platform;

public class WindowsVersionClassifierTests
{
    [Theory]
    [InlineData(10, 0, 17763, WindowsVersion.Windows10)] // 1809, oldest supported
    [InlineData(10, 0, 19045, WindowsVersion.Windows10)] // 22H2
    [InlineData(10, 0, 21999, WindowsVersion.Windows10)]
    [InlineData(10, 0, 22000, WindowsVersion.Windows11)] // 21H2
    [InlineData(10, 0, 26100, WindowsVersion.Windows11)] // 24H2
    [InlineData(10, 0, 26200, WindowsVersion.Windows11)]
    public void Classify_SupportedBuild_ReturnsVersion(int major, int minor, int build, WindowsVersion expected)
    {
        Assert.Equal(expected, WindowsVersionClassifier.Classify(new Version(major, minor, build)));
    }

    [Theory]
    [InlineData(10, 0, 17134)] // 1803, below minimum
    [InlineData(6, 3, 9600)]   // Windows 8.1
    [InlineData(6, 1, 7601)]   // Windows 7
    [InlineData(11, 0, 22000)] // hypothetical future major version
    public void Classify_UnsupportedVersion_ReturnsNull(int major, int minor, int build)
    {
        Assert.Null(WindowsVersionClassifier.Classify(new Version(major, minor, build)));
    }

    [Fact]
    public void Classify_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => WindowsVersionClassifier.Classify(null!));
    }
}
