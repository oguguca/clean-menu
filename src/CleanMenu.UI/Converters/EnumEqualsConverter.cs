using System.Globalization;
using System.Windows.Data;

namespace CleanMenu.UI.Converters;

/// <summary>
/// Binds an enum property to a RadioButton: true when the value equals the parameter; checking the
/// button writes the parameter back.
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is not null && value.Equals(parameter);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is not null ? parameter : Binding.DoNothing;
}

/// <summary>Visible when the value is not null, not empty and not zero.</summary>
public sealed class HasValueToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value switch
        {
            null => false,
            string text => text.Length > 0,
            int number => number != 0,
            bool flag => flag,
            _ => true,
        };

        var invert = parameter is string p && p.Equals("invert", StringComparison.OrdinalIgnoreCase);
        return hasValue ^ invert ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
