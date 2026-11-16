using System.Windows;

namespace CleanMenu.UI.Theming;

public enum AppTheme
{
    Dark,
    Light,
}

/// <summary>
/// Swaps the color dictionary (Colors.Dark.xaml / Colors.Light.xaml) in the application resources.
/// Brushes are referenced with DynamicResource, so open windows update immediately.
/// </summary>
public static class ThemeManager
{
    private const string ColorsMarker = "/Themes/Colors.";

    public static AppTheme Current { get; private set; } = AppTheme.Dark;

    public static event EventHandler<AppTheme>? ThemeChanged;

    public static Uri ColorsUri(AppTheme theme) =>
        new($"pack://application:,,,/CleanMenu.UI;component/Themes/Colors.{theme}.xaml", UriKind.Absolute);

    public static void Apply(Application application, AppTheme theme)
    {
        ArgumentNullException.ThrowIfNull(application);

        var dictionaries = application.Resources.MergedDictionaries;
        var colors = new ResourceDictionary { Source = ColorsUri(theme) };
        var existing = dictionaries.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains(ColorsMarker, StringComparison.Ordinal) == true);

        if (existing is null)
        {
            dictionaries.Insert(0, colors);
        }
        else
        {
            dictionaries[dictionaries.IndexOf(existing)] = colors;
        }

        Current = theme;
        foreach (Window window in application.Windows)
        {
            WindowTheme.ApplyTitleBar(window, theme == AppTheme.Dark);
        }

        ThemeChanged?.Invoke(null, theme);
    }
}
