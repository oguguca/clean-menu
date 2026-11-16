using System.Windows;
using CleanMenu.App.Services;
using CleanMenu.UI.Theming;

namespace CleanMenu.App.Views;

/// <summary>
/// Themed confirmation dialog. "Cancelar" is the default button, so pressing Enter or Esc never
/// confirms a destructive action by accident.
/// </summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog(ConfirmationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        InitializeComponent();
        Title = request.Title;
        TitleText.Text = request.Title;
        MessageText.Text = request.Message;
        DetailsList.ItemsSource = request.Details;
        DetailsList.Visibility = request.Details.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ConfirmButton.Content = request.ConfirmText;
        if (request.IsDestructive)
        {
            ConfirmButton.SetResourceReference(BackgroundProperty, "Brush.Warning");
        }

        SourceInitialized += (_, _) => WindowTheme.ApplyTitleBar(this, ThemeManager.Current == AppTheme.Dark);
        Loaded += (_, _) => CancelButton.Focus();
    }

    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
