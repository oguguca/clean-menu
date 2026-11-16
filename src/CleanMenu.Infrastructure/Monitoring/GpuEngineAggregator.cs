using System.Text.RegularExpressions;

namespace CleanMenu.Infrastructure.Monitoring;

/// <summary>
/// Combines "GPU Engine" counter instances into one number, the way Task Manager does: utilization
/// is summed per physical engine across all processes and the busiest engine is reported.
/// Instance names look like "pid_1234_luid_0x0000_0x0000ABCD_phys_0_eng_3_engtype_3D".
/// </summary>
public static partial class GpuEngineAggregator
{
    [GeneratedRegex(@"luid_(?<luid>0x[0-9a-fA-F]+_0x[0-9a-fA-F]+)_phys_(?<phys>\d+)_eng_(?<eng>\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex EnginePattern();

    /// <summary>Returns the busiest engine utilization (0–100), or null when no instance could be parsed.</summary>
    public static double? BusiestEnginePercent(IEnumerable<KeyValuePair<string, double>> instances)
    {
        ArgumentNullException.ThrowIfNull(instances);

        var perEngine = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (instance, percent) in instances)
        {
            var match = EnginePattern().Match(instance);
            if (!match.Success || double.IsNaN(percent) || percent < 0)
            {
                continue;
            }

            var key = $"{match.Groups["luid"].Value}/{match.Groups["phys"].Value}/{match.Groups["eng"].Value}";
            perEngine[key] = perEngine.GetValueOrDefault(key) + percent;
        }

        return perEngine.Count == 0 ? null : Math.Clamp(perEngine.Values.Max(), 0, 100);
    }
}
