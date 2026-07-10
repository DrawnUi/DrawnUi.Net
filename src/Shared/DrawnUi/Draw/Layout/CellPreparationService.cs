namespace DrawnUi.Draw;

/// <summary>
/// Dedicated worker for the prepared-views pipeline (<see cref="SkiaLayout.UsePreparedViews"/>):
/// binds and measures REAL templated cell views ahead of scrolling, off the render thread, so
/// DrawStack never has to sync-measure a first-appearing cell (the rhythmic fling spike killer).
///
/// Each posting layout keeps ONE latest want-list (ordered by priority: visible-unprepared first,
/// then ahead of the scroll direction, then behind); a new post replaces the previous list, so
/// stale wants from an older frame are dropped instead of drained. One AboveNormal thread is
/// enough: it must merely outrun the finger, and cell binds+measures are the only work it does.
/// </summary>
internal static class CellPreparationService
{
    private static readonly object Lock = new();
    private static readonly Dictionary<SkiaLayout, List<int>> Work = new();
    private static readonly AutoResetEvent Signal = new(false);
    private static int _started;

    /// <summary>
    /// Replaces the layout's pending want-list (drop-stale). Pass null/empty to clear it.
    /// List ownership transfers to the service — the caller must not mutate it after posting.
    /// </summary>
    public static void Post(SkiaLayout layout, List<int> indices)
    {
        if (layout == null)
            return;

        lock (Lock)
        {
            if (indices == null || indices.Count == 0)
            {
                Work.Remove(layout);
                return;
            }

            Work[layout] = indices;
        }

        EnsureWorker();
        Signal.Set();
    }

    private static void EnsureWorker()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
            return;

#if BROWSER || WEB
        // Single-threaded WASM: Thread.Start is unsupported, and Task.Run work items starve
        // indefinitely under continuous requestAnimationFrame load (see SkiaControl.Cache.cs inline
        // drain note). Drain the want-lists INLINE on the frame loop instead, budgeted per frame so
        // preparation never eats the frame itself.
        Super.OnFrame += (_, _) => DrainBudgeted(4.0);
#else
        var thread = new Thread(WorkerLoop)
        {
            IsBackground = true, Name = "DrawnUi-CellPrep", Priority = ThreadPriority.AboveNormal,
        };
        thread.Start();
#endif
    }

#if BROWSER || WEB
    private static void DrainBudgeted(double budgetMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed.TotalMilliseconds < budgetMs)
        {
            if (!TryDequeue(out var layout, out var index))
                return;

            try
            {
                layout.PrepareCellOffthread(index);
            }
            catch (Exception e)
            {
                Super.Log(e);
            }
        }
    }
#endif

    private static bool TryDequeue(out SkiaLayout layout, out int index)
    {
        layout = null;
        index = -1;
        List<SkiaLayout> deadLayouts = null;

        lock (Lock)
        {
            foreach (var kvp in Work)
            {
                if (kvp.Key.IsDisposed || kvp.Key.IsDisposing || kvp.Value.Count == 0)
                {
                    (deadLayouts ??= new()).Add(kvp.Key);
                    continue;
                }

                layout = kvp.Key;
                index = kvp.Value[0];
                kvp.Value.RemoveAt(0);
                break;
            }

            if (deadLayouts != null)
            {
                foreach (var dead in deadLayouts)
                {
                    Work.Remove(dead);
                }
            }
        }

        return layout != null;
    }

    private static void WorkerLoop()
    {
        while (true)
        {
            if (!TryDequeue(out var layout, out var index))
            {
                Signal.WaitOne();
                continue;
            }

            try
            {
                layout.PrepareCellOffthread(index);
            }
            catch (Exception e)
            {
                Super.Log(e);
            }
        }
    }
}
