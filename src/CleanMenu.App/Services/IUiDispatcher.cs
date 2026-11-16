using System.Windows;

namespace CleanMenu.App.Services;

/// <summary>Runs code on the UI thread. Abstracted so view models can be tested without WPF.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}

public sealed class WpfUiDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }
}

/// <summary>Runs immediately on the calling thread (tests).</summary>
public sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }
}
