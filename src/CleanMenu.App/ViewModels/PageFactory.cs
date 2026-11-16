using CleanMenu.App.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace CleanMenu.App.ViewModels;

/// <summary>
/// Resolves implemented pages from the DI container; sections not built yet get an honest placeholder.
/// </summary>
public sealed class PageFactory(IServiceProvider services) : IPageFactory
{
    public PageViewModel Create(AppPage page) => page switch
    {
        AppPage.Dashboard => services.GetRequiredService<DashboardViewModel>(),
        _ => new SectionPlaceholderViewModel(PageCatalog.Get(page)),
    };
}
