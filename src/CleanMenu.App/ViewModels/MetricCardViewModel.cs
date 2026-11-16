using CleanMenu.UI;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CleanMenu.App.ViewModels;

/// <summary>One metric card. Value, caption, status and chart can change while the page is open.</summary>
public sealed partial class MetricCardViewModel(string title, string glyph, string helpText) : ObservableObject
{
    public const string Pending = "—";

    public string Title { get; } = title;

    public string Glyph { get; } = glyph;

    public string HelpText { get; } = helpText;

    [ObservableProperty]
    private string _value = Pending;

    [ObservableProperty]
    private string _caption = string.Empty;

    [ObservableProperty]
    private StatusLevel _status = StatusLevel.Neutral;

    /// <summary>Recent values (0–100) for the mini chart; null hides the chart.</summary>
    [ObservableProperty]
    private IReadOnlyList<double>? _history;

    public MetricCardViewModel With(string value, string caption, StatusLevel status = StatusLevel.Neutral)
    {
        Value = value;
        Caption = caption;
        Status = status;
        return this;
    }
}
