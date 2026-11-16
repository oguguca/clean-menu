using System.ComponentModel;
using System.Windows;
using CleanMenu.App.ViewModels;
using CleanMenu.UI.Theming;

namespace CleanMenu.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        FitToWorkArea();
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SourceInitialized += (_, _) => WindowTheme.ApplyTitleBar(this, ThemeManager.Current == AppTheme.Dark);
    }

    /// <summary>
    /// Small notebook screens (e.g. 1366x768, or 1920x1080 at 150% scale) cannot fit the default
    /// size; shrink to the visible area so the window never opens partly off-screen.
    /// </summary>
    private void FitToWorkArea()
    {
        var area = SystemParameters.WorkArea;
        Width = Math.Min(Width, area.Width * 0.95);
        Height = Math.Min(Height, area.Height * 0.95);
        MinWidth = Math.Min(MinWidth, Width);
        MinHeight = Math.Min(MinHeight, Height);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A newly opened page always starts at the top.
        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
        {
            PageScroller.ScrollToTop();
        }
    }
}
