using CleanMenu.App.Navigation;
using CleanMenu.Core.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CleanMenu.App.ViewModels;

/// <summary>
/// Shell: sidebar items, the selected section and the page shown on the right.
/// Page view models are created once and reused, so a page keeps its state when the user returns.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IPageFactory _pageFactory;
    private readonly Dictionary<AppPage, PageViewModel> _pages = [];

    [ObservableProperty]
    private NavigationItem? _selectedItem;

    [ObservableProperty]
    private PageViewModel? _currentPage;

    public MainViewModel(IPageFactory pageFactory, OsProfile osProfile)
    {
        ArgumentNullException.ThrowIfNull(osProfile);
        _pageFactory = pageFactory ?? throw new ArgumentNullException(nameof(pageFactory));

        Items = PageCatalog.All.Select(info => new NavigationItem(info)).ToList();
        OsName = OsDisplay.Name(osProfile.Effective);
        OsOrigin = OsDisplay.Origin(osProfile);
        SelectedItem = Items[0];
    }

    public IReadOnlyList<NavigationItem> Items { get; }

    public string OsName { get; }

    public string OsOrigin { get; }

    public string AppVersion { get; } = "Versão 0.1 (prévia)";

    [RelayCommand]
    private void NavigateTo(AppPage page) =>
        SelectedItem = Items.First(item => item.Page == page);

    partial void OnSelectedItemChanged(NavigationItem? value)
    {
        if (value is null)
        {
            return;
        }

        if (!_pages.TryGetValue(value.Page, out var page))
        {
            page = _pageFactory.Create(value.Page);
            _pages[value.Page] = page;
        }

        CurrentPage = page;
    }
}
