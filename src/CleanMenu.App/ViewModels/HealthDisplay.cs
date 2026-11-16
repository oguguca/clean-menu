using System.Globalization;
using CleanMenu.Core.Monitoring;
using CleanMenu.UI;

namespace CleanMenu.App.ViewModels;

/// <summary>Maps measured health to visuals and formats numbers in pt-BR.</summary>
public static class HealthDisplay
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static StatusLevel ToStatus(HealthLevel level) => level switch
    {
        HealthLevel.Good => StatusLevel.Good,
        HealthLevel.Attention => StatusLevel.Warning,
        HealthLevel.Critical => StatusLevel.Critical,
        _ => StatusLevel.Neutral,
    };

    public static string Percent(double value) =>
        double.IsNaN(value) ? "—" : $"{Math.Round(value).ToString("0", PtBr)}%";

    public static string Number(double value, string format = "0.#") => value.ToString(format, PtBr);

    public static string Date(DateTimeOffset? value) =>
        value is { } date ? date.ToLocalTime().ToString("dd/MM/yyyy", PtBr) : "Não informado";

    public static string Duration(TimeSpan span) => span.TotalDays >= 1
        ? $"{(int)span.TotalDays} dia(s) e {span.Hours} hora(s)"
        : span.TotalHours >= 1 ? $"{(int)span.TotalHours} hora(s) e {span.Minutes} min" : $"{Math.Max(1, span.Minutes)} min";
}
