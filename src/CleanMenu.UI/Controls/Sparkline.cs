using System.Windows;
using System.Windows.Media;

namespace CleanMenu.UI.Controls;

/// <summary>
/// Small line chart for recent samples (e.g. CPU usage over the last minute). Assign a new list
/// to <see cref="Values"/> to redraw. Draws nothing when there are fewer than two samples.
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(Sparkline),
        new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.SeaGreen, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>Maps samples to points inside a width x height box (y grows downward).</summary>
    public static IReadOnlyList<Point> Project(IReadOnlyList<double> values, double maximum, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count < 2 || maximum <= 0 || width <= 0 || height <= 0)
        {
            return [];
        }

        var step = width / (values.Count - 1);
        var points = new Point[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var ratio = Math.Clamp(values[i] / maximum, 0d, 1d);
            points[i] = new Point(i * step, height - ratio * height);
        }

        return points;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        if (Values is not { } values)
        {
            return;
        }

        const double inset = 1.5; // keep the stroke inside the bounds
        var points = Project(values, Maximum, ActualWidth, ActualHeight - 2 * inset);
        if (points.Count < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(Offset(points[0], inset), isFilled: false, isClosed: false);
            for (var i = 1; i < points.Count; i++)
            {
                context.LineTo(Offset(points[i], inset), isStroked: true, isSmoothJoin: true);
            }
        }

        geometry.Freeze();
        var pen = new Pen(Stroke, 2) { LineJoin = PenLineJoin.Round };
        drawingContext.DrawGeometry(null, pen, geometry);
    }

    private static Point Offset(Point point, double dy) => new(point.X, point.Y + dy);
}
