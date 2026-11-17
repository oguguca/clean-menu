using CleanMenu.Infrastructure.Advanced;

namespace CleanMenu.Infrastructure.Tests.Advanced;

public class WindowsRecentFilesTests
{
    [Fact]
    public void ReadsRecentItems_WithoutChangingAnything()
    {
        var recent = new WindowsRecentFiles().GetRecentlyOpened();

        Assert.All(recent, path => Assert.True(Path.IsPathRooted(path), path));
    }
}
