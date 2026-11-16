using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace CleanMenu.UI.Controls;

public partial class StatCard : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(StatCard), new PropertyMetadata("—"));

    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(StatCard), new PropertyMetadata(Glyphs.Info));

    public static readonly DependencyProperty HelpTextProperty = DependencyProperty.Register(
        nameof(HelpText), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(StatusLevel), typeof(StatCard),
        new PropertyMetadata(StatusLevel.Neutral, (d, _) => ((StatCard)d).UpdateStatus()));

    public StatCard()
    {
        InitializeComponent();
        UpdateStatus();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string HelpText
    {
        get => (string)GetValue(HelpTextProperty);
        set => SetValue(HelpTextProperty, value);
    }

    public StatusLevel Status
    {
        get => (StatusLevel)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    private void UpdateStatus()
    {
        // No dot while there is no judgement to show; a gray dot next to "—" reads as noise.
        StatusDot.Visibility = Status == StatusLevel.Neutral ? Visibility.Collapsed : Visibility.Visible;

        // Resource reference keeps the dot in sync when the theme changes.
        StatusDot.SetResourceReference(Shape.FillProperty, Status.BrushKey());
    }
}
