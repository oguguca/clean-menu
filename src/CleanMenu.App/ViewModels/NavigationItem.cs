using CleanMenu.App.Navigation;

namespace CleanMenu.App.ViewModels;

/// <summary>One sidebar entry.</summary>
public sealed class NavigationItem(PageInfo info)
{
    public PageInfo Info { get; } = info ?? throw new ArgumentNullException(nameof(info));

    public AppPage Page => Info.Page;

    public string Title => Info.Title;

    public string Description => Info.Description;

    public string Glyph => Info.Glyph;

    public string Group => Info.Group;

    public bool IsAdvanced => Info.IsAdvanced;

    /// <summary>Used by screen readers to announce the sidebar item.</summary>
    public override string ToString() => Title;
}
