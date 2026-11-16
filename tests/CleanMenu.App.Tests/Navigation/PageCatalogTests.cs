using CleanMenu.App.Navigation;

namespace CleanMenu.App.Tests.Navigation;

public class PageCatalogTests
{
    [Fact]
    public void All_ContainsEverySectionExactlyOnce()
    {
        var pages = PageCatalog.All.Select(p => p.Page).ToList();

        Assert.Equal(Enum.GetValues<AppPage>().Length, pages.Count);
        Assert.Equal(pages.Count, pages.Distinct().Count());
    }

    [Fact]
    public void Dashboard_IsFirst()
    {
        Assert.Equal(AppPage.Dashboard, PageCatalog.All[0].Page);
    }

    [Fact]
    public void EveryPage_HasPlainLanguageTexts()
    {
        Assert.All(PageCatalog.All, page =>
        {
            Assert.False(string.IsNullOrWhiteSpace(page.Title));
            Assert.False(string.IsNullOrWhiteSpace(page.Description));
            Assert.False(string.IsNullOrWhiteSpace(page.Glyph));
            Assert.False(string.IsNullOrWhiteSpace(page.Group));
            Assert.EndsWith(".", page.Description, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void EveryUnbuiltPage_ExplainsWhatItWillDo()
    {
        Assert.All(PageCatalog.All.Where(p => p.Page != AppPage.Dashboard),
            page => Assert.NotEmpty(page.PlannedFeatures));
    }

    [Fact]
    public void Get_ReturnsMatchingPage()
    {
        Assert.Equal("Memória RAM", PageCatalog.Get(AppPage.Memory).Title);
    }

    [Fact]
    public void AdvancedAreas_AreFlagged()
    {
        var advanced = PageCatalog.All.Where(p => p.IsAdvanced).Select(p => p.Page).ToHashSet();

        Assert.Contains(AppPage.AdvancedCleanup, advanced);
        Assert.Contains(AppPage.Processes, advanced);
        Assert.DoesNotContain(AppPage.Dashboard, advanced);
        Assert.DoesNotContain(AppPage.SmartCleanup, advanced);
    }
}
