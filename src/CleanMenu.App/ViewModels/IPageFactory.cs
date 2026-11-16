using CleanMenu.App.Navigation;

namespace CleanMenu.App.ViewModels;

/// <summary>Creates the view model for a section.</summary>
public interface IPageFactory
{
    PageViewModel Create(AppPage page);
}
