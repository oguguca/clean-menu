using CleanMenu.UI.Controls;

namespace CleanMenu.App.Tests.Controls;

public class GaugeMathTests
{
    [Theory]
    [InlineData(50, 100, 0.5)]
    [InlineData(0, 100, 0)]
    [InlineData(150, 100, 1)]
    [InlineData(-5, 100, 0)]
    public void RingFraction_IsClamped(double value, double max, double expected)
    {
        Assert.Equal(expected, RingGauge.Fraction(value, max));
    }

    [Fact]
    public void RingFraction_NoValue_IsNull()
    {
        Assert.Null(RingGauge.Fraction(double.NaN, 100));
        Assert.Null(RingGauge.Fraction(10, 0));
    }

    [Fact]
    public void Sparkline_ProjectsIntoBox()
    {
        var points = Sparkline.Project([0, 50, 100], 100, 200, 40);

        Assert.Equal(3, points.Count);
        Assert.Equal(0, points[0].X);
        Assert.Equal(40, points[0].Y);   // 0% at the bottom
        Assert.Equal(100, points[1].X);
        Assert.Equal(20, points[1].Y);
        Assert.Equal(200, points[2].X);
        Assert.Equal(0, points[2].Y);    // 100% at the top
    }

    [Fact]
    public void Sparkline_NeedsTwoSamples()
    {
        Assert.Empty(Sparkline.Project([42], 100, 200, 40));
    }
}
