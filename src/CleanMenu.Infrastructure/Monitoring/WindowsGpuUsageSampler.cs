using System.Diagnostics;
using CleanMenu.Core.Monitoring;

namespace CleanMenu.Infrastructure.Monitoring;

/// <summary>
/// GPU usage from the "GPU Engine" performance counters (Windows 10 1709+ with a WDDM 2.x driver).
/// Reads the whole category in one call per sample and computes rates between two samples.
/// Returns null when the counters do not exist (unsupported driver or virtual machine).
/// </summary>
public sealed class WindowsGpuUsageSampler : IGpuUsageSampler
{
    private const string Category = "GPU Engine";
    private const string Counter = "Utilization Percentage";

    private Dictionary<string, CounterSample> _previous = [];
    private bool? _supported;

    public double? Sample()
    {
        if (_supported == false)
        {
            return null;
        }

        try
        {
            if (_supported is null)
            {
                _supported = PerformanceCounterCategory.Exists(Category);
                if (_supported == false)
                {
                    return null;
                }
            }

            var data = new PerformanceCounterCategory(Category).ReadCategory();
            var current = new Dictionary<string, CounterSample>(StringComparer.Ordinal);
            if (data.Contains(Counter))
            {
                foreach (InstanceData instance in data[Counter].Values)
                {
                    current[instance.InstanceName] = instance.Sample;
                }
            }

            var rates = new List<KeyValuePair<string, double>>(current.Count);
            foreach (var (name, sample) in current)
            {
                if (_previous.TryGetValue(name, out var before))
                {
                    rates.Add(new(name, CounterSample.Calculate(before, sample)));
                }
            }

            var firstSample = _previous.Count == 0;
            _previous = current;
            if (firstSample)
            {
                return double.NaN;
            }

            return GpuEngineAggregator.BusiestEnginePercent(rates) ?? 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            // Counters can be disabled or corrupted on some machines. GPU usage is optional.
            _supported = false;
            return null;
        }
    }
}
