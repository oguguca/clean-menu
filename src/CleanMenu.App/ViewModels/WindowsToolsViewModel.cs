using CleanMenu.App.Navigation;
using CleanMenu.App.Services;
using CleanMenu.Core.Platform;
using CleanMenu.Core.Tools;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CleanMenu.App.ViewModels;

/// <summary>Ferramentas do Windows: shortcuts to built-in tools that exist on the chosen Windows version.</summary>
public sealed partial class WindowsToolsViewModel : PageViewModel
{
    private readonly ISystemActions _actions;

    [ObservableProperty]
    private string? _message;

    public WindowsToolsViewModel(ISystemActions actions, OsProfile osProfile)
        : base(PageCatalog.Get(AppPage.WindowsTools))
    {
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        Tools = WindowsToolCatalog.For(osProfile);
    }

    public IReadOnlyList<WindowsTool> Tools { get; }

    [RelayCommand]
    private void Open(WindowsTool? tool)
    {
        if (tool is null)
        {
            return;
        }

        Message = _actions.OpenTool(tool.Command, tool.Arguments)
            ? null
            : $"Não foi possível abrir \"{tool.Title}\" neste computador.";
    }
}
