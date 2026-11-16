using System.Windows;
using System.Windows.Controls;

namespace CleanMenu.UI.Controls;

public partial class ComingSoonPanel : UserControl
{
    public static readonly DependencyProperty FeaturesProperty = DependencyProperty.Register(
        nameof(Features), typeof(IReadOnlyList<string>), typeof(ComingSoonPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty NoteProperty = DependencyProperty.Register(
        nameof(Note), typeof(string), typeof(ComingSoonPanel), new PropertyMetadata(string.Empty));

    public ComingSoonPanel()
    {
        InitializeComponent();
    }

    public IReadOnlyList<string>? Features
    {
        get => (IReadOnlyList<string>?)GetValue(FeaturesProperty);
        set => SetValue(FeaturesProperty, value);
    }

    public string Note
    {
        get => (string)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }
}
