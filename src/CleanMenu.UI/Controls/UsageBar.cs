using System.Windows;
using System.Windows.Media;

namespace CleanMenu.UI.Controls;

/// <summary>Horizontal usage bar (e.g. disk space used). NaN draws only the track.</summary>
public sealed class UsageBar : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(UsageBar),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(UsageBar),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(
        nameof(FillBrush), typeof(Brush), typeof(UsageBar),
        new FrameworkPropertyMetadata(Brushes.SeaGreen, FrameworkPropertyMetadataOptions.AffectsRender));

    public UsageBar()
    {
        Height = 8;
    }

    /// <summary>Percentage 0–100.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public Brush FillBrush
    {
        get => (Brush)GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        var radius = ActualHeight / 2;
        drawingContext.DrawRoundedRectangle(TrackBrush, null, new Rect(0, 0, ActualWidth, ActualHeight), radius, radius);

        if (RingGauge.Fraction(Value, 100) is { } fraction && fraction > 0)
        {
            var width = Math.Max(ActualHeight, ActualWidth * fraction);
            drawingContext.DrawRoundedRectangle(FillBrush, null, new Rect(0, 0, width, ActualHeight), radius, radius);
        }
    }
}
