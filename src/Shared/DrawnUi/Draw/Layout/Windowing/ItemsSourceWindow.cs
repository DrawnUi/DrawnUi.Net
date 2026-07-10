using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using AppoMobi.Specials;

namespace DrawnUi.Draw;

/// <summary>
/// BUILT-IN sliding window between a large user ItemsSource and the rendering pipeline.
/// The pipeline binds to <see cref="Items"/> (a bounded slice in natural source order, exposed via
/// SkiaLayout.EffectiveItemsSource); this controller slides the slice INTERNALLY over the full source
/// (in-memory, synchronous — no spinners) as the user scrolls, reusing the layout's existing
/// structure-preserving mutation paths (tail append, head-insert rebase, head/tail window trims).
/// The app's LoadMoreCommand/LoadMoreTopCommand fire ONLY when the window reaches a source edge —
/// separating "page within already-available data" (internal) from "fetch more data" (app/API).
/// Engaged automatically by <see cref="SkiaLayout.TryEngageItemsWindow"/> when a templated ItemsSource
/// exceeds <see cref="SkiaLayout.WindowSourceThreshold"/>; capacity is retuned from the real viewport
/// (<see cref="SkiaLayout.WindowSourceViewports"/> viewports) once the first visible range is known.
/// </summary>
public class ItemsSourceWindow
{
    // pre-tune defaults, used until the first draw tells us how many items fit one viewport
    private const int DefaultBatch = 32;
    private const int DefaultCap = 128;
    private const int MinBatch = 16;

    private readonly SkiaLayout _layout;
    private IList _source;
    private int _batch = DefaultBatch;
    private int _cap = DefaultCap;
    private int _seenPerViewport;
    private volatile bool _sliding;   // one slide in flight until its structure change lands
    private volatile bool _trimArmed; // forward slide (MeasureVisible): head trim deferred until the appended batch measured

    /// <summary>The materialized slice the pipeline renders. Natural source order (no inversion).</summary>
    public ObservableRangeCollection<object> Items { get; } = new();

    /// <summary>Source range currently materialized: [WindowStart, WindowEnd).</summary>
    public int WindowStart { get; private set; }

    public int WindowEnd { get; private set; }

    public IList Source => _source;

    public bool CanSlideForward => _source != null && WindowEnd < _source.Count;

    public bool CanSlideBackward => WindowStart > 0;

    // MeasureVisible mutates structure incrementally and raises MeasurementApplied; other strategies
    // rebuild synchronously, so trims run in the same turn and the slide latch releases immediately.
    private bool DeferredTrims => _layout.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible;

    public ItemsSourceWindow(SkiaLayout layout, IList source)
    {
        _layout = layout;
        _source = source;

        WindowStart = 0;
        WindowEnd = Math.Min(_cap, source.Count);
        var seed = new List<object>(WindowEnd);
        for (int i = 0; i < WindowEnd; i++)
        {
            seed.Add(source[i]);
        }

        Items.AddRange(seed);

        if (source is INotifyCollectionChanged incc)
        {
            incc.CollectionChanged += OnSourceCollectionChanged;
        }

        _layout.MeasurementApplied += OnMeasurementApplied;
    }

    public void Detach()
    {
        if (_source is INotifyCollectionChanged incc)
        {
            incc.CollectionChanged -= OnSourceCollectionChanged;
        }

        _layout.MeasurementApplied -= OnMeasurementApplied;
        _source = null;
    }

    /// <summary>
    /// Derive batch/cap from the REAL viewport: window = <see cref="SkiaLayout.WindowSourceViewports"/>
    /// viewports of items. Tracks the MAX visible range seen — early frames can report a partial
    /// range (a single cell) which must never lock in a tiny thrashing window.
    /// </summary>
    public void AutoTune(int firstVisible, int lastVisible)
    {
        if (lastVisible <= firstVisible)
            return; // partial/degenerate frame, not a real viewport fill

        int perViewport = lastVisible - firstVisible + 1;
        if (perViewport <= _seenPerViewport)
            return;

        _seenPerViewport = perViewport;
        // STABLE GEOMETRY (no slide ping-pong): after a slide of `batch`, the viewport must land
        // OUTSIDE both trigger zones (margin at each end). batch = 1 viewport, margin = 1/2 viewport,
        // cap >= 3 viewports -> post-slide clearance from either zone >= 1 viewport. Bigger batches
        // (2P) with margin=P made the zones overlap the landing spot = infinite forward/backward churn.
        // CAP IS MONOTONIC and never drops below the seed: the seed's cells are already materialized —
        // shrinking under it just throws away paid work and costs one violent multi-batch cut (a 90+
        // row head trim = a one-off frame spike). It only GROWS when the real viewport demands more.
        _batch = Math.Max(perViewport, MinBatch);
        _cap = Math.Max(_cap, Math.Max(perViewport * Math.Max(3, SkiaLayout.WindowSourceViewports), _batch * 3));
        Debug.WriteLine($"[ItemsWindow] tuned: {perViewport}/viewport -> batch={_batch} cap={_cap}");
    }

    /// <summary>
    /// Slide check driven by the layout's viewport updates — INDEPENDENT of the scroll's user
    /// LoadMoreCommand (SkiaScroll only runs its edge checks when a command is set). Near the window
    /// tail → slide forward; near the head (when trimmed) → slide backward.
    /// </summary>
    public void CheckSlide(int firstVisible, int lastVisible)
    {
        if (lastVisible < 0)
            return;

        // SERIALIZE against the structure pipeline: a slide staged while the previous one's
        // head-remove/insert commit is still pending would route through the generic (non-compensated)
        // remove path — the scroll offset jumps and cascades (viewport stranded above content).
        if (_sliding || _layout.HasPendingStructureChanges)
            return;

        // NEVER slide while an ordered ScrollToIndex is in flight: a jump rebase transits the viewport
        // through arbitrary positions (content top for a frame) — a slide fired off that transient
        // shifts the window under the still-settling order and the jump lands short/dies.
        if (_layout.Parent is SkiaScroll scroll &&
            (scroll.OrderedScrollToIndexIsSet || scroll.OrderedScrollTo.IsValid))
            return;

        // margin = 1/2 viewport: with batch = 1 viewport and cap = 4 viewports a slide lands the
        // viewport >= 1 viewport away from BOTH zones — a resting viewport can never re-trigger
        // (the ping-pong churn seen as jumping DebugString intervals / unbound cells on WASM)
        int margin = Math.Max(2, (_seenPerViewport > 0 ? _seenPerViewport : _batch) / 2);

        if (CanSlideForward && lastVisible >= Items.Count - margin)
        {
            RequestSlideForward();
        }
        else if (CanSlideBackward && firstVisible >= 0 && firstVisible <= margin)
        {
            RequestSlideBackward();
        }
    }

    /// <summary>
    /// Maps a GLOBAL (ItemsSource-space) index to a window-local one for a programmatic scroll.
    /// Resident target = plain mapping. Non-resident = the window REBASES centered on the target
    /// (ReplaceRange -> the layout's clean full-replace reset path, templates preserved), then the
    /// returned local index is scrolled to by the caller's ordered-scroll machinery, which waits out
    /// the staged structure change before resolving geometry. Call on the UI thread.
    /// </summary>
    public int MapToLocalForScroll(int global)
    {
        var source = _source;
        if (source == null || source.Count == 0)
            return global;

        global = Math.Clamp(global, 0, source.Count - 1);

        int local = global - WindowStart;
        if (local >= 0 && local < Items.Count)
            return local;

        // rebase centered on the target so there is scroll room on both sides
        WindowStart = Math.Clamp(global - _cap / 2, 0, Math.Max(0, source.Count - _cap));
        WindowEnd = Math.Min(WindowStart + _cap, source.Count);

        var slice = new List<object>(WindowEnd - WindowStart);
        for (int i = WindowStart; i < WindowEnd; i++)
        {
            slice.Add(source[i]);
        }

        Debug.WriteLine($"[ItemsWindow] jump rebase -> [{WindowStart}..{WindowEnd}) for global {global}");
        Items.ReplaceRange(slice);

        return global - WindowStart;
    }

    // ----- internal LoadMore (slides), called from ShouldTriggerLoadMore on the render thread -----

    public void RequestSlideForward()
    {
        if (_sliding || _layout.HasPendingStructureChanges || !CanSlideForward)
            return;

        _sliding = true;
        MainThread.BeginInvokeOnMainThread(SlideForward);
    }

    public void RequestSlideBackward()
    {
        if (_sliding || _layout.HasPendingStructureChanges || !CanSlideBackward)
            return;

        _sliding = true;
        MainThread.BeginInvokeOnMainThread(SlideBackward);
    }

    private void SlideForward()
    {
        var source = _source;
        if (source == null)
        {
            _sliding = false;
            return;
        }

        int n = Math.Min(_batch, source.Count - WindowEnd);
        if (n <= 0)
        {
            _sliding = false;
            return;
        }


        var batch = new List<object>(n);
        for (int i = 0; i < n; i++)
        {
            batch.Add(source[WindowEnd + i]);
        }

        Items.AddRange(batch);
        WindowEnd += n;
        Debug.WriteLine($"[ItemsWindow] forward -> [{WindowStart}..{WindowEnd}) of {source.Count}");

        if (DeferredTrims)
        {
            // MeasureVisible: the head trim MUST wait for the appended batch's measurement — trimming
            // in the same turn shifts unmeasured content and desyncs binds wholesale (harness: 52%
            // wrong contexts at rest when tried 2026-07-10). The resulting multi-frame ContentSize
            // grow->shrink (the WASM slide spike) needs a REPORTED-size clamp during the slide, not an
            // early trim.
            _trimArmed = true;
        }
        else
        {
            // MeasureFirst: appended cells were placed arithmetically (already "measured") — trim in
            // the SAME turn (mirror of SlideBackward): one pre-paint pass, net state only, resident
            // count and ContentSize stable across frames.
            TrimHead();
            _sliding = false;
        }
    }

    private void SlideBackward()
    {
        var source = _source;
        if (source == null)
        {
            _sliding = false;
            return;
        }

        int n = Math.Min(_batch, WindowStart);
        if (n <= 0)
        {
            _sliding = false;
            return;
        }

        // trim the tail BEFORE the head insert, same UI turn — tail is far offscreen, no visual flash
        int over = Items.Count + n - _cap;
        if (over > 0)
        {
            over = Math.Min(over, Items.Count);
            Items.RemoveRange(Items.Count - over, over);
            WindowEnd -= over;
        }

        var batch = new List<object>(n);
        for (int i = 0; i < n; i++)
        {
            batch.Add(source[WindowStart - n + i]);
        }

        Items.InsertRange(0, batch);
        WindowStart -= n;
        Debug.WriteLine($"[ItemsWindow] backward -> [{WindowStart}..{WindowEnd}) of {source.Count}");

        if (!DeferredTrims)
        {
            _sliding = false;
        }
        // else: released by OnMeasurementApplied once the head-insert commit lands
    }

    private void TrimHead()
    {
        int over = Items.Count - _cap;
        if (over > 0)
        {
            Items.RemoveRange(0, over);
            WindowStart += over;
            Debug.WriteLine($"[ItemsWindow] head trim -> [{WindowStart}..{WindowEnd})");
        }
    }

    private void OnMeasurementApplied()
    {
        if (_trimArmed)
        {
            // stay armed until actually over cap: MeasurementApplied fires for EVERY applied
            // background-measure chunk, an early fire from a previous batch must not disarm the trim
            if (Items.Count - _cap <= 0)
            {
                _trimArmed = false;
                _sliding = false;
                return;
            }

            _trimArmed = false;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                TrimHead();
                _sliding = false;
            });
            return;
        }

        _sliding = false;
    }

    // ----- external source mutations mapped into the window (UI thread, same contract as ItemsSource) -----

    private void OnSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
    {
        var source = _source;
        if (source == null)
            return;

        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add:
            {
                int index = args.NewStartingIndex < 0 ? source.Count - args.NewItems.Count : args.NewStartingIndex;
                int count = args.NewItems.Count;
                if (index < WindowStart)
                {
                    // before the window: residents keep items, their global indices shift silently
                    WindowStart += count;
                    WindowEnd += count;
                }
                else if (index < WindowEnd)
                {
                    // inside the window: materialize; cap converges via later slides
                    var list = new List<object>(count);
                    foreach (var item in args.NewItems)
                    {
                        list.Add(item);
                    }

                    Items.InsertRange(index - WindowStart, list);
                    WindowEnd += count;
                }

                // index >= WindowEnd (incl. tail append): not materialized, forward slides pick it up
                break;
            }

            case NotifyCollectionChangedAction.Remove:
            {
                int index = args.OldStartingIndex;
                int count = args.OldItems.Count;
                if (index + count <= WindowStart)
                {
                    WindowStart -= count;
                    WindowEnd -= count;
                }
                else if (index >= WindowEnd)
                {
                    // beyond the window: nothing to do
                }
                else if (index >= WindowStart && index + count <= WindowEnd)
                {
                    Items.RemoveRange(index - WindowStart, count);
                    WindowEnd -= count;
                }
                else
                {
                    // removal straddles a window edge — rare; rebuild cleanly
                    RebuildWindow();
                }

                break;
            }

            case NotifyCollectionChangedAction.Replace:
            {
                int index = args.NewStartingIndex;
                if (index >= WindowStart && index < WindowEnd && args.NewItems.Count == args.OldItems.Count)
                {
                    for (int i = 0; i < args.NewItems.Count; i++)
                    {
                        int local = index - WindowStart + i;
                        if (local >= 0 && local < Items.Count)
                        {
                            Items[local] = args.NewItems[i];
                        }
                    }
                }
                else
                {
                    RebuildWindow();
                }

                break;
            }

            default:
                // Reset / Move / anything exotic: rebuild the slice around the current position
                RebuildWindow();
                break;
        }
    }

    private void RebuildWindow()
    {
        var source = _source;
        if (source == null)
            return;

        WindowStart = Math.Clamp(WindowStart, 0, Math.Max(0, source.Count - 1));
        WindowEnd = Math.Min(WindowStart + _cap, source.Count);
        var slice = new List<object>(WindowEnd - WindowStart);
        for (int i = WindowStart; i < WindowEnd; i++)
        {
            slice.Add(source[i]);
        }

        Items.ReplaceRange(slice); // full-collection replace routes through the clean reset cycle
    }
}
