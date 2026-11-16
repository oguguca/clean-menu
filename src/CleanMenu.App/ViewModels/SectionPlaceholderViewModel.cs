using CleanMenu.App.Navigation;

namespace CleanMenu.App.ViewModels;

/// <summary>
/// Page for a section that is not implemented yet. Says so plainly and lists what it will do,
/// instead of showing fake controls.
/// </summary>
public sealed class SectionPlaceholderViewModel(PageInfo info) : PageViewModel(info)
{
    public IReadOnlyList<string> PlannedFeatures => Info.PlannedFeatures;

    public string Note { get; } = "Disponível em uma próxima versão do Clean Menu.";
}
