namespace TrafficSimulation.Engine.Simulation;

/// <summary>
/// Batches a burst of change notifications into a single notification.
/// </summary>
/// <remarks>
/// <para>
/// Several editor operations change the city more than once: splitting a road removes it, redistributes
/// its vehicles and adds two new roads. Without coalescing the view would render four times for one
/// user gesture. The coalescer defers the notification until the outermost operation has finished.
/// </para>
/// <para>
/// It is intentionally tiny and single threaded: the simulation loop and the map editor both run on
/// the user-interface thread.
/// </para>
/// </remarks>
public sealed class ChangeNotifier
{
    private readonly Action _onChanged;
    private int _deferDepth;
    private bool _pending;

    /// <summary>Creates a notifier.</summary>
    /// <param name="onChanged">Callback invoked once per batch of changes.</param>
    public ChangeNotifier(Action onChanged)
        => _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));

    /// <summary>Whether a notification is waiting to be delivered.</summary>
    public bool HasPendingNotification => _pending;

    /// <summary>Records a change, delivering it immediately unless a batch is open.</summary>
    public void Notify()
    {
        if (_deferDepth > 0)
        {
            _pending = true;
            return;
        }

        _onChanged();
    }

    /// <summary>
    /// Runs an action with change notifications batched, delivering a single notification at the end
    /// if anything changed.
    /// </summary>
    /// <param name="action">Action that may change the model any number of times.</param>
    public void Batch(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        _deferDepth++;
        try
        {
            action();
        }
        finally
        {
            _deferDepth--;
            if (_deferDepth == 0 && _pending)
            {
                _pending = false;
                _onChanged();
            }
        }
    }

    /// <summary>Runs a function with change notifications batched and returns its result.</summary>
    /// <typeparam name="TResult">Type of the result.</typeparam>
    /// <param name="function">Function that may change the model.</param>
    /// <returns>The value returned by <paramref name="function"/>.</returns>
    public TResult Batch<TResult>(Func<TResult> function)
    {
        ArgumentNullException.ThrowIfNull(function);

        TResult result = default!;
        Batch(() => result = function());
        return result;
    }
}
