using CleanMenu.UI;

namespace CleanMenu.App.ViewModels;

/// <summary>Data for one metric card on the dashboard.</summary>
public sealed record SummaryCardModel(
    string Title,
    string Glyph,
    string Value,
    string Caption,
    string HelpText,
    StatusLevel Status = StatusLevel.Neutral);
