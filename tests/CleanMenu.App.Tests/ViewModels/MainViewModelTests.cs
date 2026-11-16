using CleanMenu.App.Navigation;
using CleanMenu.App.ViewModels;
using CleanMenu.Core.Platform;

namespace CleanMenu.App.Tests.ViewModels;

public class MainViewModelTests
{
    private sealed class CountingFactory : IPageFactory
    {
        public int Created { get; private set; }

        public PageViewModel Create(AppPage page)
        {
            Created++;
            return new SectionPlaceholderViewModel(PageCatalog.Get(page));
        }
    }

    private static readonly OsProfile Windows11 = new(WindowsVersion.Windows11, null);

    [Fact]
    public void Starts_OnDashboard()
    {
        var vm = new MainViewModel(new CountingFactory(), Windows11);

        Assert.Equal(AppPage.Dashboard, vm.SelectedItem?.Page);
        Assert.Equal(AppPage.Dashboard, vm.CurrentPage?.Info.Page);
    }

    [Fact]
    public void Lists_AllSections_InCatalogOrder()
    {
        var vm = new MainViewModel(new CountingFactory(), Windows11);

        Assert.Equal(PageCatalog.All.Select(p => p.Page), vm.Items.Select(i => i.Page));
    }

    [Fact]
    public void SelectingItem_ShowsItsPage()
    {
        var vm = new MainViewModel(new CountingFactory(), Windows11);

        vm.SelectedItem = vm.Items.Single(i => i.Page == AppPage.Disks);

        Assert.Equal(AppPage.Disks, vm.CurrentPage?.Info.Page);
    }

    [Fact]
    public void ReturningToPage_ReusesSameInstance()
    {
        var factory = new CountingFactory();
        var vm = new MainViewModel(factory, Windows11);
        var dashboard = vm.CurrentPage;

        vm.SelectedItem = vm.Items.Single(i => i.Page == AppPage.Memory);
        vm.SelectedItem = vm.Items.Single(i => i.Page == AppPage.Dashboard);

        Assert.Same(dashboard, vm.CurrentPage);
        Assert.Equal(2, factory.Created);
    }

    [Fact]
    public void NavigateToCommand_SelectsSection()
    {
        var vm = new MainViewModel(new CountingFactory(), Windows11);

        vm.NavigateToCommand.Execute(AppPage.Settings);

        Assert.Equal(AppPage.Settings, vm.SelectedItem?.Page);
        Assert.Equal(AppPage.Settings, vm.CurrentPage?.Info.Page);
    }

    [Fact]
    public void ShowsDetectedWindows()
    {
        var vm = new MainViewModel(new CountingFactory(), new OsProfile(WindowsVersion.Windows10, null));

        Assert.Equal("Windows 10", vm.OsName);
        Assert.Equal("Detectado automaticamente", vm.OsOrigin);
    }
}
