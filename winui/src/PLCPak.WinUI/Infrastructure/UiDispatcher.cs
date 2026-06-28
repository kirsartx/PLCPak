using Microsoft.UI.Dispatching;

namespace PLCPak.WinUI.Infrastructure;

public sealed class UiDispatcher
{
    private readonly DispatcherQueue _queue;

    public UiDispatcher(DispatcherQueue queue) => _queue = queue;

    public static UiDispatcher ForCurrentThread() => new(DispatcherQueue.GetForCurrentThread());

    public void Run(Action action)
    {
        if (_queue.HasThreadAccess)
            action();
        else
            _queue.TryEnqueue(() => action());
    }

    public void Run<T>(Action<T> action, T state)
    {
        Run(() => action(state));
    }
}