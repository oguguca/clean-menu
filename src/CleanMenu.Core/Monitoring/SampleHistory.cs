namespace CleanMenu.Core.Monitoring;

/// <summary>
/// Fixed-size history of recent values (oldest first). Thread-safe.
/// </summary>
public sealed class SampleHistory
{
    private readonly double[] _buffer;
    private readonly Lock _lock = new();
    private int _start;
    private int _count;

    public SampleHistory(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _buffer = new double[capacity];
    }

    public int Capacity => _buffer.Length;

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    public void Add(double value)
    {
        lock (_lock)
        {
            var index = (_start + _count) % _buffer.Length;
            _buffer[index] = value;
            if (_count < _buffer.Length)
            {
                _count++;
            }
            else
            {
                _start = (_start + 1) % _buffer.Length;
            }
        }
    }

    /// <summary>Copy of the values, oldest first.</summary>
    public IReadOnlyList<double> Snapshot()
    {
        lock (_lock)
        {
            var result = new double[_count];
            for (var i = 0; i < _count; i++)
            {
                result[i] = _buffer[(_start + i) % _buffer.Length];
            }

            return result;
        }
    }
}
