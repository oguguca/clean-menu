using CleanMenu.App.Navigation;

namespace CleanMenu.App.Services;

/// <summary>Lets pages ask the shell to open another section.</summary>
public interface INavigationService
{
    event EventHandler<AppPage>? NavigationRequested;

    void NavigateTo(AppPage page);
}

public sealed class NavigationService : INavigationService
{
    public event EventHandler<AppPage>? NavigationRequested;

    public void NavigateTo(AppPage page) => NavigationRequested?.Invoke(this, page);
}
