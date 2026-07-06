namespace DrawnUi.Draw
{
    public class TemplatedViewsPool : IDisposable
    {
        public Func<object> CreateTemplate { get; protected set; }
        public int MaxSize { get; set; }
        public bool IsDisposing;
        private bool _disposed = false;
        private readonly Action<IDisposable> _dispose;

        // New: track height-based pools
        // Key: Rounded integer height, Value: Stack of controls for that height
        private Dictionary<int, Stack<SkiaControl>> _heightPools = new();
        private int _maxDistinctHeights = 24; 

        // Generic (size-key 0) reservoir — the ONLY pool used in RecyclingTemplate.Disabled (GetSizeKey is
        // always 0 there). Split in two so the hot path is allocation-free AND a returned cell can be retrieved
        // again by its exact BindingContext at O(1):
        //
        //   _byContext : tagged cells, keyed by their BindingContext reference. A cell sitting here still holds
        //                its measured/laid-out state for that context. Get(ctx) reclaims the exact cell, so the
        //                index is never re-measured. (Old code scanned only the top 10 of a Stack, so with a
        //                205-cell prefill the matching cell was buried -> missed -> a foreign cell popped ->
        //                rebind -> re-measure every sweep = churn.)
        //   _freeSpares: untagged cells (null context or displaced owners). Stack = array-backed, so Push/Pop
        //                allocate nothing — same GC profile as the original Stack pool. Misses consume these
        //                first so a measure sweep never cannibalises a neighbour's freshly-tagged cell.
        //
        // A LinkedList+dict version would have given the same lookups but allocated a node per Return, adding GC
        // pressure on fast scroll. This split avoids that entirely.
        private readonly Dictionary<object, SkiaControl> _byContext = new();
        private readonly Stack<SkiaControl> _freeSpares = new();

        private Stack<SkiaControl> _standalonePool = new();
        private readonly object _syncLock = new object();

        // Reused under _syncLock by GetViewWithMatchingBindingContext to hold scanned non-matches
        // while searching the pool top, so the search+remove is allocation-free on the hot path.
        private readonly List<SkiaControl> _matchScanBuffer = new(16);

        public TemplatedViewsPool(Func<object> initialViewModel, int maxSize, Action<IDisposable> dispose)
        {
            CreateTemplate = initialViewModel;
            MaxSize = maxSize;
            _dispose = dispose;
        }

        public int Size
        {
            get
            {
                lock (_syncLock)
                {
                    // Total size = generic reservoir + all height pools
                    int total = GenericCount;
                    foreach (var kvp in _heightPools)
                        total += kvp.Value.Count;
                    return total;
                }
            }
        }

        private int _createdCount;

        /// <summary>
        /// Total live cell instances created by this pool and not yet disposed by it — parked in the
        /// reservoirs PLUS rented out (in use). This, not <see cref="Size"/> (parked only), is what
        /// <see cref="MaxSize"/> really caps. Gating creation on Size let totals grow unbounded while
        /// cells were rented ("cells 198/320" with a nominal cap of 200): every creation gate now uses
        /// CreatedCount, and returns that would keep more instances alive than MaxSize are disposed.
        /// </summary>
        public int CreatedCount => Volatile.Read(ref _createdCount);

        public int MaxDistinctHeights
        {
            get => _maxDistinctHeights;
            set
            {
                lock (_syncLock)
                {
                    _maxDistinctHeights = value;
                }
            }
        }

        // ---- Generic reservoir helpers (must be called under _syncLock) --------------------------------

        private int GenericCount => _freeSpares.Count + _byContext.Count;

        /// <summary>
        /// Returns a cell to the generic reservoir. A cell carrying a BindingContext is indexed by it so a later
        /// Get(thatContext) hands back this exact (already-measured) cell. A null-context cell is a free spare.
        /// </summary>
        private void GenericPush(SkiaControl view)
        {
            if (view == null)
                return;

            var ctx = view.BindingContext;
            if (ctx != null)
            {
                // Rare (duplicate data item / realize race): a different cell already owns this context. The
                // displaced cell loses its key and becomes a free spare; latest cell wins the key.
                if (_byContext.TryGetValue(ctx, out var prev) && !ReferenceEquals(prev, view))
                {
                    _freeSpares.Push(prev);
                }

                _byContext[ctx] = view;
            }
            else
            {
                _freeSpares.Push(view);
            }
        }

        /// <summary>
        /// Pops the exact cell that was returned carrying <paramref name="bindingContext"/>, or null. O(1).
        /// </summary>
        private SkiaControl GenericTryPopContext(object bindingContext)
        {
            if (bindingContext == null)
                return null;

            if (_byContext.Remove(bindingContext, out var view))
                return view;

            return null;
        }

        /// <summary>
        /// Pops a free spare (no measured work to lose); O(1), allocation-free. Only when no free spare exists
        /// (every pooled cell is tagged = live contexts exceed pool size = real starvation) does it evict a
        /// tagged cell. With the intended config (pool 205 > ~150 items) free spares always exist, so this never
        /// destroys measured work.
        /// </summary>
        private SkiaControl GenericPopAny()
        {
            while (_freeSpares.Count > 0)
            {
                var v = _freeSpares.Pop();
                if (v != null)
                    return v;
            }

            // Starvation: evict one tagged cell. Dictionary enumerator is a struct -> no allocation.
            foreach (var kvp in _byContext)
            {
                _byContext.Remove(kvp.Key);
                return kvp.Value;
            }

            return null;
        }

        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Searches for a view with matching bindingContext in the stack (height pools only).
        /// Limited search depth for performance. The generic reservoir uses the O(1) context index instead.
        /// </summary>
        private SkiaControl GetViewWithMatchingBindingContext(Stack<SkiaControl> stack, object bindingContext)
        {
            if (stack == null || stack.Count == 0 || bindingContext == null)
                return null;

            // Search only the top items for performance (or full stack if smaller).
            // Pop into a reused buffer instead of allocating ToArray/ToList per call: this keeps the
            // hot path (called per appearing cell) allocation-free. Bounded to searchLimit ops.
            var searchLimit = Math.Min(10, stack.Count);

            SkiaControl found = null;
            _matchScanBuffer.Clear();

            for (int i = 0; i < searchLimit; i++)
            {
                var view = stack.Pop();

                // Reference equality only: matching by value (overridden Equals) could falsely pair
                // two distinct data items onto one recycled cell. We only want the literal same instance.
                if (found == null && view != null && !view.IsDisposed &&
                    ReferenceEquals(view.BindingContext, bindingContext))
                {
                    found = view; // removed = not pushed back
                }
                else
                {
                    _matchScanBuffer.Add(view);
                }
            }

            // Restore the scanned non-matches preserving original top-down order.
            for (int i = _matchScanBuffer.Count - 1; i >= 0; i--)
            {
                stack.Push(_matchScanBuffer[i]);
            }

            _matchScanBuffer.Clear();
            return found; // null if no matching instance found
        }

        protected virtual void Dispose(bool disposing)
        {
            lock (_syncLock)
            {
                IsDisposing = true;
                if (!_disposed && disposing)
                {
                    DisposeGeneric();
                    foreach (var kvp in _heightPools)
                    {
                        DisposeStack(kvp.Value);
                    }

                    Interlocked.Exchange(ref _createdCount, 0);
                    _disposed = true;
                }
            }
        }

        private void DisposeGeneric()
        {
            foreach (var c in _byContext.Values)
            {
                if (c != null)
                {
                    c.ContextIndex = -1;
                    c.Dispose();
                }
            }
            _byContext.Clear();

            DisposeStack(_freeSpares);
        }

        private void DisposeStack(Stack<SkiaControl> stack)
        {
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                if (c != null)
                {
                    c.ContextIndex = -1;
                    c.Dispose();
                }
            }
        }

        public void Dispose()
        {
            IsDisposing = true;
            Dispose(true);
        }

        SkiaControl CreateFromTemplate()
        {
            var created = CreateFromTemplateUncounted();
            if (created != null)
            {
                Interlocked.Increment(ref _createdCount);
            }

            return created;
        }

        // Instantiates a cell WITHOUT touching CreatedCount — used by the reserved-slot creation path
        // (see TryReserveCreateSlot/CreateReserved) where the slot was already claimed up front.
        SkiaControl CreateFromTemplateUncounted()
        {
            if (IsDisposing)
                return null;

            var create = CreateTemplate();

            if (create != null)
            {
                if (ViewsAdapter.LogEnabled)
                    Super.Log("[ViewsAdapter] created new view !");

                if (create is SkiaControl element)
                {
                    return element;
                }

                var ctrl = (SkiaControl)create;
                ctrl.ContextIndex = -1;
                return ctrl;
            }

            return null;
        }

        /// <summary>
        /// Atomically claims a creation slot against <see cref="MaxSize"/> WITHOUT holding _syncLock:
        /// increment-then-check keeps the cap exact under concurrent claimers (warm-up fill, prep worker,
        /// render thread). Pair with <see cref="CreateReserved"/>. This is what lets the EXPENSIVE cell
        /// ctor run outside the pool lock: holding _syncLock through a ctor (10-1000ms on Debug) made the
        /// render thread's per-frame Get()/Return() queue behind every background warm-up creation — the
        /// multi-second "hard UI lock" right after the first paint of a list with a large prefill.
        /// </summary>
        private bool TryReserveCreateSlot()
        {
            if (Interlocked.Increment(ref _createdCount) > MaxSize)
            {
                Interlocked.Decrement(ref _createdCount);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates a cell for a slot claimed by <see cref="TryReserveCreateSlot"/>, OUTSIDE the pool lock.
        /// Releases the slot when creation fails or throws.
        /// </summary>
        private SkiaControl CreateReserved()
        {
            SkiaControl view = null;
            try
            {
                view = CreateFromTemplateUncounted();
            }
            finally
            {
                if (view == null)
                    Interlocked.Decrement(ref _createdCount);
            }

            return view;
        }

        // Dispose a cell this pool created (deferred via the host's dispose action) keeping the created
        // total honest. All pool-side disposals of counted cells must go through here.
        private void DisposeCounted(SkiaControl view)
        {
            if (view == null)
                return;

            Interlocked.Decrement(ref _createdCount);
            view.ContextIndex = -1;
            if (_dispose != null)
                _dispose.Invoke(view);
            else
                view.Dispose();
        }

        public void Reserve()
        {
            if (IsDisposing)
                return;

            if (!TryReserveCreateSlot())
                return;

            // Cell ctor runs OUTSIDE _syncLock (see TryReserveCreateSlot doc) — the reservoir lock is
            // taken only for the microsecond push, so the render thread's Get/Return never wait a ctor.
            SkiaControl view;
            try
            {
                view = CreateReserved();
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
                throw;
            }

            if (view == null)
                return;

            lock (_syncLock)
            {
                if (IsDisposing)
                {
                    DisposeCounted(view);
                    return;
                }

                GenericPush(view);
            }
        }

        public SkiaControl GetStandalone()
        {
            lock (_syncLock)
            {
                if (IsDisposing)
                    return null;

                if (_standalonePool.Count > 0)
                {
                    return _standalonePool.Pop();
                }

                var ret = CreateFromTemplate();
                ret.IsParentIndependent = true;
                return ret;
            }
        }

        public void ReturnStandalone(SkiaControl viewModel)
        {
            lock (_syncLock)
            {
                if (IsDisposing)
                {
                    _dispose?.Invoke(viewModel);
                    return;
                }

                _standalonePool.Push(viewModel);
            }
        }

        public void ClearStandalonePool()
        {
            lock (_syncLock)
            {
                while (_standalonePool.Count > 0)
                {
                    var ctrl = _standalonePool.Pop();
                    if (ctrl != null)
                    {
                        Interlocked.Decrement(ref _createdCount);
                        ctrl.ContextIndex = -1;
                        ctrl.Dispose();
                    }
                }
            }
        }

        public SkiaControl Get(float height = 0, object bindingContext = null)
        {
            lock (_syncLock)
            {
                if (IsDisposing)
                    return null;

                // Exact already-measured cell for this context, at any depth. This is the hot path in
                // RecyclingTemplate.Disabled and the whole reason the generic reservoir is context-indexed.
                if (bindingContext != null)
                {
                    var matchingView = GenericTryPopContext(bindingContext);
                    if (matchingView != null && !matchingView.IsDisposed)
                        return matchingView;
                }

                if (height == 0)
                {
                    var generic = GenericPopAny();
                    if (generic != null && !generic.IsDisposed)
                        return generic;

                    if (CreatedCount < MaxSize)
                        return CreateFromTemplate();

                    return null;
                }

                int hKey = (int)Math.Round(height);
                if (!_heightPools.TryGetValue(hKey, out var stack))
                {
                    if (_heightPools.Count < MaxDistinctHeights)
                    {
                        stack = new();
                        _heightPools[hKey] = stack;
                    }

                    var generic = GenericPopAny();
                    if (generic != null && !generic.IsDisposed)
                        return generic;
                }

                SkiaControl view = null;

                if (stack != null && stack.Count > 0)
                {
                    // Height pool still uses the bounded top-N scan (small pools, recycling modes only).
                    if (bindingContext != null)
                    {
                        view = GetViewWithMatchingBindingContext(stack, bindingContext);
                    }

                    // Fall back to normal pop if no match found
                    if (view == null)
                    {
                        view = stack.Pop();
                    }
                }
                else
                {
                    view = GenericPopAny();

                    //create new for size
                    if (view == null && CreatedCount < MaxSize)
                    {
                        view = CreateFromTemplate();
                    }
                }

                return view;
            }
        }

        /// <summary>
        /// Read-only peek at the pooled cell tagged with this exact BindingContext, or null. Does not
        /// remove it from the pool — used by the prepared-views pipeline to check cell readiness without
        /// renting (see <see cref="SkiaLayout.UsePreparedViews"/>).
        /// </summary>
        public SkiaControl PeekContext(object bindingContext)
        {
            if (bindingContext == null)
                return null;

            lock (_syncLock)
            {
                return _byContext.TryGetValue(bindingContext, out var view) ? view : null;
            }
        }

        /// <summary>
        /// Rents a cell for OFF-THREAD preparation (bind+measure ahead of scrolling): the exact cell tagged
        /// with this context, else a free spare, else a new instance while under MaxSize, else (pool full,
        /// every cell tagged) evicts a tagged cell whose context is DEAD per <paramref name="isTagEvictable"/> —
        /// typically a context that left a windowed source on a LoadMore trim; without reclaiming those the
        /// pool fills with dead-tagged cells and preparation deadlocks into permanent skeletons.
        /// Cells tagged by LIVE contexts are never evicted here: speculative (ahead/behind) preparation
        /// stealing another wanted context's prepared cell made that index unprepared again, which re-wanted
        /// it next pass and evicted a third — a self-sustaining eviction carousel that kept the preparation
        /// worker (and its Repaint per prep) running at ~400 preps/sec while the list sat IDLE.
        /// Returns null when nothing is safely available — the caller must then skip (no measure, no repaint).
        /// </summary>
        /// <param name="bindingContext">Data context the cell will be prepared for.</param>
        /// <param name="isTagEvictable">
        /// Judges a TAGGED cell's context: true = dead (safe to evict), false = still live (protected).
        /// Null = never evict tagged cells.
        /// </param>
        public SkiaControl GetForPreparation(object bindingContext, Func<object, bool> isTagEvictable = null)
        {
            if (bindingContext == null)
                return null;

            lock (_syncLock)
            {
                if (IsDisposing)
                    return null;

                var match = GenericTryPopContext(bindingContext);
                if (match != null && !match.IsDisposed)
                    return match;

                while (_freeSpares.Count > 0)
                {
                    var v = _freeSpares.Pop();
                    if (v != null && !v.IsDisposed)
                        return v;
                }

                if (CreatedCount < MaxSize)
                    return CreateFromTemplate();

                if (isTagEvictable != null)
                {
                    foreach (var kvp in _byContext)
                    {
                        if (isTagEvictable(kvp.Key))
                        {
                            _byContext.Remove(kvp.Key);
                            return kvp.Value;
                        }
                    }
                }

                return null;
            }
        }

        public void Return(SkiaControl viewModel, int hKey)
        {
            if (viewModel == null)
                return;

            lock (_syncLock)
            {
                if (IsDisposing)
                {
                    Interlocked.Decrement(ref _createdCount);
                    _dispose?.Invoke(viewModel);
                    return;
                }

                // ENFORCE the cap on returns too: parking every returned cell let the pool keep more
                // instances alive than MaxSize (a windowed source keeps returning cells of trimmed-out
                // contexts). Overflow is disposed here instead of parked — after a window trim the pool
                // SHRINKS back to the cap instead of hoarding a cell per context ever seen.
                if (Volatile.Read(ref _createdCount) > MaxSize)
                {
                    DisposeCounted(viewModel);
                    return;
                }

                if (hKey != 0)
                {
                    if (!_heightPools.TryGetValue(hKey, out var stack))
                    {
                        if (_heightPools.Count < MaxDistinctHeights)
                        {
                            stack = new Stack<SkiaControl>();
                            _heightPools[hKey] = stack;
                        }
                    }

                    if (stack != null)
                    {
                        stack.Push(viewModel);
                        return;
                    }
                }

                GenericPush(viewModel);
            }
        }


        /// <summary>
        /// Invalidate all views held inside all internal pools so they will be re-measured on next use.
        /// Does not remove or modify pool contents; only marks controls as needing measure/layout.
        /// </summary>
        public void InvalidateAll()
        {
            lock (_syncLock)
            {
                if (IsDisposing)
                    return;

                void InvalidateStack(Stack<SkiaControl> stack)
                {
                    if (stack == null || stack.Count == 0)
                        return;

                    foreach (var view in stack)
                    {
                        if (view != null && !view.IsDisposed && !view.IsDisposing)
                        {
                            view.InvalidateWithChildren();
                        }
                    }
                }

                foreach (var view in _byContext.Values)
                {
                    if (view != null && !view.IsDisposed && !view.IsDisposing)
                    {
                        view.InvalidateWithChildren();
                    }
                }

                InvalidateStack(_freeSpares);

                foreach (var kvp in _heightPools)
                {
                    InvalidateStack(kvp.Value);
                }

                InvalidateStack(_standalonePool);
            }
        }
    }
}
