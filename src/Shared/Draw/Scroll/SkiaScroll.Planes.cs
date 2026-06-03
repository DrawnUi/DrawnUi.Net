#define TMP

using System.Collections.Immutable;
using System.Numerics;

/*
    When Scrolling Down:

    1. User scrolls down → currentScroll changes
    2. Real-time positioning: rectForward.Offset(0, currentScroll + PlaneForward.OffsetY)
    3. Trigger check: rectForward.MidY <= (Viewport.Height / 2)
    4. Swap triggered: SwapDown() rotates planes and repositions
    5. New positioning: PlaneForward.OffsetY = PlaneCurrent.OffsetY + _planeHeight
    6. Background preparation: New forward plane gets rendered with content for the "next" scroll area

    The positioning is continuous and automatic - planes are always positioned relative to the current scroll
    position plus their individual offsets, ensuring seamless infinite scrolling!

 */

namespace DrawnUi.Draw
{

    public partial class SkiaScroll
    {
        //todo complete and move

        public const string PlaneRed = "Red";
        public const string PlaneGreen = "Greeen";
        public const string PlaneBlue = "Blue";

        private bool _childrenNeedRedraw = false;

        public override bool NeedMeasure
        {
            get
            {
                return base.NeedMeasure;
            }
            set
            {
                if (value)
                {
                    Debug.WriteLine("Scroll remeasured");
                }

                base.NeedMeasure = value;
            }
        }

        public override void InvalidateInternal()
        {
            base.InvalidateInternal();

            InvalidatePlanes();
        }

        private bool _buildingPlaneWindow;

        protected virtual void InvalidatePlanes()
        {
            // Measuring a plane's window must not invalidate the planes (would re-prepare every frame).
            if (_buildingPlaneWindow)
                return;

            PlaneCurrent?.Invalidate();
            PlaneBackward?.Invalidate();
            PlaneForward?.Invalidate();
        }

        protected virtual void DisposeVirtualPlanes()
        {
            foreach (var state in _planeBuildStates.Values)
            {
                state.Cts?.Cancel();
            }

            foreach (var planeLock in _planeLocks.Values)
            {
                planeLock.Wait();
            }

            try
            {
                foreach (var state in _planeBuildStates.Values)
                {
                    state.IsBuilding = false;
                    state.Cts?.Dispose();
                    state.Cts = null;
                }

                DisposePlane(PlaneCurrent);
                DisposePlane(PlaneForward);
                DisposePlane(PlaneBackward);

                PlaneCurrent = null;
                PlaneForward = null;
                PlaneBackward = null;
            }
            finally
            {
                foreach (var planeLock in _planeLocks.Values)
                {
                    planeLock.Release();
                }
            }
        }

        private void DisposePlane(Plane plane)
        {
            if (plane == null)
            {
                return;
            }

            plane.IsReady = false;

            if (plane.CachedObject != null)
            {
                DisposeObject(plane.CachedObject);
                plane.CachedObject = null;
            }

            if (plane.Surface != null)
            {
                DisposeObject(plane.Surface);
                plane.Surface = null;
            }
        }


        public override void UpdateByChild(SkiaControl control)
        {
            if (UseVirtual)
            {
                // Just set a flag - we'll determine which planes need invalidation during painting
                if (HasDirtyChildren())
                {
                    NeedMeasure = true;
                    _childrenNeedRedraw = true;
                    Content.ClearDirtyChildren();
                    //Update();
                }
                //Update(); // Trigger redraw
                return;
            }

            base.UpdateByChild(control);
        }

        /// <summary>
        /// Check dirty children and invalidate specific planes that contain them
        /// </summary>
        protected virtual void CheckAndInvalidateDirtyPlanes()
        {
            if (Content == null)
                return;
                
            var dirtyChildren = Content.DirtyChildrenTracker;
            if (dirtyChildren == null || dirtyChildren.IsEmpty)
                return;

            var planesToInvalidate = new HashSet<Plane>();
            
            // For each dirty child, find which plane contains it
            foreach (var dirtyChild in dirtyChildren.GetList())
            {
                var plane = FindPlaneContainingChild(dirtyChild);
                if (plane != null)
                {
                    planesToInvalidate.Add(plane);
                }
            }
            
            // Invalidate only the planes that contain dirty children
            foreach (var plane in planesToInvalidate)
            {
                plane.Invalidate();
                Debug.WriteLine($"[PLANES] Invalidated plane {plane.Id} due to dirty children");
            }
            
            // Clear the dirty children tracker
            dirtyChildren.Clear();
        }

        protected virtual bool HasDirtyChildren()
        {
            if (Content == null)
                return false;

            var dirtyChildren = Content.DirtyChildrenTracker;
            if (dirtyChildren == null || dirtyChildren.IsEmpty)
                return false;

            var planesToInvalidate = new HashSet<Plane>();

            // For each dirty child, find which plane contains it
            foreach (var dirtyChild in dirtyChildren.GetList())
            {
                var plane = FindPlaneContainingChild(dirtyChild);
                if (plane != null)
                {
                    return true;
                    planesToInvalidate.Add(plane);
                }
            }

            //// Invalidate only the planes that contain dirty children
            //foreach (var plane in planesToInvalidate)
            //{
            //    plane.Invalidate();
            //    Debug.WriteLine($"[PLANES] Invalidated plane {plane.Id} due to dirty children");
            //}

            //// Clear the dirty children tracker
            //dirtyChildren.Clear();

            return false;
        }

        /// <summary>
        /// Determines which plane contains the given child control by checking render trees
        /// </summary>
        protected virtual Plane FindPlaneContainingChild(SkiaControl control)
        {
            // Check each plane's render tree for the invalidated child
            var planes = new[] { PlaneCurrent, PlaneForward, PlaneBackward };

            foreach (var plane in planes)
            {
                if (plane?.RenderTree != null)
                {
                    // Check if this plane's render tree contains the invalidated control
                    for (int i = 0; i < plane.RenderTree.Count; i++)
                    {
                        if (plane.RenderTree[i].Control == control)
                        {
                            return plane;
                        }
                    }
                }
            }

            return null; // Child not found in any plane's render tree
        }


        //todo use when context size is bigger than 2 viewports?
        public virtual bool UseVirtual
        {
            get
            {
                return Content != null
                       && Orientation != ScrollOrientation.Both && Content is SkiaLayout layout &&
                       layout.Virtualisation == VirtualisationType.Managed;
            }
        }


        protected Plane PlaneCurrent { get; set; }
        protected Plane PlaneForward { get; set; }
        protected Plane PlaneBackward { get; set; }
        protected int _planeWidth;
        protected int _planeHeight;
        protected int _planePrepareThreshold;
        private float swappedDownAt;
        private float swappedUpAt;

        public override ScaledRect GetOnScreenVisibleArea(DrawingContext context, Vector2 inflateByPixels = default)
        {
            if (UseVirtual)
            {
                //todo
                if (context.GetArgument(ContextArguments.Viewport.ToString()) is SKRect insideViewport)
                {
                    //we can return the plane rect
                    //Debug.WriteLine($"UsePlanes area: {insideViewport}");
                    return ScaledRect.FromPixels(insideViewport, _zoomedScale);
                }

                var measurePlaneArea = context.GetArgument("InitialMeasureVisibleArea") as bool? == true;

                var initialViewport = ContentViewport.Pixels;
                if (initialViewport.IsEmpty)
                {
                    initialViewport = Viewport.Pixels;
                }
                if (initialViewport.IsEmpty)
                {
                    initialViewport = DrawingRect;
                }

                if (measurePlaneArea && !initialViewport.IsEmpty)
                {
                    var planeWidth = _planeWidth > 0
                        ? _planeWidth
                        : (int)Math.Ceiling(initialViewport.Width);
                    var planeHeight = _planeHeight > 0
                        ? _planeHeight
                        : Orientation == ScrollOrientation.Horizontal
                            ? (int)Math.Ceiling(initialViewport.Height)
                            : (int)Math.Ceiling(initialViewport.Height * 2f);

                    if (Orientation == ScrollOrientation.Horizontal)
                    {
                        planeWidth = _planeWidth > 0
                            ? _planeWidth
                            : (int)Math.Ceiling(initialViewport.Width * 2f);
                    }

                    var planeViewport = new SKRect(
                        initialViewport.Left,
                        initialViewport.Top,
                        initialViewport.Left + planeWidth,
                        initialViewport.Top + planeHeight);

                    planeViewport.Inflate(inflateByPixels.X, inflateByPixels.Y);
                    return ScaledRect.FromPixels(planeViewport, _zoomedScale);
                }

                if (!initialViewport.IsEmpty)
                {
                    initialViewport.Inflate(inflateByPixels.X, inflateByPixels.Y);
                    return ScaledRect.FromPixels(initialViewport, _zoomedScale);
                }

                return ScaledRect.FromPixels(context.Destination, _zoomedScale);
            }

            if (Virtualisation != VirtualisationType.Disabled) //true by default
            {
                //passing visible area to be rendered
                //when scrolling we will pass changed area to be rendered
                //most suitable for large content
                var inflated = ContentViewport.Pixels;
                if (ContentViewport.Pixels.IsEmpty)
                {
                    var initialViewport = Viewport.Pixels;
                    if (initialViewport.IsEmpty)
                    {
                        initialViewport = DrawingRect;
                    }

                    if (!initialViewport.IsEmpty)
                    {
                        initialViewport.Inflate(inflateByPixels.X, inflateByPixels.Y);
                        return ScaledRect.FromPixels(initialViewport, RenderingScale);
                    }

                    return ContentRectWithOffset; // last-resort fallback before viewport is initialized
                }
                inflated.Inflate(inflateByPixels.X, inflateByPixels.Y);
                return ScaledRect.FromPixels(inflated, RenderingScale);
            }
            else
            {
                //passing the whole area to be rendered.
                //when scrolling we will just translate it
                //most suitable for small content
                return ContentRectWithOffset;

                //absoluteViewPort = new SKRect(Viewport.Pixels.Left, Viewport.Pixels.Top,
                //    Viewport.Pixels.Left + ContentSize.Pixels.Width, Viewport.Pixels.Top + ContentSize.Pixels.Height);
            }
        }


        public virtual void InitializePlanes()
        {
            var viewportWidth = Viewport.Pixels.Width;
            var viewportHeight = Viewport.Pixels.Height;

            // Ensure the planes cover twice the viewport area
            _planeWidth = (int)(viewportWidth); //for vertical, todo all orientations
            _planeHeight = (int)(viewportHeight * 2);
            _planePrepareThreshold = (int)(_planeHeight / 2);

            float offsetX = 0, offsetY = 0;

            if (Orientation == ScrollOrientation.Vertical)
            {
                offsetY = _planeHeight;
            }
            else if (Orientation == ScrollOrientation.Horizontal)
            {
                offsetX = _planeWidth;
            }

            PlaneCurrent = new Plane
            {
                Id = PlaneRed,
                Surface = SKSurface.Create(new SKImageInfo(_planeWidth, _planeHeight)),
                BackgroundColor = SKColors.Red,
                Destination = new(0, 0, _planeWidth, _planeHeight)
            };

            PlaneForward = new Plane
            {
                Id = PlaneGreen,
                OffsetX = offsetX,
                OffsetY = offsetY,
                Surface = SKSurface.Create(new SKImageInfo(_planeWidth, _planeHeight)),
                Destination = new(0, 0, _planeWidth, _planeHeight),
                BackgroundColor = SKColors.Green,
            };

            PlaneBackward = new Plane
            {
                Id = PlaneBlue,
                OffsetX = -offsetX,
                OffsetY = -offsetY,
                Surface = SKSurface.Create(new SKImageInfo(_planeWidth, _planeHeight)),
                Destination = new(0, 0, _planeWidth, _planeHeight),
                BackgroundColor = SKColors.Blue,
            };

        }
  

        private int visibleAreaCaller = 0;
        private bool _availablePlaneC;
        private bool _availablePlaneB;
  
        private readonly Dictionary<string, PlaneBuildState> _planeBuildStates
            = new Dictionary<string, PlaneBuildState>
            {
                { PlaneRed, new PlaneBuildState() },
                { PlaneGreen, new PlaneBuildState() },
                { PlaneBlue, new PlaneBuildState() }
            };

        private class PlaneBuildState
        {
            public bool IsBuilding;
            public CancellationTokenSource Cts;
        }

        private static readonly SemaphoreSlim _globalPlanePreparationLock = new(1, 1);

        protected void TriggerPreparePlane(DrawingContext context, string planeId)
        {
            if (!_planeBuildStates.TryGetValue(planeId, out var state))
            {
                Debug.WriteLine($"Unknown planeId: {planeId}");
                return;
            }

            // If this plane is already building, cancel the previous job
            if (state.IsBuilding && state.Cts != null)
            {
                state.Cts.Cancel(); // signal old task to stop
                //Debug.WriteLine($"Canceling previous rendering: {planeId}");
            }

            // Create a fresh CTS and mark building
            state.Cts?.Dispose();
            state.Cts = new CancellationTokenSource();
            state.IsBuilding = true;
            var token = state.Cts.Token;

            var clone = context; //always clone struct from arguments for another thread!
            Task.Run(async () =>
            {
                try
                {
                    if (token.IsCancellationRequested)
                    {
                        //Debug.WriteLine($"Plane rendering canceled: {planeId}");
                        return; // canceled before starting
                    }

                    await _planeLocks[planeId].WaitAsync(token);

                    // Now do the actual PreparePlane
                    var plane = GetPlaneById(planeId);
                    //Debug.WriteLine($"Run prepare plane {plane?.Id}");

                    await _globalPlanePreparationLock.WaitAsync(token);

                    PreparePlane(clone.WithArgument(new("BThread", true)), plane);

                    if (plane.IsReady)
                    {
                        Repaint();
                    }
                }
                catch (OperationCanceledException)
                {
                    //Debug.WriteLine($"Plane rendering canceled: {planeId}");
                    // Normal if we got canceled
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error building plane {planeId}: {ex}");
                }
                finally
                {
                    _globalPlanePreparationLock.Release();
                    state.IsBuilding = false;
                    _planeLocks[planeId].Release();
                }
            }, token).ConfigureAwait(false);
        }





        /// <summary>Read-only snapshot of a plane's captured content, for diagnostics/testing.</summary>
        public readonly record struct PlaneContentInfo(string Id, bool IsReady, int Count, int MinIndex, int MaxIndex, float OffsetY);

        /// <summary>
        /// Returns the captured item-index coverage of the current, forward and backward planes.
        /// A plane spanning two viewports should capture roughly two viewports' worth of items; a much
        /// smaller range means its far half is empty (unmeasured content at prepare time).
        /// </summary>
        public PlaneContentInfo[] GetPlanesContentInfo()
        {
            PlaneContentInfo Build(Plane p)
            {
                if (p == null)
                    return new PlaneContentInfo("<null>", false, 0, -1, -1, 0);

                int count = 0, min = int.MaxValue, max = -1;
                var rt = p.RenderTree;
                if (rt != null)
                {
                    for (int i = 0; i < rt.Count; i++)
                    {
                        var c = rt[i].Control;
                        if (c == null) continue;
                        count++;
                        int idx = c.ContextIndex;
                        if (idx >= 0)
                        {
                            if (idx < min) min = idx;
                            if (idx > max) max = idx;
                        }
                    }
                }

                return new PlaneContentInfo(p.Id, p.IsReady, count, min == int.MaxValue ? -1 : min, max, p.OffsetY);
            }

            return new[] { Build(PlaneCurrent), Build(PlaneForward), Build(PlaneBackward) };
        }

        protected virtual Plane GetPlaneById(string planeId)
        {
            return planeId switch
            {
                PlaneGreen => PlaneForward,
                PlaneBlue => PlaneBackward,
                PlaneRed => PlaneCurrent,
                _ => throw new ArgumentException("Invalid plane ID", nameof(planeId))
            };
        }

        private readonly Dictionary<string, SemaphoreSlim> _planeLocks
            = new Dictionary<string, SemaphoreSlim>
            {
                { PlaneRed, new SemaphoreSlim(1, 1) },
                { PlaneGreen, new SemaphoreSlim(1, 1) },
                { PlaneBlue, new SemaphoreSlim(1, 1) }
            };


        /// <summary>
        /// Viewport scrolled
        /// </summary>
        protected virtual void OnScrolledForPlanes()
        {
            _availablePlaneB = true;

            if (Content is SkiaLayout layout && layout.IsTemplated
                                             && layout.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible
                                             && layout.LastMeasuredIndex < layout.ItemsSource.Count)
            {
                var measuredEnd = layout.GetMeasuredContentEnd();

                double currentOffset = Orientation == ScrollOrientation.Vertical
                    ? -ViewportOffsetY
                    : -ViewportOffsetX;

                if (measuredEnd - currentOffset < 0)
                {
                    TriggerIncrementalMeasurement(layout);
                }

            }
        }

        protected void OrderToPreparePlaneForwardInBackground(DrawingContext context)
        {
            if (_planeBuildStates[PlaneGreen].IsBuilding
                //|| !_availablePlaneB
                || PlaneForward == null
                || PlaneForward.IsReady
                || ViewportOffsetY == 0)
            {
                return;
            }

            //Debug.WriteLine($"Preparing PLANE {PlaneGreen}..");

            // Capture current viewport state to avoid race conditions
            var capturedOffset = InternalViewportOffset.Pixels;
            var capturedContext = context.WithArgument(new(nameof(ContextArguments.Offset), capturedOffset));

            TriggerPreparePlane(capturedContext, PlaneGreen);

            //_availablePlaneB = false;
        }

        protected void OrderToPreparePlaneBackwardInBackground(DrawingContext context)
        {
            if (_planeBuildStates[PlaneBlue].IsBuilding
                //|| !_availablePlaneB                          
                || PlaneBackward == null
                || PlaneBackward.IsReady
                || ViewportOffsetY >= 0)
            {
                return;
            }

            //Debug.WriteLine($"Preparing PLANE {PlaneBlue}..");

            // Capture current viewport state to avoid race conditions
            var capturedOffset = InternalViewportOffset.Pixels;
            var capturedContext = context.WithArgument(new(nameof(ContextArguments.Offset), capturedOffset));

            TriggerPreparePlane(capturedContext, PlaneBlue);
        }

        /// <summary>
        /// Determines if we should swap down based on visual position and content boundaries
        /// </summary>
        protected virtual bool ShouldSwapDown(SKRect rectForward)
        {
            // Original visual trigger: forward plane center reaches viewport center
            bool visualTrigger = rectForward.MidY <= (Viewport.Pixels.Height / 2) + DrawingRect.Top;

            // Content boundary trigger: at end of content and forward plane is becoming visible
            bool contentBoundaryTrigger = false;

            if (Content is SkiaLayout layout && layout.IsTemplated &&
                layout.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
            {
                // Check if we've measured all content and forward plane is entering viewport
                bool atContentEnd = layout.LastMeasuredIndex >= layout.ItemsSource.Count - 1;
                bool forwardPlaneEntering = rectForward.Top < Viewport.Pixels.Height * 0.8f; // Trigger slightly earlier

                contentBoundaryTrigger = atContentEnd && forwardPlaneEntering;
            }

            return visualTrigger || contentBoundaryTrigger;
        }

        /// <summary>
        /// Determines if we should swap up based on visual position and content boundaries  
        /// </summary>
        protected virtual bool ShouldSwapUp(SKRect rectBackward)
        {
            // Original visual trigger: backward plane center crosses viewport center
            bool visualTrigger = rectBackward.MidY > Viewport.Pixels.Height / 2 + DrawingRect.Top;

            // Content boundary trigger: at start of content and backward plane is becoming visible
            bool contentBoundaryTrigger = false;

            if (Content is SkiaLayout layout && layout.IsTemplated &&
                layout.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
            {
                // Check if we're at content start and backward plane is entering viewport
                bool atContentStart = layout.FirstMeasuredIndex <= 0;
                bool backwardPlaneEntering =
                    rectBackward.Bottom > Viewport.Pixels.Height * 0.2f; // Trigger slightly earlier

                contentBoundaryTrigger = atContentStart && backwardPlaneEntering;
            }

            return visualTrigger || contentBoundaryTrigger;
        }

        // ===================================================================================
        // TILED PLANES (redesign): the content is split into FIXED tiles of _planeHeight each.
        // Tile N always covers content band [N*_planeHeight .. (N+1)*_planeHeight]. Each tile is
        // rendered once to a bitmap (in the background for non-current tiles) and then SCROLLING
        // only blits the bitmaps at (N*_planeHeight - scroll). No swaps, no floating plane offsets,
        // no desync -> smooth blit-only scrolling, correct content at any position.
        // ===================================================================================

        private readonly Dictionary<int, Plane> _tiles = new();
        private readonly HashSet<int> _tileBuilding = new();
        private readonly Stack<SKSurface> _tileSurfacePool = new();
        private const int TileBufferEachSide = 1;   // tiles kept above/below the visible range
        private float _tileSlot;                     // cached avg item slot (height + spacing)

        /// <summary>
        /// Debug aid: when true each tile's bitmap is tinted with a cycling color (red/green/blue/...)
        /// so the tile boundaries are visible while scrolling. Cell content draws on top of the tint.
        /// </summary>
        public bool DebugShowPlanes { get; set; }

        private static readonly SKColor[] _debugPlaneColors =
        {
            new SKColor(255, 0, 0),     // red
            new SKColor(0, 180, 0),     // green
            new SKColor(0, 80, 255),    // blue
            new SKColor(255, 180, 0),   // amber
        };

        protected virtual void InvalidateTiles()
        {
            foreach (var tile in _tiles.Values)
                tile.Invalidate();
        }

        private SKSurface RentTileSurface()
        {
            if (_tileSurfacePool.Count > 0)
                return _tileSurfacePool.Pop();
            return SKSurface.Create(new SKImageInfo(_planeWidth, _planeHeight));
        }

        private Plane GetOrCreateTile(int index)
        {
            if (_tiles.TryGetValue(index, out var existing))
                return existing;

            var tile = new Plane
            {
                Id = $"T{index}",
                OffsetY = index * _planeHeight,    // fixed content band top
                OffsetX = 0,
                Surface = RentTileSurface(),
                Destination = new SKRect(0, 0, _planeWidth, _planeHeight),
                BackgroundColor = SKColors.Transparent,
            };
            _tiles[index] = tile;
            return tile;
        }

        private void EvictTilesOutside(int keepFirst, int keepLast)
        {
            if (_tiles.Count == 0)
                return;
            List<int> remove = null;
            foreach (var kv in _tiles)
            {
                if (kv.Key < keepFirst || kv.Key > keepLast)
                    (remove ??= new()).Add(kv.Key);
            }
            if (remove == null)
                return;
            foreach (var idx in remove)
            {
                if (_tileBuilding.Contains(idx))
                    continue; // don't evict a tile being rendered
                var tile = _tiles[idx];
                _tiles.Remove(idx);
                if (tile.CachedObject != null)
                {
                    DisposeObject(tile.CachedObject);
                    tile.CachedObject = null;
                }
                if (tile.Surface != null && _tileSurfacePool.Count < 8)
                    _tileSurfacePool.Push(tile.Surface);
                else if (tile.Surface != null)
                    DisposeObject(tile.Surface);
                tile.Surface = null;
            }
        }

        /// <summary>
        /// Renders a tile's FIXED content band [tile.OffsetY .. +_planeHeight] into its bitmap, in the
        /// absolute content frame (translate by -bandTop so the band maps onto the tile surface origin).
        /// </summary>
        private void RenderTile(DrawingContext context, Plane tile)
        {
            if (tile.Surface == null || Content is not SkiaLayout layout)
                return;

            float scale = Math.Max(0.0001f, (float)RenderingScale);
            double bandTop = tile.OffsetY;
            double bandBottom = bandTop + _planeHeight;

            var recordingContext = context.CreateForRecordingImage(tile.Surface, new SKSize(_planeWidth, _planeHeight));
            var canvas = recordingContext.Context.Canvas;
            int save = canvas.Save();
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(0, -(float)bandTop);   // content Y -> surface Y (band top -> 0)

            var window = layout.BuildPlaneWindowStructure(bandTop, bandBottom, scale, _planeWidth);
            layout.PlaneOverrideStructure = window;
            _buildingPlaneWindow = true;
            try
            {
                // Destination.Top = 0 -> a cell's drawn Y equals its content-from-0 Y (index * slot);
                // the visibility area is the band, so only this band's cells are drawn.
                var dest = new SKRect(0, 0, _planeWidth, (float)bandBottom);
                var bandViewport = new SKRect(0, (float)bandTop, _planeWidth, (float)bandBottom);

                // IMPORTANT: SkiaScroll.PaintViews re-applies the destination from the Rect argument
                // (ContentRectWithOffset, scroll baked in). Override it with the band-aligned rect so the
                // cells draw at their absolute content Y (origin 0) and our -bandTop translate lands them
                // on the tile surface. Without this the scroll offset shifts cells out of the band -> empty.
                PaintOnPlane(recordingContext.WithDestination(dest)
                    .WithArguments(
                        new(nameof(ContextArguments.Rect), dest),
                        new(nameof(ContextArguments.Plane), tile.Id),
                        new(nameof(ContextArguments.Viewport), bandViewport)), tile);
                // Note: no RenderTree capture here — tile cells are recycled immediately, so gestures
                // realize their own live cell on demand (see ProcessGesturesForTiles) instead.
            }
            finally
            {
                // Release WHILE PlaneOverrideStructure is still set so GetSizeKey routes returns to the
                // generic bucket (symmetric with the Gets). DrawStack PASS2 left the cells it drew in-use;
                // MarkViewAsHidden returns them to the pool. Cells PASS1 already hid are skipped (no-op).
                if (window != null)
                    foreach (var cell in window.GetChildrenAsSpans())
                        if (cell != null)
                            layout.ChildrenFactory.MarkViewAsHidden(cell.ControlIndex);

                _buildingPlaneWindow = false;
                layout.PlaneOverrideStructure = null;
            }

            if (DebugShowPlanes)
            {
                // Overlay (drawn over content, semi-transparent) so each tile's color + border is clearly
                // visible while scrolling. Canvas is still translated by -bandTop, so use content coords.
                int tileIndex = _planeHeight > 0 ? (int)Math.Round(bandTop / _planeHeight) : 0;
                var color = _debugPlaneColors[tileIndex % _debugPlaneColors.Length];
                var rect = new SKRect(0, (float)bandTop, _planeWidth, (float)bandBottom);
                using (var fill = new SKPaint { Color = color.WithAlpha(45), Style = SKPaintStyle.Fill })
                    canvas.DrawRect(rect, fill);
                using (var border = new SKPaint { Color = color, Style = SKPaintStyle.Stroke, StrokeWidth = 8 })
                    canvas.DrawRect(rect, border);
            }

            canvas.RestoreToCount(save);
            canvas.Flush();

            DisposeObject(tile.CachedObject);
            tile.CachedObject = new CachedObject(
                SkiaCacheType.Image,
                tile.Surface,
                new SKRect(0, 0, _planeWidth, _planeHeight),
                new SKRect(0, 0, _planeWidth, _planeHeight)) { PreserveSourceFromDispose = true };

            tile.IsReady = true;
        }

        private void TriggerRenderTileBackground(DrawingContext context, int index, Plane tile)
        {
            if (_tileBuilding.Contains(index))
                return;
            _tileBuilding.Add(index);

            var clone = context;
            Task.Run(async () =>
            {
                try
                {
                    await _globalPlanePreparationLock.WaitAsync();
                    if (_tiles.TryGetValue(index, out var t) && ReferenceEquals(t, tile)
                        && t.Surface != null && !t.IsReady)
                    {
                        RenderTile(clone, t);
                        Repaint();
                    }
                }
                catch (Exception ex)
                {
                    Super.Log($"Error rendering tile {index}: {ex}");
                }
                finally
                {
                    _globalPlanePreparationLock.Release();
                    _tileBuilding.Remove(index);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Tiled virtual rendering: blit fixed content tiles at scroll-translated positions, rendering
        /// missing tiles (current one synchronously, buffer tiles in the background).
        /// </summary>
        public virtual void DrawVirtual(DrawingContext context)
        {
            if (_childrenNeedRedraw)
            {
                Content?.ClearDirtyChildren();
                _childrenNeedRedraw = false;
                InvalidateTiles();
            }

            if (Content is not SkiaLayout layout || !layout.IsTemplated
                || layout.MeasureItemsStrategy != MeasuringStrategy.MeasureVisible
                || layout.ItemsSource == null || layout.ItemsSource.Count == 0)
                return;

            if (_planeHeight <= 0)
            {
                InitializePlanes();          // computes _planeWidth/_planeHeight from viewport
                // we do NOT use the legacy current/forward/backward planes in tiled mode
                if (_planeHeight <= 0)
                    return;
            }

            float scale = Math.Max(0.0001f, (float)RenderingScale);
            int itemsCount = layout.ItemsSource.Count;
            float avg = Math.Max(1f, layout.GetAverageItemHeightPixels(scale));
            float spacing = (float)(layout.Spacing * scale);
            _tileSlot = avg + spacing;
            float totalContent = _tileSlot * itemsCount;

            float tileH = _planeHeight;
            float scrollTop = -InternalViewportOffset.Pixels.Y;       // content-from-0 at viewport top
            float viewportH = Viewport.Pixels.Height;
            if (viewportH <= 0) viewportH = DrawingRect.Height;

            int maxTile = Math.Max(0, (int)Math.Floor(Math.Max(0, totalContent - 1) / tileH));
            int firstVisibleTile = Math.Max(0, (int)Math.Floor(scrollTop / tileH));
            int lastVisibleTile = Math.Min(maxTile, (int)Math.Floor((scrollTop + viewportH) / tileH));

            int firstTile = Math.Max(0, firstVisibleTile - TileBufferEachSide);
            int lastTile = Math.Min(maxTile, lastVisibleTile + TileBufferEachSide);

            var canvas = context.Context.Canvas;

            for (int t = firstTile; t <= lastTile; t++)
            {
                var tile = GetOrCreateTile(t);

                if (!tile.IsReady && !_tileBuilding.Contains(t))
                {
                    bool isVisible = t >= firstVisibleTile && t <= lastVisibleTile;
                    if (isVisible)
                        RenderTile(context, tile);                    // synchronous (must be on screen now)
                    else
                        TriggerRenderTileBackground(context, t, tile); // ahead-of-scroll, background
                }

                if (tile.IsReady && tile.CachedObject != null)
                {
                    float canvasY = DrawingRect.Top + (t * tileH - scrollTop);
                    tile.CachedObject.Draw(canvas, DrawingRect.Left, canvasY, null);
                    tile.LastDrawnAt = new SKRect(DrawingRect.Left, canvasY,
                        DrawingRect.Left + _planeWidth, canvasY + tileH);
                }
            }

            EvictTilesOutside(firstTile - TileBufferEachSide, lastTile + TileBufferEachSide);
        }



        // -----------------------------------------------------------
        // SWAP LOGIC
        // -----------------------------------------------------------
        private void SwapDown()
        {
            Debug.WriteLine(
                $"Swap DOWN: {PlaneForward.Id} becomes Current, {PlaneCurrent.Id} becomes Backward, {PlaneBackward.Id} becomes Forward");
            // forward ↑ current
            // current ↑ backward
            // backward ↓ forward + invalidate
            var temp = PlaneBackward;
            PlaneBackward = PlaneCurrent;
            PlaneCurrent = PlaneForward;
            PlaneForward = temp;
            PlaneForward.OffsetY = PlaneCurrent.OffsetY + _planeHeight;
            PlaneBackward.OffsetY = PlaneCurrent.OffsetY - _planeHeight;
            PlaneForward.Invalidate();
        }

        private void SwapUp()
        {
            Debug.WriteLine(
                $"Swap UP: {PlaneBackward.Id} becomes Current, {PlaneCurrent.Id} becomes Forward, {PlaneForward.Id} becomes Backward");
            var temp = PlaneForward;
            PlaneForward = PlaneCurrent;
            PlaneCurrent = PlaneBackward;
            PlaneBackward = temp;
            PlaneBackward.OffsetY = PlaneCurrent.OffsetY - _planeHeight;
            PlaneForward.OffsetY = PlaneCurrent.OffsetY + _planeHeight;
            PlaneBackward.Invalidate();
        }

        /// <summary>
        /// Calculate the specific viewport area this plane should render
        /// </summary>
        protected virtual SKRect CalculateViewportForPlane(Plane plane, SKPoint offsetToUse)
        {
            // Create a viewport that represents the area this plane should render
            var planeViewport = new SKRect(0, 0, _planeWidth, _planeHeight);
            
            // Apply the same offsets as the plane rendering
            planeViewport.Offset(offsetToUse.X, offsetToUse.Y);
            planeViewport.Offset(DrawingRect.Left, DrawingRect.Top);
            planeViewport.Offset(plane.OffsetX, plane.OffsetY);
            
            //Debug.WriteLine($"[{plane.Id}] Calculated plane-specific viewport: {planeViewport}");
            
            return planeViewport;
        }

        protected virtual void PreparePlane(DrawingContext context, Plane plane)
        {
            // Ensure content is properly measured before painting the plane
            //if (Content != null && Content.NeedMeasure)
            //{
            //    // Force re-measurement of the entire content to handle cases where
            //    // cells have !WasMeasured (strategy changes, global layout changes, etc.)
            //    var availableSize = ContentSize.Pixels;
            //    Content.Measure(availableSize.Width, availableSize.Height, RenderingScale);
            //}
            
            var destination = plane.Destination;

            var recordingContext = context.CreateForRecordingImage(plane.Surface, destination.Size);

            var viewport = plane.Destination;

            // Use captured offset from trigger time to avoid race conditions
            var capturedOffset = context.GetArgument(nameof(ContextArguments.Offset)) as SKPoint?;
            var offsetToUse = capturedOffset ?? InternalViewportOffset.Pixels;

            //if (capturedOffset.HasValue)
            //{
            //    Debug.WriteLine($"Using captured offset for {plane.Id}: {capturedOffset.Value}");
            //}
            //else
            //{
            //    Debug.WriteLine($"No captured offset for {plane.Id}, using current: {InternalViewportOffset.Pixels}");
            //}

            viewport.Offset(offsetToUse.X, offsetToUse.Y);
            viewport.Offset(DrawingRect.Left, DrawingRect.Top);
            viewport.Offset(plane.OffsetX, plane.OffsetY);

            // Calculate plane-specific viewport for managed virtualization
            var planeSpecificViewport = CalculateViewportForPlane(plane, offsetToUse);

            // Per-plane sliding window: realize only the items in THIS plane's content band (fits the
            // recycling pool) so any scroll position renders correct content. For forward/backward planes
            // PreparePlane runs on a background thread, so this measurement is off the render thread.
            SkiaLayout windowLayout = null;
            LayoutStructure windowStructure = null;
            if (Content is SkiaLayout planeLayout && planeLayout.IsTemplated
                && planeLayout.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible
                && planeLayout.ItemsSource != null && planeLayout.ItemsSource.Count > 0)
            {
                double bandTopPx = plane.OffsetY;
                double bandBottomPx = plane.OffsetY + _planeHeight;
                _buildingPlaneWindow = true;
                try
                {
                    windowStructure = planeLayout.BuildPlaneWindowStructure(bandTopPx, bandBottomPx,
                        (float)RenderingScale, _planeWidth);
                    planeLayout.PlaneOverrideStructure = windowStructure;
                }
                finally
                {
                    _buildingPlaneWindow = false;
                }
                windowLayout = planeLayout;
            }

            var c = recordingContext.Context.Canvas.Save();
            recordingContext.Context.Canvas.Translate(-viewport.Left, -viewport.Top);
            recordingContext.Context.Canvas.Clear(plane.BackgroundColor);

            try
            {
                PaintOnPlane(recordingContext
                    .WithDestination(viewport)
                    .WithArguments(
                        new(nameof(ContextArguments.Plane), plane.Id),
                        new(nameof(ContextArguments.Viewport), viewport),
                        new(nameof(ContextArguments.PlaneViewport), planeSpecificViewport)), plane);
            }
            finally
            {
                if (windowLayout != null)
                {
                    if (windowStructure != null)
                        foreach (var wc in windowStructure.GetChildrenAsSpans())
                            if (wc != null)
                                windowLayout.ChildrenFactory.MarkViewAsHidden(wc.ControlIndex);
                    windowLayout.PlaneOverrideStructure = null;
                }
            }

            recordingContext.Context.Canvas.RestoreToCount(c);

            // Capture rendering tree for gesture processing after content is painted
            if (Content is SkiaLayout layout && layout.RenderTree != null)
            {
                plane.CaptureRenderTree(layout.RenderTree, offsetToUse, plane.OffsetY);
                //Debug.WriteLine($"Captured render tree for {plane.Id}: {plane.RenderTree?.Count ?? 0} controls at offset {offsetToUse}, planeOffsetY: {plane.OffsetY}");
            }

            recordingContext.Context.Canvas.Flush();
            DisposeObject(plane.CachedObject);
            plane.CachedObject = new CachedObject(
                SkiaCacheType.Image,
                plane.Surface,
                new SKRect(0, 0, _planeWidth, _planeHeight),
                destination) { PreserveSourceFromDispose = true };

            plane.IsReady = true;
            //Debug.WriteLine($"Plane rendering READY: {plane.Id}");
        }



        protected virtual void PaintOnPlane(DrawingContext context, Plane plane)
        {
            PaintViews(context);
        }



        /// <summary>
        /// Check if gesture point intersects with plane's visible area
        /// </summary>
        protected virtual bool IsGestureInPlane(Plane plane, System.Drawing.PointF location)
        {
            var currentScroll = InternalViewportOffset.Pixels.Y;
            var planeRect = new SKRect(0, 0, _planeWidth, _planeHeight);
            planeRect.Offset(DrawingRect.Left, DrawingRect.Top);
            planeRect.Offset(0, currentScroll + plane.OffsetY);

            return ContentViewport.Pixels.IntersectsWith(planeRect) &&
                   planeRect.ContainsInclusive(location.X, location.Y);
        }

        /// <summary>
        /// Process gestures for a specific plane using its rendering tree
        /// </summary>
        protected virtual ISkiaGestureListener ProcessGesturesForPlane(
            Plane plane,
            SkiaGesturesParameters args,
            GestureEventProcessingInfo apply)
        {
            var thisOffset = TranslateInputCoords(apply.ChildOffset);
            var currentScroll = InternalViewportOffset.Pixels.Y;

            // Calculate the plane's current rendered position
            var planeOffsetY = currentScroll + plane.OffsetY;

            // Keep gesture coordinates as-is, but adjust child HitRects to current plane position
            var gesturePoint = new SKPoint(
                args.Event.Location.X + thisOffset.X,
                args.Event.Location.Y + thisOffset.Y);


            // Process gestures using plane's render tree in reverse Z-order
            var renderTree = plane.RenderTree;
            //Debug.WriteLine($"[PLANE {plane.Id}] Processing {renderTree.Count} children");

            bool hadDebug = false;
            for (int i = renderTree.Count - 1; i >= 0; i--)
            {
                var child = renderTree[i];

                if (child.Control == null || child.Control.IsDisposed || child.Control.IsDisposing ||
                    child.Control.InputTransparent || !child.Control.CanDraw)
                    continue;

                //Debug.WriteLine($"[PLANE {plane.Id}] Child {i}: {child.Control.Tag} Rect: {child.Rect} HitRect: {child.HitRect}");

                // Adjust child's HitRect to current plane position
                // Account for: scroll offset change + plane offset change since capture
                var scrollMovement = currentScroll - plane.RenderTreeCaptureOffset.Y;
                var planeMovement = plane.OffsetY - plane.RenderTreeCapturePlaneOffsetY;
                var totalMovement = scrollMovement + planeMovement;
                var adjustedHitRect = child.HitRect;
                adjustedHitRect.Offset(0, totalMovement);

                //if (args.Type == TouchActionResult.Tapped && !hadDebug)
                //{
                //    hadDebug = true;
                //    Debug.WriteLine($"[PLANE {plane.Id}] Raw gesture: {args.Event.Location}, thisOffset: {thisOffset}");
                //    Debug.WriteLine($"[PLANE {plane.Id}] currentScroll: {currentScroll}, plane.OffsetY: {plane.OffsetY}, planeOffsetY: {planeOffsetY}");
                //    Debug.WriteLine($"[PLANE {plane.Id}] captureOffset: {plane.RenderTreeCaptureOffset}, capturePlaneOffsetY: {plane.RenderTreeCapturePlaneOffsetY}");
                //    Debug.WriteLine($"[PLANE {plane.Id}] scrollMovement: {scrollMovement}, planeMovement: {planeMovement}, totalMovement: {totalMovement}");
                //    Debug.WriteLine($"[PLANE {plane.Id}] Gesture point: {gesturePoint}");
                //    Debug.WriteLine($"[PLANE {plane.Id}] Child {i} original HitRect: {child.HitRect}");
                //    Debug.WriteLine($"[PLANE {plane.Id}] Child {i} adjusted HitRect: {adjustedHitRect}");
                //}

                // Use the adjusted HitRect for hit testing
                if (adjustedHitRect.ContainsInclusive(gesturePoint.X, gesturePoint.Y))
                {
                    //Debug.WriteLine($"[PLANE {plane.Id}] HIT! Loop index {i}, ContextIndex {child.Control.ContextIndex}");

                    // FREEZE FIX: Restore the correct BindingContext from when the plane was captured
                    var originalBindingContext = child.Control.BindingContext;
                    if (child.FreezeBindingContext != null)
                    {
                        child.Control.BindingContext = child.FreezeBindingContext;
                    }

                    try
                    {
                        // Handle child tapped events
                        if (args.Type == TouchActionResult.Tapped)
                        {
                            Content.OnChildTapped(child.Control, args, apply);
                        }

                        // Get gesture listener for this child
                        ISkiaGestureListener listener = child.Control.GesturesEffect;
                        if (listener == null && child.Control is ISkiaGestureListener listen)
                        {
                            listener = listen;
                        }

                        if (listener != null)
                        {
                            var childOffset = TranslateInputCoords(apply.ChildOffsetDirect, false);

                            // Forward gesture to child with proper coordinate transformation
                            var consumed = listener.OnSkiaGestureEvent(args,
                                new GestureEventProcessingInfo(
                                    apply.MappedLocation,
                                    thisOffset,
                                    childOffset,
                                    apply.AlreadyConsumed));

                            if (consumed != null)
                            {
                                return consumed;
                            }

                            // Check attached gesture listeners
                            if (AddGestures.AttachedListeners.TryGetValue(child.Control, out var effect))
                            {
                                var attachedConsumed = effect.OnSkiaGestureEvent(args,
                                    new GestureEventProcessingInfo(
                                        apply.MappedLocation,
                                        thisOffset,
                                        childOffset,
                                        apply.AlreadyConsumed));

                                if (attachedConsumed != null)
                                {
                                    return effect;
                                }
                            }
                        }
                        
                        // Return after first hit to prevent multiple hits
                        return null;
                    }
                    finally
                    {
                        // FREEZE FIX: Always restore the original BindingContext
                        if (child.FreezeBindingContext != null)
                        {
                            child.Control.BindingContext = originalBindingContext;
                        }
                    }
                }
            }

            return null;
        }

        // ===================================================================================
        // TILED PLANES gesture routing
        // Tile cells are recycled to the pool right after a tile renders to its bitmap, so there is no
        // persistent live control to hit-test between frames. Instead we materialize the candidate cell
        // ON the gesture: realize it (which binds it to its data item), arrange it at its on-screen rect
        // so HitBoxAuto/translation match the screen, hit-test with the framework's transform-aware
        // HitIsInside, forward the gesture through the normal child path, then release it back to the pool.
        // Items sit on the estimate grid (index*slot), so the candidate index = floor(contentY/slot).
        // ===================================================================================

        protected virtual ISkiaGestureListener ProcessGesturesForTiles(
            SkiaGesturesParameters args,
            GestureEventProcessingInfo apply)
        {
            if (Content is not SkiaLayout layout || _planeHeight <= 0
                || layout.ItemsSource == null || layout.ItemsSource.Count == 0)
                return null;

            // Only discrete events target a cell. Skip the high-frequency scroll events (Panning/Wheel)
            // so the scroll hot path never realizes a cell per move.
            if (args.Type == TouchActionResult.Panning
                || args.Type == TouchActionResult.Wheel)
                return null;

            float slot = _tileSlot > 1f ? _tileSlot : Math.Max(1f, layout.GetAverageItemHeightPixels((float)RenderingScale));
            float scrollTop = -InternalViewportOffset.Pixels.Y;

            var thisOffset = TranslateInputCoords(apply.ChildOffset);
            float gx = args.Event.Location.X + thisOffset.X;
            float gy = args.Event.Location.Y + thisOffset.Y;

            float contentY = gy - DrawingRect.Top + scrollTop;
            int count = layout.ItemsSource.Count;

            // The estimate grid fully tiles the content with no gaps, so floor(contentY/slot) is the exact
            // owning item of the point (the 2px inter-cell spacing belongs to the slot above it).
            int idx = (int)Math.Floor(contentY / slot);
            if (idx < 0 || idx >= count)
                return null;

            var cell = layout.ChildrenFactory.GetViewForIndex(idx, null, 0, false);
            if (cell == null)
                return null;

            try
            {
                // Arrange the live cell at its on-screen rect so HitBoxAuto/translation match the screen
                // (lets transform-aware children hit-test correctly).
                float top = DrawingRect.Top + idx * slot - scrollTop;
                var rect = new SKRect(DrawingRect.Left, top, DrawingRect.Left + _planeWidth, top + slot);
                cell.Arrange(rect, _planeWidth / (float)RenderingScale, slot / (float)RenderingScale, (float)RenderingScale);

                return ForwardGestureToCell(cell, args, apply, thisOffset);
            }
            finally
            {
                // Return to the GENERIC pool (symmetric with the tile renderer's height:0 gets) so gesture
                // hit-testing never strands cells in a height bucket and starves tile rendering.
                layout.ChildrenFactory.ReleaseMeasuringView(cell);
            }
        }

        private ISkiaGestureListener ForwardGestureToCell(
            SkiaControl cell,
            SkiaGesturesParameters args,
            GestureEventProcessingInfo apply,
            SKPoint thisOffset)
        {
            if (cell.IsDisposed || cell.InputTransparent || !cell.CanDraw)
                return null;

            if (args.Type == TouchActionResult.Tapped)
                Content.OnChildTapped(cell, args, apply);

            ISkiaGestureListener listener = cell.GesturesEffect;
            if (listener == null && cell is ISkiaGestureListener listen)
                listener = listen;

            var childOffset = TranslateInputCoords(apply.ChildOffsetDirect, false);

            if (listener != null)
            {
                var consumed = listener.OnSkiaGestureEvent(args,
                    new GestureEventProcessingInfo(apply.MappedLocation, thisOffset, childOffset, apply.AlreadyConsumed));
                if (consumed != null)
                    return consumed;
            }

            if (AddGestures.AttachedListeners.TryGetValue(cell, out var effect))
            {
                var attachedConsumed = effect.OnSkiaGestureEvent(args,
                    new GestureEventProcessingInfo(apply.MappedLocation, thisOffset, childOffset, apply.AlreadyConsumed));
                if (attachedConsumed != null)
                    return effect;
            }

            return null;
        }
    }
}
