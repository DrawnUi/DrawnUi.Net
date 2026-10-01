namespace DrawnUi.Draw;

/// <summary>
/// Lets at most a given number of image loads run at once; the others wait in line by priority:
/// High, then Normal, then Low, first come first served within one priority. A running load is never preempted.
/// Used by <see cref="SkiaImageManager"/> around network loads.
/// </summary>
public sealed class ImageLoadQueue
{
    readonly object _lock = new();
    readonly LinkedList<(LoadPriority Priority, TaskCompletionSource<bool> Signal)> _waiting = new();
    readonly Func<int> _limit;
    int _running;

    /// <param name="limit">How many loads may run at once, read every time a slot is handed out.</param>
    public ImageLoadQueue(Func<int> limit)
    {
        _limit = limit;
    }

    /// <summary>
    /// Loads running now.
    /// </summary>
    public int RunningCount
    {
        get
        {
            lock (_lock)
                return _running;
        }
    }

    /// <summary>
    /// Loads waiting for a free slot.
    /// </summary>
    public int QueuedCount
    {
        get
        {
            lock (_lock)
                return _waiting.Count;
        }
    }

    int Limit => Math.Max(1, _limit());

    /// <summary>
    /// Waits for a free slot. Every completed wait must be paired with one <see cref="Release"/>.
    /// </summary>
    public async Task WaitAsync(LoadPriority priority, CancellationToken cancel = default)
    {
        TaskCompletionSource<bool> signal;
        LinkedListNode<(LoadPriority Priority, TaskCompletionSource<bool> Signal)> node;

        lock (_lock)
        {
            cancel.ThrowIfCancellationRequested();

            if (_waiting.Count == 0 && _running < Limit)
            {
                _running++;
                return;
            }

            signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // after the last waiter of the same or a higher priority
            var after = _waiting.Last;
            while (after != null && after.Value.Priority < priority)
                after = after.Previous;
            node = after == null
                ? _waiting.AddFirst((priority, signal))
                : _waiting.AddAfter(after, (priority, signal));
        }

        if (!cancel.CanBeCanceled)
        {
            await signal.Task;
            return;
        }

        using (cancel.Register(() =>
               {
                   lock (_lock)
                   {
                       if (node.List == null)
                           return; // already handed a slot
                       _waiting.Remove(node);
                   }

                   signal.TrySetCanceled(cancel);
               }))
        {
            await signal.Task;
        }
    }

    /// <summary>
    /// Frees the slot taken by a completed <see cref="WaitAsync"/> and hands it to the next waiter.
    /// </summary>
    public void Release()
    {
        lock (_lock)
        {
            if (_running > 0)
                _running--;

            while (_running < Limit && _waiting.First != null)
            {
                var next = _waiting.First.Value.Signal;
                _waiting.RemoveFirst();
                _running++;
                next.TrySetResult(true);
            }
        }
    }
}
