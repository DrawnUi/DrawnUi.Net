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
        private int _maxDistinctHeights = 24; // or configurable

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

        public void Reserve()
        {
            if (IsDisposing)
                return;

            // MUST lock: the reservoir is shared with Get/Return (which lock _syncLock). Adding here without the
            // lock races their reads/removals on the background measure / tile-render threads and corrupts the
            // structures. Pre-warming a larger pool makes this frequent.
            lock (_syncLock)
            {
                if (IsDisposing)
                    return;

                if (GenericCount < MaxSize)
                {
                    try
                    {
                        GenericPush(CreateFromTemplate());
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine(e);
                        throw;
                    }
                }
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

                    if (Size < MaxSize)
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
                    if (view == null && Size < MaxSize)
                    {
                        view = CreateFromTemplate();
                    }
                }

                return view;
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
                    _dispose?.Invoke(viewModel);
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
