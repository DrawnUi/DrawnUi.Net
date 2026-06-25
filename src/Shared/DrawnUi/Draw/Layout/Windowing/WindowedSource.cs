using System.Diagnostics;
using AppoMobi.Specials;

namespace DrawnUi.Draw;

/// <summary>
/// A sliding-window adapter over a remote/large data source for a virtualizing scroll. It keeps only a
/// bounded slice (<see cref="WindowStart"/>..<see cref="WindowEnd"/>) materialized in <see cref="Items"/>
/// and pulls more from an <see cref="IWindowDataSource{T}"/> in BOTH directions as the user scrolls,
/// capping memory by trimming the opposite end. It NEVER holds the full collection.
///
/// Built for the INVERTED chat model (Rotation=180): the window is exposed newest-first, i.e.
/// <c>Items[i]</c> maps to global <c>WindowEnd - 1 - i</c>. The data source returns ascending global
/// order; this class owns the inversion. Mapping lives in one place (<see cref="ToLocal"/>/<see cref="ToGlobal"/>).
///
/// Every data access — initial seed, LoadOlder, LoadNewer, and the "long jump" window-replace — is async
/// and toggles a loading flag so a single spinner can be shown (and repositioned) per operation.
/// </summary>
public sealed class WindowedSource<T>
{
    private readonly int _batch;
    private readonly int _maxInMemory;
    private readonly bool _limitMemory;

    private IWindowDataSource<T> _source;
    private IWindowHost _host;
    private int _count;

    public WindowedSource(int batch, int maxInMemory, bool limitMemory)
    {
        _batch = batch;
        _maxInMemory = maxInMemory;
        _limitMemory = limitMemory;
    }

    /// <summary>The materialized window, newest-first. Bound to the stack's ItemsSource.</summary>
    public ObservableRangeCollection<T> Items { get; } = new();

    /// <summary>Total items in the backing source (history length + local live inserts).</summary>
    public int Count => _count;

    public int WindowStart { get; private set; }
    public int WindowEnd { get; private set; }

    /// <summary>Window touches the newest end (no newer messages were trimmed away).</summary>
    public bool AtPresent => WindowEnd == _count;

    /// <summary>Window touches the oldest end (history start is resident).</summary>
    public bool AtOldest => WindowStart == 0;

    /// <summary>Invoked with each freshly loaded slice (newest-first), e.g. to preload images. UI-thread.</summary>
    public Action<IReadOnlyList<T>> OnSliceLoaded { get; set; }

    /// <summary>A LoadOlder (history) fetch is in flight. Spinner at the TOP edge.</summary>
    public bool IsLoadingOlder { get; private set; }

    /// <summary>A LoadNewer (head-insert) fetch is in flight. Spinner at the BOTTOM edge.</summary>
    public bool IsLoadingNewer { get; private set; }

    /// <summary>A long-jump window-replace fetch is in flight. Spinner CENTERED.</summary>
    public bool IsLoadingJump { get; private set; }

    /// <summary>Raised (UI thread) whenever any of the loading flags changes.</summary>
    public Action LoadingChanged { get; set; }

    public void SetHost(IWindowHost host) => _host = host;
    public void SetDataSource(IWindowDataSource<T> source) => _source = source;

    // INVERTED mapping, single source of truth: visual order is newest-first.
    public int ToLocal(int global) => WindowEnd - 1 - global;
    public int ToGlobal(int local) => WindowEnd - 1 - local;

    // ----- seeding ---------------------------------------------------------

    /// <summary>Fetch the total count and materialize the present (newest) window. Call once after
    /// <see cref="SetDataSource"/> (and after the host is set).</summary>
    public async Task InitializeAsync(CancellationToken cancel = default)
    {
        if (_source == null)
            return;

        _count = await _source.GetCountAsync(cancel);
        WindowEnd = _count;
        WindowStart = Math.Max(0, WindowEnd - _batch);
        var asc = await _source.GetRangeAsync(WindowStart, WindowEnd - WindowStart, cancel);
        var slice = Reverse(asc);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Items.Clear();
            Items.AddRange(slice);
        });
    }

    // ----- jump ------------------------------------------------------------

    /// <summary>
    /// Universal jump to a GLOBAL index. Resident target = a plain scroll (no fetch). Out-of-window =
    /// rebase a window CENTERED on it (room on both sides), fetch that slice (centered spinner shows via
    /// <see cref="IsLoadingJump"/>), ReplaceRange, then ordered-scroll there. LoadMore is suppressed for
    /// the settle and released in <see cref="OnScrolled"/> once the scroll lands.
    /// </summary>
    public async Task ScrollToIndex(int global, RelativePositionType align, bool animate)
    {
        if (_host == null || _source == null || _count == 0)
            return;

        global = Math.Clamp(global, 0, _count - 1);

        int local = ToLocal(global);
        if (local >= 0 && local < Items.Count)
        {
            _host.ScrollToLocal(local, align, animate);
            return;
        }

        int half = _batch / 2;
        WindowEnd = Math.Min(_count, global + half + 1);
        WindowStart = Math.Max(0, WindowEnd - _batch);
        local = ToLocal(global);
        Debug.WriteLine($"[WINDOW] jump to {global}, window -> [{WindowStart}..{WindowEnd}) local={local}/{WindowEnd - WindowStart}");

        // Suppress LoadMore for the FETCH only: a page-in on the OLD window mid-fetch would drift the jump.
        // After the new window is in and the ordered scroll is issued, the base ordered-scroll LoadMore gate
        // (ShouldTriggerLoadMore returns false while OrderedScrollToIndexIsSet) takes over and SELF-CLEARS on
        // completion — so we hand off and release here. No armed latch, no stuck-suppress (the bug where
        // history LoadMore died after a centered jump because the latch never caught the trailing event).
        _host.SuppressLoadMore = true;
        SetLoading(jump: true);

        IReadOnlyList<T> asc;
        try { asc = await _source.GetRangeAsync(WindowStart, WindowEnd - WindowStart); }
        catch
        {
            _host.SuppressLoadMore = false;
            SetLoading(jump: false);
            return;
        }

        var slice = Reverse(asc);
        var targetLocal = local;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            OnSliceLoaded?.Invoke(slice);
            Items.ReplaceRange(slice);
            _host.ScrollToLocal(targetLocal, align, animate); // sets OrderedScrollToIndex -> gate blocks LoadMore until done
            _host.SuppressLoadMore = false;                   // hand off to the self-clearing ordered-scroll gate
            SetLoading(jump: false);
        });
    }

    // ----- navigation (atomic replace + scroll) ----------------------------
    // The window-replace and the scroll MUST happen in the SAME UI turn: a measure pass between them would
    // run without the ordered target set and land the viewport in unmeasured space (stall/blank). So these
    // fetch off-thread, then ReplaceRange + scroll together. They show the centered jump loader while
    // fetching (a window-replace is a load too).

    /// <summary>Jump to the present (newest). Resident-present = plain scroll to index 0; detached = fetch
    /// the present window, replace, and snap to content start (offset 0 = newest).</summary>
    public async Task ScrollToNewest(bool animate)
    {
        if (_host == null || _source == null || _count == 0)
            return;

        if (AtPresent)
        {
            _host.ScrollToLocal(0, RelativePositionType.Start, animate);
            return;
        }

        SetLoading(jump: true);
        WindowEnd = _count;
        WindowStart = Math.Max(0, WindowEnd - _batch);

        IReadOnlyList<T> asc;
        try { asc = await _source.GetRangeAsync(WindowStart, WindowEnd - WindowStart); }
        catch { SetLoading(jump: false); return; }

        var slice = Reverse(asc);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            OnSliceLoaded?.Invoke(slice);
            Items.ReplaceRange(slice);
            _host.SnapToStart(); // offset 0 = newest, instant (always valid even mid re-measure)
            SetLoading(jump: false);
        });
    }

    /// <summary>Jump to the oldest (history start): fetch the head window, replace, ordered-scroll to the
    /// visual top (oldest = last resident in the inverted list). LoadMore suppressed for the settle.</summary>
    public async Task ScrollToOldest(bool animate)
    {
        if (_host == null || _source == null || _count == 0)
            return;

        if (AtOldest && Items.Count > 0)
        {
            _host.ScrollToLocal(Items.Count - 1, RelativePositionType.Start, animate);
            return;
        }

        _host.SuppressLoadMore = true; // fetch-only; ordered-scroll gate takes over after the scroll is issued
        SetLoading(jump: true);

        WindowStart = 0;
        WindowEnd = Math.Min(_batch, _count);

        IReadOnlyList<T> asc;
        try { asc = await _source.GetRangeAsync(WindowStart, WindowEnd - WindowStart); }
        catch
        {
            _host.SuppressLoadMore = false;
            SetLoading(jump: false);
            return;
        }

        var slice = Reverse(asc);
        int local = slice.Count - 1;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            OnSliceLoaded?.Invoke(slice);
            Items.ReplaceRange(slice);
            _host.ScrollToLocal(local, RelativePositionType.Start, animate); // ordered -> gate blocks LoadMore until done
            _host.SuppressLoadMore = false;                                  // hand off to the self-clearing gate
            SetLoading(jump: false);
        });
    }

    // ----- rebasing --------------------------------------------------------

    /// <summary>Rebase the window back to the present (newest). <paramref name="reset"/> uses
    /// ReplaceRangeReset (snaps the inverted scroll to content start) vs a plain structure-preserving
    /// ReplaceRange when the caller snaps the offset itself.</summary>
    public async Task RebaseToPresent(bool reset)
    {
        if (_source == null)
            return;

        WindowEnd = _count;
        WindowStart = Math.Max(0, WindowEnd - _batch);
        var asc = await _source.GetRangeAsync(WindowStart, WindowEnd - WindowStart);
        var slice = Reverse(asc);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (reset)
                Items.ReplaceRangeReset(slice);
            else
                Items.ReplaceRange(slice);
        });
    }

    /// <summary>Rebase to the oldest end (history start). No-op if already there.</summary>
    public async Task RebaseToOldest()
    {
        if (_source == null || WindowStart == 0)
            return;

        WindowStart = 0;
        WindowEnd = Math.Min(_batch, _count);
        var asc = await _source.GetRangeAsync(WindowStart, WindowEnd - WindowStart);
        var slice = Reverse(asc);
        await MainThread.InvokeOnMainThreadAsync(() => Items.ReplaceRange(slice));
    }


    // ----- live insert -----------------------------------------------------

    /// <summary>Register a new newest item (already appended to the backing source by the caller) and, when
    /// the window is at the present, head-insert it into the visible window. Returns false when detached
    /// from the present (it lives only in the source until the user returns).</summary>
    public bool InsertNewest(T item)
    {
        bool atPresent = WindowEnd == _count;
        _count++;

        if (!atPresent)
            return false;

        if (_limitMemory && _host != null)
            _host.OnMeasured = LimitForNewerInsert;

        WindowEnd++;
        Items.Insert(0, item);
        return true;
    }

    // ----- paging (scroll-driven) -----------------------------------------

    /// <summary>History: older items APPENDED at the list end (top spinner). Memory cap trims newest first.
    /// Re-entrant triggers during the fetch are ignored.</summary>
    public void LoadOlder()
    {
        if (_source == null || WindowStart <= 0 || IsLoadingOlder)
            return;

        int n = Math.Min(_batch, WindowStart);
        int from = WindowStart - n;
        SetLoading(older: true);
        _ = LoadOlderAsync(from, n);
    }

    private async Task LoadOlderAsync(int from, int n)
    {
        IReadOnlyList<T> asc;
        try { asc = await _source.GetRangeAsync(from, n); }
        catch { SetLoading(older: false); return; }

        var loaded = Reverse(asc);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ApplyOlder(n, loaded);
            SetLoading(older: false);
        });
    }

    private void ApplyOlder(int n, IReadOnlyList<T> loaded)
    {
        WindowStart -= n;
        Debug.WriteLine($"[WINDOW] LoadOlder {WindowStart}..{WindowStart + n - 1}");
        OnSliceLoaded?.Invoke(loaded);
        if (_host != null)
            _host.OnMeasured = LimitNewestAfterOlder;
        Items.AddRange(loaded);
    }

    /// <summary>Back towards the present: newer items HEAD-INSERTED (bottom spinner). The oldest tail is
    /// trimmed BEFORE the insert, same UI turn, to avoid a grow-then-shrink flash. Re-entrancy ignored.</summary>
    public void LoadNewer()
    {
        if (_source == null || WindowEnd >= _count || IsLoadingNewer)
            return;

        int n = Math.Min(_batch, _count - WindowEnd);
        int from = WindowEnd;
        SetLoading(newer: true);
        _ = LoadNewerAsync(from, n);
    }

    private async Task LoadNewerAsync(int from, int n)
    {
        IReadOnlyList<T> asc;
        try { asc = await _source.GetRangeAsync(from, n); }
        catch { SetLoading(newer: false); return; }

        var loaded = Reverse(asc);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ApplyNewer(n, loaded);
            SetLoading(newer: false);
        });
    }

    private void ApplyNewer(int n, IReadOnlyList<T> loaded)
    {
        Debug.WriteLine($"[WINDOW] LoadNewer {WindowEnd}..{WindowEnd + n - 1}");
        OnSliceLoaded?.Invoke(loaded);

        if (_host != null)
            _host.OnMeasured = null;

        if (_limitMemory)
        {
            int over = Items.Count + n - _maxInMemory;
            if (over > 0)
            {
                over = Math.Min(over, Items.Count);
                Items.RemoveRange(Items.Count - over, over); // list tail = oldest
                WindowStart += over;
            }
        }

        Items.InsertRange(0, loaded);
        WindowEnd += n;
    }

    // ----- deferred trims (run after the stack measures the just-added batch) ----

    private void LimitNewestAfterOlder()
    {
        if (!_limitMemory)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            int over = Items.Count - _maxInMemory;
            if (over > 0)
            {
                Debug.WriteLine($"[WINDOW] TrimNewest {WindowEnd - over}..{WindowEnd - 1}");
                Items.RemoveRange(0, over); // list head = newest
                WindowEnd -= over;
            }
        });

        if (_host != null)
            _host.OnMeasured = null;
    }

    private void LimitForNewerInsert()
    {
        if (!_limitMemory)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            int over = Items.Count - _maxInMemory;
            if (over > 0)
            {
                Debug.WriteLine($"[WINDOW] TrimOldest {WindowStart}..{WindowStart + over - 1}");
                Items.RemoveRange(Items.Count - over, over); // list tail = oldest
                WindowStart += over;
            }
        });

        if (_host != null)
            _host.OnMeasured = null;
    }

    // ----- loading state ----------------------------------------------------

    // Sets exactly the flags named; the others are forced false (only one operation runs at a time —
    // LoadOlder/Newer are re-entrancy-guarded and a jump suppresses LoadMore). Raises LoadingChanged once.
    private void SetLoading(bool older = false, bool newer = false, bool jump = false)
    {
        if (IsLoadingOlder == older && IsLoadingNewer == newer && IsLoadingJump == jump)
            return;
        IsLoadingOlder = older;
        IsLoadingNewer = newer;
        IsLoadingJump = jump;
        RaiseLoadingChanged();
    }

    private void RaiseLoadingChanged()
    {
        var handler = LoadingChanged;
        if (handler == null) return;
        if (MainThread.IsMainThread) handler();
        else MainThread.BeginInvokeOnMainThread(handler);
    }


    private static List<T> Reverse(IReadOnlyList<T> ascending)
    {
        var list = new List<T>(ascending.Count);
        for (int i = ascending.Count - 1; i >= 0; i--)
            list.Add(ascending[i]);
        return list;
    }
}
