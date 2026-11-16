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
        AppPage.SmartCleanup => services.GetRequiredService<SmartCleanupViewModel>(),
        AppPage.Memory => services.GetRequiredService<MemoryViewModel>(),
        AppPage.Startup => services.GetRequiredService<StartupViewModel>(),
        AppPage.Optimization => services.GetRequiredService<OptimizationViewModel>(),
        AppPage.Processes => services.GetRequiredService<ProcessesViewModel>(),
        AppPage.SystemInfo => services.GetRequiredService<SystemInfoViewModel>(),
        AppPage.Disks => services.GetRequiredService<DisksViewModel>(),
        AppPage.History => services.GetRequiredService<HistoryViewModel>(),
        AppPage.WindowsTools => services.GetRequiredService<WindowsToolsViewModel>(),
        AppPage.Settings => services.GetRequiredService<SettingsViewModel>(),
        _ => new SectionPlaceholderViewModel(PageCatalog.Get(page)),
    };
}
