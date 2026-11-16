using CleanMenu.Core.SystemInfo;

namespace CleanMenu.App.Services;

/// <summary>
/// Hardware facts do not change while the app runs; read them once (WMI takes a few seconds)
/// and share the result between pages. A failed read is retried on the next request.
/// </summary>
public sealed class CachedSystemInfoProvider(ISystemInfoProvider inner) : ISystemInfoProvider
{
    private readonly Lock _lock = new();
    private Task<SystemInformation>? _task;

    public Task<SystemInformation> GetAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_task is null || _task.IsFaulted || _task.IsCanceled)
            {
                _task = inner.GetAsync(CancellationToken.None);
            }

            return _task.WaitAsync(cancellationToken);
        }
    }
}
