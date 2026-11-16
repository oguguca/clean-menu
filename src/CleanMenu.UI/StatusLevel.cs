namespace CleanMenu.UI;

/// <summary>
/// Visual status of a metric. <see cref="Neutral"/> means "no data yet" and must be used whenever
/// there is not enough real information to judge.
/// </summary>
public enum StatusLevel
{
    Neutral,
    Good,
    Warning,
    Critical,
}

public static class StatusLevelExtensions
{
    /// <summary>Resource key of the brush that represents the status.</summary>
    public static string BrushKey(this StatusLevel level) => level switch
    {
        StatusLevel.Good => "Brush.Good",
        StatusLevel.Warning => "Brush.Warning",
        StatusLevel.Critical => "Brush.Critical",
        _ => "Brush.Neutral",
    };
}
