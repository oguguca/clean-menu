using CleanMenu.App.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CleanMenu.App.ViewModels;

/// <summary>Base class for every page shown in the content area.</summary>
public abstract class PageViewModel(PageInfo info) : ObservableObject
{
    public PageInfo Info { get; } = info ?? throw new ArgumentNullException(nameof(info));

    public string Title => Info.Title;

    public string Description => Info.Description;
}
