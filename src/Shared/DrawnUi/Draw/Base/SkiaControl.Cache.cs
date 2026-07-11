namespace DrawnUi.Draw;

public partial class SkiaControl
{
    private readonly LimitedQueue<Action> _offscreenCacheRenderingQueue = new(1);

    // Supersession token for offscreen rendering. The latest scheduled offscreen render always wins;
    // earlier in-flight renders (which cannot be aborted mid-call) observe cancellation/supersession and
    // abandon their result instead of publishing it. Also tripped on dispose so a render in flight while the
    // control is being torn down bails before touching freed surfaces/children.
    private CancellationTokenSource _offscreenRenderCts;
    private long _offscreenRenderGeneration;

    /// <summary>Token of the currently scheduled offscreen render (see <see cref="RenewOffscreenRenderToken"/>).</summary>
    public CancellationToken OffscreenRenderToken => _offscreenRenderCts?.Token ?? CancellationToken.None;

    /// <summary>
    /// Cancels the previous offscreen render (if any) and returns a fresh token + generation stamp for the new one.
    /// Long-running offscreen actions should poll the returned token at safe checkpoints and MUST NOT publish their
    /// result once it is cancelled or <see cref="IsOffscreenRenderSuperseded"/> returns true.
    /// </summary>
    public CancellationToken RenewOffscreenRenderToken(out long generation)
    {
        var previous = _offscreenRenderCts;
        var fresh = new CancellationTokenSource();
        _offscreenRenderCts = fresh;
        generation = Interlocked.Increment(ref _offscreenRenderGeneration);
        if (previous != null)
        {
            try
            {
                previous.Cancel();
            }
            catch
            {
                /* nop */
            }

            previous.Dispose(); // reading token.IsCancellationRequested after dispose is safe
        }

        return fresh.Token;
    }

    /// <summary>True once a newer offscreen render was scheduled after the one identified by <paramref name="generation"/>.</summary>
    public bool IsOffscreenRenderSuperseded(long generation) =>
        Interlocked.Read(ref _offscreenRenderGeneration) != generation;

    /// <summary>Cancels any scheduled/in-flight offscreen render for this control without scheduling a new one.</summary>
    public void CancelOffscreenRendering()
    {
        try
        {
            _offscreenRenderCts?.Cancel();
        }
        catch
        {
            /* nop */
        }
    }

    /// <summary>
    /// Find intersections between changed children and DrawingRect,
    /// add intersecting ones to DirtyChildrenInternal and set IsRenderingWithComposition = true if any.
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="destination"></param>
    protected virtual void SetupRenderingWithComposition(DrawingContext ctx)
    {
        if (IsCacheComposite)
        {
            DirtyChildrenInternal.Clear();

            var previousCache = RenderObjectPrevious;

            if (previousCache != null && ctx.Context.IsRecycled) //not the first draw
            {
                IsRenderingWithComposition = true;

                var offset = new SKPoint(this.DrawingRect.Left - previousCache.Bounds.Left,
                    DrawingRect.Top - previousCache.Bounds.Top);

                //Super.Log($"[ImageComposite] {Tag} drawing cached at {offset}  {DrawingRect}");


                // Add more children that are not already added but intersect with the dirty regions
                var asSpans = CollectionsMarshal.AsSpan(DirtyChildrenTracker.GetList());
                foreach (var item in asSpans)
                {
                    DirtyChildrenInternal.Add(item);
                }

                //make intersecting children dirty too
                var asSpan = RenderTree.AsSpans();
                foreach (var cell in asSpan)
                {
                    //use full transform-aware bounds (handles rotation, scale, skew, perspective)
                    if (!DirtyChildrenInternal.Contains(cell.Control) &&
                        DirtyChildrenInternal.Any(dirtyChild =>
                            dirtyChild.GetTransformedDirtyBounds()
                                .IntersectsWith(cell.Control.GetTransformedDirtyBounds())))
                    {
                        DirtyChildrenInternal.Add(cell.Control);
                    }

                    // Log the current cell's DirtyRegion
                    /*
                      var cellRect = cell.Control.DirtyRegion;
                      Trace.WriteLine($"Checking cell.Control: {cell.Control}, DirtyRegion: X={cellRect.Left}, Y={cellRect.Top}, Width={cellRect.Width}, Height={cellRect.Height}");

                      if (!DirtyChildrenInternal.Contains(cell.Control))
                      {
                          bool intersects = false;
                          foreach (var dirtyChild in DirtyChildrenInternal)
                          {
                              var dirtyChildRect = dirtyChild.DirtyRegion;
                              bool doesIntersect = dirtyChild.DirtyRegion.IntersectsWith(cell.Control.DirtyRegion);

                              // Log the comparison details
                              Trace.WriteLine($"  Comparing with dirtyChild: {dirtyChild}, DirtyRegion: X={dirtyChildRect.Left}, Y={dirtyChildRect.Top}, Width={dirtyChildRect.Width}, Height={dirtyChildRect.Height}");
                              Trace.WriteLine($"  Intersects: {doesIntersect}");

                              if (doesIntersect)
                              {
                                  intersects = true;
                                  // Optionally break early if you only need one intersection
                                  // break;
                              }
                          }

                          if (intersects)
                          {
                              Trace.WriteLine($"Adding cell.Control: {cell.Control} to DirtyChildrenInternal");
                              DirtyChildrenInternal.Add(cell.Control);
                          }
                      }
                      else
                      {
                          Trace.WriteLine($"Skipping cell.Control: {cell.Control} (already in DirtyChildrenInternal)");
                      }
                     */
                }

                var count = 0;
                foreach (var dirtyChild in DirtyChildrenInternal)
                {
                    var clip = dirtyChild.GetTransformedDirtyBounds();
                    clip.Offset(offset);
                    //clip.Inflate(0.4f, 0.4f);

                    previousCache.Surface.Canvas.DrawRect(clip, PaintErase);

                    count++;
                }
            }
            else
            {
                //Debug.WriteLine("[ImageComposite] was rebuild");
                IsRenderingWithComposition = false;
            }
        }
        else
        {
            IsRenderingWithComposition = false;
        }
    }

    public static readonly BindableProperty CacheSharingProperty = BindableProperty.Create(
        nameof(CacheSharing),
        typeof(CacheSharingType),
        typeof(SkiaControl),
        CacheSharingType.Default,
        propertyChanged: NeedDraw);

    /// <summary>
    /// When Shared, all instances of this control type on the same Canvas share one CachedObject
    /// instead of each allocating their own. Effective only for Image and GPU cache types.
    /// Individual control disposal does not release the shared cache;
    /// use SuperView.Cache.Free&lt;T&gt;() or let the Canvas dispose it.
    /// </summary>
    public CacheSharingType CacheSharing
    {
        get => (CacheSharingType)GetValue(CacheSharingProperty);
        set => SetValue(CacheSharingProperty, value);
    }

    /// <summary>
    /// Cache sharing is only supported for Image and GPU cache types.
    /// </summary>
    private bool IsSharedCacheEligible => UsingCacheType == SkiaCacheType.Operations ||
                                          UsingCacheType == SkiaCacheType.Image || UsingCacheType == SkiaCacheType.GPU;

    public static readonly BindableProperty AutoCacheProperty = BindableProperty.Create(nameof(AutoCache),
        typeof(bool),
        typeof(SkiaControl),
        false,
        propertyChanged: NeedDraw);

    /// <summary>
    /// Control will be responsible for controlling cache instead of using UseCache property.
    /// </summary>
    public bool AutoCache
    {
        get { return (bool)GetValue(AutoCacheProperty); }
        set { SetValue(AutoCacheProperty, value); }
    }

    public static readonly BindableProperty UseCacheProperty = BindableProperty.Create(nameof(UseCache),
        typeof(SkiaCacheType),
        typeof(SkiaControl),
        SkiaCacheType.None,
        propertyChanged: NeedDraw);

    /// <summary>
    /// Never reuse the rendering result. Actually true for ScrollLooped SkiaLayout viewport container to redraw its content several times for creating a looped aspect.
    /// </summary>
    public SkiaCacheType UseCache
    {
        get { return (SkiaCacheType)GetValue(UseCacheProperty); }
        set { SetValue(UseCacheProperty, value); }
    }

    public static readonly BindableProperty AllowCachingProperty = BindableProperty.Create(nameof(AllowCaching),
        typeof(bool),
        typeof(SkiaControl),
        true,
        propertyChanged: NeedDraw);

    /// <summary>
    /// Might want to set this to False for certain cases.
    /// </summary>
    public bool AllowCaching
    {
        get { return (bool)GetValue(AllowCachingProperty); }
        set { SetValue(AllowCachingProperty, value); }
    }


    /// <summary>
    /// Used by the UseCacheDoubleBuffering process. 
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public CachedObject RenderObjectPrevious
    {
        get { return _renderObjectPrevious; }
        set
        {
            RenderObjectNeedsUpdate = false;
            if (_renderObjectPrevious != value)
            {
                var kill = _renderObjectPrevious;
                if (kill != null)
                {
                    if (kill.Surface != null)
                    {
                        if (value == null || value.Surface.Handle != _renderObjectPrevious.Surface.Handle)
                        {
                            //recycle surface
                            ReturnSurface(kill.Surface);
                        }

                        kill.Surface = null;
                        //if (_renderObjectPrevious.Type == SkiaCacheType.ImageComposite)
                        //{
                        //    kill.Surface = null;
                        //    kill.Image = null;
                        //}
                    }

                    DisposeObject(kill);
                }

                _renderObjectPrevious = value;
            }
        }
    }

    public SKSurface CreateSurface(int width, int height, bool isGpu)
    {
        SKSurface surface = null;
        if (Superview is DrawnView view)
        {
            surface = view.CreateSurface(width, height, isGpu);
        }

        if (surface == null) //fallback if gpu failed
        {
            //non-gpu
            var cacheSurfaceInfo = new SKImageInfo(width, height);
            surface = SKSurface.Create(cacheSurfaceInfo);
        }

        return surface;
    }

    public void ReturnSurface(SKSurface surface)
    {
        if (Superview is DrawnView view)
        {
            view.ReturnSurface(surface);
        }
        else
        {
            DisposeObject(surface);
        }
    }

    CachedObject _renderObjectPrevious;

    /// <summary>
    /// The cached representation of the control.
    /// Will be used on redraws without calling Paint etc, until the control is requested to be updated.
    /// This WILL NOT raise PropertyChanged !!! (avoiding MAUI concurrent access conflict)
    /// Use OnCacheCreated (CacheCreated event) and OnCacheDestroyed (CacheDestroyed event).
    /// </summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    public CachedObject RenderObject
    {
        get
        {
            if (Superview != null && CacheSharing == CacheSharingType.Shared && IsSharedCacheEligible &&
                Superview != null)
                return Superview.SharedCache.Get(GetType());
            return _renderObject;
        }
        set
        {
            if (Superview != null && CacheSharing == CacheSharingType.Shared && IsSharedCacheEligible &&
                Superview != null)
            {
                lock (LockDraw)
                {
                    if (value != null)
                    {
                        RenderObjectNeedsUpdate = false;
                        Superview.SharedCache.Set(GetType(), value);
                        OnCacheCreated();
                    }
                    else
                    {
                        // Don't clear shared cache on individual control dispose/invalidate.
                        // Mark this instance as needing re-render so CheckCachedObjectValid fails.
                        RenderObjectNeedsUpdate = true;
                        OnCacheDestroyed();
                    }

                    Monitor.PulseAll(LockDraw);
                }

                return;
            }

            RenderObjectNeedsUpdate = false;
            if (_renderObject != value)
            {
                //lock both RenderObjectPrevious and RenderObject
                lock (LockDraw)
                {
                    if (_renderObject != null) //if we already have something in actual cache then
                    {
                        if (UsesCacheDoubleBuffering
                            //|| UsingCacheType == SkiaCacheType.Image //to just reuse same surface
                            || IsCacheComposite)
                        {
                            RenderObjectPrevious = _renderObject; //send it to back for special cases
                        }
                        else
                        {
                            if (_renderObject.Surface != null && Superview is DrawnView view)
                            {
                                view.ReturnSurface(_renderObject.Surface);
                                _renderObject.Surface = null;
                            }

                            DisposeObject(_renderObject);
                        }
                    }

                    _renderObject = value;

                    if (value != null)
                    {
                        PixelsForeign = false; // fresh cache = pixels belong to the current BindingContext
                        OnCacheCreated();
                    }
                    else
                        OnCacheDestroyed();

                    Monitor.PulseAll(LockDraw);
                }
            }
        }
    }

    CachedObject _renderObject;

    protected virtual void OnCacheCreated()
    {
        CreatedCache?.Invoke(this, RenderObject);
    }

    protected virtual void OnCacheDestroyed()
    {
    }

    /// <summary>
    /// Indended to prohibit background rendering, useful for streaming controls like camera, gif etc. SkiaBackdrop has it set to True as well.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual bool CanUseCacheDoubleBuffering
    {
        get
        {
#if WEB || BROWSER
            return false;
#else
            return true;
#endif
        }
    }

    /// <summary>
    /// Read-only computed flag for internal use.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool UsesCacheDoubleBuffering
    {
        get
        {
            return CanUseCacheDoubleBuffering
                   && Super.Multithreaded
                   || UsingCacheType == SkiaCacheType.ImageDoubleBuffered;
        }
    }

    public event EventHandler<CachedObject> CreatedCache;

    public void DestroyRenderingObject()
    {
        RenderObject = null;
        RenderObjectPreviousNeedsUpdate = true;
    }

    /// <summary>
    /// Technical optional method for some custom logic. Actually used by SkiaMap only.
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="cache"></param>
    public void DrawRenderObject(DrawingContext ctx, float x, float y, CachedObject cache = null)
    {
        cache ??= RenderObject;
        if (cache == null)
            return;
        var destinationRect = new SKRect(x, y, x + cache.Bounds.Width, y + cache.Bounds.Height);
        var context = AddPaintArguments(ctx).WithDestination(destinationRect);
        DrawRenderObjectInternal(context, cache);
    }

    public bool IsRenderObjectValid(SKSize size, CachedObject cache = null)
    {
        cache ??= RenderObject;

        if (cache == null || !CompareSize(cache.Bounds.Size, size, 1))
            return false;

        return true;
    }

    /// <summary>
    /// Technical optional method for some custom logic. Will create as SKImage or SKPicture (asOperations=true).
    /// WIll not affect current RenderObject property.
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="area"></param>
    /// <param name="asOperations"></param>
    /// <returns></returns>
    public CachedObject CreateRenderedObject(DrawingContext ctx, SKRect area, bool asOperations)
    {
        var usingCacheType = asOperations ? SkiaCacheType.Operations : SkiaCacheType.Image;

        var renderObject = CreateRenderingObject(ctx, area, null, usingCacheType,
            (context) => { PaintWithEffects(context.WithDestination(area)); });

        return renderObject;
    }

    public void RasterizeToCache(DrawingContext ctx)
    {
        SkiaControl control = this;

        var destination = new SKRect(0, 0, float.PositiveInfinity, float.PositiveInfinity);

        var measuredSize = control.Measure(destination.Width, destination.Height, ctx.Scale);
        var size = measuredSize.Pixels;

        control.Arrange(
            new SKRect(0, 0, size.Width, size.Height),
            size.Width, size.Height, ctx.Scale);

        control.RenderObject = control.CreateRenderedObject(ctx, control.DrawingRect, false);
    }

    protected virtual bool CheckCachedObjectValid(CachedObject cache, SKRect recordingArea, SkiaDrawingContext context)
    {
        // In shared mode the per-instance RenderObjectNeedsUpdate flag is irrelevant — the shared
        // cache's existence and size are the only validity signals. Individual controls should call
        // SuperView.Cache.Free<T>() to evict the shared entry and force a full re-render.
        bool isSharedMode = CacheSharing == CacheSharingType.Shared && IsSharedCacheEligible;

        if (cache != null && (!RenderObjectNeedsUpdate || isSharedMode))
        {
            if (cache.Surface != null && cache.Surface.Handle == 0)
            {
                return false; //maybe disposed by GC
            }

            if (!CompareSize(cache.RecordingArea.Size, recordingArea.Size, 1))
            {
                CacheValidity = CacheValidityType.SizeMismatch;
                return false;
            }

            //check hardware context maybe changed
            if (IsCacheGPU && cache.Surface != null &&
                cache.Surface.Context != null &&
                context.Superview?.CanvasView is SkiaViewAccelerated hardware)
            {
                //hardware context might change if we returned from background..
                if (hardware.GRContext == null || cache.Surface.Context == null
                                               || (int)hardware.GRContext.Handle != (int)cache.Surface.Context.Handle)
                {
                    CacheValidity = CacheValidityType.GraphicContextMismatch;
                    return false;
                }
            }

            if (isSharedMode)
                RenderObjectNeedsUpdate = false; // sync per-instance flag when accepting shared cache

            CacheValidity = CacheValidityType.Valid;
            return true;
        }

        CacheValidity = CacheValidityType.Missing;
        return false;
    }

    public bool IsCacheGPU
    {
        get { return UsingCacheType == SkiaCacheType.GPU || UsingCacheType == SkiaCacheType.ImageCompositeGPU; }
    }

    public virtual SkiaCacheType UsingCacheType
    {
        get
        {
            if (!AllowCaching || !Super.CacheEnabled)
            {
                return SkiaCacheType.None;
            }

            if (CanUseCacheDoubleBuffering && Super.Multithreaded)
            {
                //if (Parent is SkiaControl)
                {
                    if (UseCache == SkiaCacheType.None)
                        return SkiaCacheType.OperationsFull;
                }

                if (UseCache == SkiaCacheType.ImageDoubleBuffered || UseCache == SkiaCacheType.GPU)
                    return SkiaCacheType.Image;

                if (UseCache == SkiaCacheType.ImageComposite || UseCache == SkiaCacheType.ImageCompositeGPU)
                    return SkiaCacheType.Operations;
            }
            // FIX: removed ImageDoubleBuffered->Image downgrade in the non-multithreaded else branch.
            // It defeated async double-buffering (Super.Multithreaded is false by default) -> recycled cells
            // rebaked synchronously on the render thread = scroll lag. (regression from "improvements for cells")
            //else
            //if (UseCache == SkiaCacheType.ImageDoubleBuffered)
            //    return SkiaCacheType.Image;

            if (UseCache == SkiaCacheType.ImageDoubleBuffered && !CanUseCacheDoubleBuffering)
                return SkiaCacheType.Image;

            if (UseCache == SkiaCacheType.GPU && !Super.GpuCacheEnabled)
                return SkiaCacheType.Image;

            if (UseCache == SkiaCacheType.ImageCompositeGPU && !Super.GpuCacheEnabled)
                return SkiaCacheType.ImageComposite;

            //if (EffectPostRenderer != null 
            //    && (UseCache == SkiaCacheType.None || UseCache == SkiaCacheType.Operations))
            //    return SkiaCacheType.Image;

            if (UseCache == SkiaCacheType.None && CanUseCacheDoubleBuffering && Super.Multithreaded &&
                Parent is SkiaControl)
                return SkiaCacheType.Operations;

            return UseCache;
        }
    }

    public bool IsCacheComposite
    {
        get
        {
            return UsingCacheType == SkiaCacheType.ImageComposite || UsingCacheType == SkiaCacheType.ImageCompositeGPU;
        }
    }

    public virtual CachedObject CreateRenderingObject(
        DrawingContext context,
        SKRect recordingArea,
        CachedObject reuseSurfaceFrom,
        SkiaCacheType usingCacheType,
        Action<DrawingContext> action)
    {
        if (recordingArea.Height == 0 || recordingArea.Width == 0 || IsDisposed || IsDisposing)
        {
            return null;
        }

        CachedObject renderObject = null;

        try
        {
            var recordArea = GetCacheArea(recordingArea);

            //if (UsingCacheType == SkiaCacheType.OperationsFull)
            //{
            //    recordArea = destination;
            //}

            NeedUpdate =
                false; //if some child changes this while rendering to cache we will erase resulting RenderObject

            //GRContext grContext = null;

            if (usingCacheType == SkiaCacheType.Operations || usingCacheType == SkiaCacheType.OperationsFull)
            {
                var cacheRecordingArea = GetCacheRecordingArea(recordingArea);

                var cullRect = cacheRecordingArea;
                var expand = GetRenderingExpandPixels();
                if (expand.Left != 0 || expand.Top != 0 || expand.Right != 0 || expand.Bottom != 0)
                {
                    //BeginRecording's cull rect becomes the recording canvas's initial clip,
                    //so shadows/glow drawn beyond cacheRecordingArea would get cut here.
                    //Expand only the cull rect fed to the recorder - RecordingArea stored on
                    //CachedObject must stay unexpanded, it drives CalculateDrawingOffset's
                    //reposition delta and expanding it would shift content by the expand amount.
                    cullRect = new SKRect(
                        cacheRecordingArea.Left - (float)Math.Round(expand.Left),
                        cacheRecordingArea.Top - (float)Math.Round(expand.Top),
                        cacheRecordingArea.Right + (float)Math.Round(expand.Right),
                        cacheRecordingArea.Bottom + (float)Math.Round(expand.Bottom));
                }

                using (var recorder = new SKPictureRecorder())
                {
                    var recordingContext = context.CreateForRecordingOperations(recorder, cullRect);

                    action(recordingContext);

                    SKPicture skPicture = recorder.EndRecording();
                    renderObject = new(UsingCacheType, skPicture, context.Destination, cacheRecordingArea)
                    {
                        DataContext = this.ContextIndex
                    };
                }
            }
            else if (usingCacheType != SkiaCacheType.None)
            {
                var width = (int)recordArea.Width;
                var height = (int)recordArea.Height;

                bool needCreateSurface = !CheckCachedObjectValid(reuseSurfaceFrom, recordingArea, context.Context) ||
                                         usingCacheType == SkiaCacheType.GPU; //never reuse GPU surfaces

                SKSurface surface = null;

                if (!needCreateSurface)
                {
                    //reusing existing surface
                    surface = reuseSurfaceFrom.Surface;
                    if (surface == null || surface.Handle == 0)
                    {
                        Super.Log("CreateRenderingObject failed to reuse surface!");
                        return null; //would be totally unexpected
                    }

                    reuseSurfaceFrom.PreserveSourceFromDispose = true; //we will dispose that source in this new object

                    if (!IsCacheComposite)
                        surface.Canvas.Clear();
                }
                else
                {
                    surface = CreateSurface(width, height, IsCacheGPU);

                    if (IsCacheComposite && RenderObjectPrevious != null)
                    {
                        InvalidateMeasure();
                    }
                }

                if (surface == null)
                {
                    return null; //would be totally unexpected
                }

                var recordingContext = context.CreateForRecordingImage(surface, recordArea.Size);

                recordingContext.Context.IsRecycled = !needCreateSurface;

                // Translate the canvas to start drawing at (0,0)

                recordingContext.Context.Canvas.Translate(-recordArea.Left, -recordArea.Top);

                //create empty NODE just for children to attach to
                if (Super.UseFrozenVisualLayers)
                {
                    VisualLayer = VisualLayer.CreateEmpty();
                }

                // Perform the drawing action
                action(recordingContext);

                //surface.Canvas.Flush();

                recordingContext.Context.Canvas.Translate(recordArea.Left, recordArea.Top);
                recordingContext.Context.Canvas.Flush();

                renderObject = new(usingCacheType, surface, recordArea, recordingArea)
                {
                    SurfaceIsRecycled = recordingContext.Context.IsRecycled, DataContext = this.ContextIndex
                };

                if (Super.UseFrozenVisualLayers && VisualLayer != null)
                {
                    var childrenNodes = this.VisualLayer.Children;
                    renderObject.Children = childrenNodes;
                }
            }


            //else we landed here with no cache type at all..
        }
        catch (Exception e)
        {
            Super.Log(e);
        }

        return renderObject;
    }

    public Action<DrawingContext, CachedObject> DelegateDrawCache { get; set; }

    public virtual void DrawRenderObjectInternal(
        DrawingContext ctx,
        CachedObject cache)
    {
        var destination = ctx.Destination;
        // New absolute offsets
        destination.Offset((float)(Left * ctx.Scale), (float)(Top * ctx.Scale));

        if (DelegateDrawCache != null)
        {
            DelegateDrawCache(ctx.WithDestination(destination), cache);
        }
        else
        {
            DrawRenderObject(ctx.WithDestination(destination), cache);
        }

        if (Super.UseFrozenVisualLayers && VisualLayer != null && cache.Children != null)
        {
            this.VisualLayer.AttachFromCache(cache);
        }
    }

    public bool IsCacheImage
    {
        get
        {
            var cache = UsingCacheType;
            return cache == SkiaCacheType.Image
                   || cache == SkiaCacheType.GPU
                   || cache == SkiaCacheType.ImageComposite
                   || cache == SkiaCacheType.ImageCompositeGPU
                   || cache == SkiaCacheType.ImageDoubleBuffered;
        }
    }

    public bool IsCacheOperations
    {
        get
        {
            var cache = UsingCacheType;
            return cache == SkiaCacheType.Operations
                   || cache == SkiaCacheType.OperationsFull;
        }
    }

// True while an offscreen bake for THIS control is pending or painting (double-buffered path). Guards the
// sync resize-rebuild below from painting the same control concurrently with the background bake.
    private volatile bool _offscreenBakeBusy;

    /// <summary>
    /// The control RESIZED while holding a cache: the cached pixels are valid but at a stale size, and the
    /// double-buffered path would otherwise draw a blank placeholder until the async rebake lands (a visible
    /// blink). Re-record synchronously at the new size instead — one render-thread paint, happening only on
    /// an actual resize of an already-cached control, never per-frame and never for cold (never-cached)
    /// controls, so scrolling smoothness is unaffected. Skipped while a background bake is painting us.
    /// </summary>
    bool TrySyncRebuildStaleSize(DrawingContext context, SKRect recordArea)
    {
        if (_offscreenBakeBusy)
            return false; // background bake owns painting this control right now — do not race it

        CreateRenderingObjectAndPaint(context, recordArea,
            (ctx) => { PaintWithEffects(ctx.WithDestination(DrawingRect)); });

        return RenderObject != null;
    }

    protected virtual bool UseRenderingObject(DrawingContext context, SKRect recordArea)
    {
        lock (LockDraw) //prevent conflicts with erasing cache after we decided to use it
        {
            var cache = RenderObject;
            var cacheOffscreen = RenderObjectPrevious;
            var needBuild = false;


            if (IsCacheComposite)
            {
                if (RenderObjectPreviousNeedsUpdate)
                {
                    IsRenderingWithComposition = false;

                    cacheOffscreen = null;
                    cache = null;
                    RenderObject = null;
                    RenderObjectPrevious = null;

                    RenderObjectPreviousNeedsUpdate = false;
                }
            }
            else
            {
                if (RenderObjectPrevious != null && RenderObjectPreviousNeedsUpdate)
                {
                    // 1.7.8.2 kept OffscreenRenderObject (the previous cache) DRAWABLE and blitted it while the
                    // new one baked async — cells never blanked, so no placeholder/sync-rebuild stall during
                    // scroll. Do NOT dispose/null it here; just flag a rebuild. The completed async bake swaps it.
                    RenderObjectPreviousNeedsUpdate = false;
                    needBuild = true;
                }
            }

            if (cache != null)
            {
                //CacheValidity will be set by CheckCachedObjectValid
                if (!UsesCacheDoubleBuffering && !CheckCachedObjectValid(cache, recordArea, context.Context))
                {
                    return false;
                }

                //draw existing front cache
                lock (LockDraw)
                {
                    // Blit the existing cache unconditionally (1.7.8.2 fast path). A size change just
                    // schedules an async rebake below (NeedUpdateFrontCache) — never a sync render-thread
                    // re-record, which stalls the frame ~150ms per cell on slow hardware during scroll.
                    DrawRenderObjectInternal(context, cache);

                    Monitor.PulseAll(LockDraw);
                }

                if (!UsesCacheDoubleBuffering || !NeedUpdateFrontCache)
                {
                    return true;
                }
            }
            else
            {
                CacheValidity = CacheValidityType.Missing;
            }

            if (UsesCacheDoubleBuffering)
            {
                lock (LockDraw)
                {
                    if (cache == null && cacheOffscreen != null)
                    {
                        // Blit the previous cache unconditionally (1.7.8.2 fast path); async rebake below
                        // adopts the new size — never a sync render-thread re-record during scroll.
                        DrawRenderObjectInternal(context, cacheOffscreen);
                    }
                    else
                    {
                        if (!ExistingCacheWasRendered)
                        {
                            DrawPlaceholder(context);

                            // INVARIANT: painting a placeholder with NO cache and NO bake in flight
                            // means the rebuild signal was LOST (e.g. a recycle cancelled the offscreen
                            // bake after NeedUpdate was already consumed — cell frozen as a silhouette
                            // forever at idle, nothing left to trigger a redraw). Re-arm the build here:
                            // a placeholder frame must always have a bake scheduled behind it.
                            if (!_offscreenBakeBusy)
                                needBuild = true;
                        }
                    }

                    Monitor.PulseAll(LockDraw);
                }

                if (NeedUpdateFrontCache)
                {
                    needBuild = true;
                }

                NeedUpdateFrontCache = false;

                if (needBuild)
                {
                    var clone = AddPaintArguments(context);
                    _offscreenBakeBusy = true;
                    PushToOffscreenRendering(() =>
                    {
                        try
                        {
                            //will be executed on background thread in parallel
                            var prepared = CreateRenderingObject(clone, recordArea, RenderObjectPreparing,
                                UsingCacheType,
                                (ctx) => { PaintWithEffects(ctx); });

                            RenderObjectPreparing = prepared;
                            if (prepared != null)
                            {
                                RenderObject = prepared;
                                _renderObjectPreparing = null;
                            }
                        }
                        finally
                        {
                            _offscreenBakeBusy = false;
                        }

                        // UNCONDITIONAL wakeup: gating this on Parent.UpdateLocks lost the present
                        // when the bake completed DURING the very frame that queued it (parent holds
                        // LockUpdate while painting) — cache READY but no frame ever requested: static
                        // content stayed a placeholder until user interaction (Sandbox car image).
                        // A locked parent defers via _neededUpdate and LockUpdate(false) replays it.
                        Repaint();
                    });
                }

                return !NeedUpdateFrontCache;
            }

            return false;
        }
    }

    /// <summary>
    /// Called by ImageDoubleBuffered cache rendering when no cache is ready yet.
    /// Other controls might use this too to draw placeholders when result is not ready yet.
    /// Default paints the control's own BackgroundColor (a cell keeps its silhouette), falling back
    /// to a faint translucent gray — never nothing: an empty default painted invisible HOLES where a
    /// cell's cache was still being prepared (or its rebuild was lost). Override for custom skeletons.
    /// </summary>
    /// <param name="context"></param>
    public virtual void DrawPlaceholder(DrawingContext context)
    {
        var color = BackgroundColor;
        if (color == null || color.Alpha <= 0.01)
        {
            color = PlaceholderFallbackColor;
        }

        using var paint = new SKPaint { Color = color.ToSKColor(), Style = SKPaintStyle.Fill };
        context.Context.Canvas.DrawRect(context.Destination, paint);
    }

    private static readonly Color PlaceholderFallbackColor = Color.FromRgba(128, 128, 128, 32);

    public CacheValidityType CacheValidity { get; protected set; }

    public enum CacheValidityType
    {
        Valid,
        Missing,
        SizeMismatch,
        GraphicContextMismatch
    }

    public Action GetOffscreenRenderingAction()
    {
        var action = _offscreenCacheRenderingQueue.Pop();
        return action;
    }

    public void PushToOffscreenRendering(Action action, CancellationToken cancel = default)
    {
        if (Super.OffscreenRenderingAtCanvasLevel)
        {
            _offscreenCacheRenderingQueue.Push(action);
            Superview?.PushToOffscreenRendering(this, cancel);
        }
        else
        {
            _offscreenCacheRenderingQueue.Push(action);

            if (OperatingSystem.IsBrowser())
            {
                // Single-threaded WASM: there is no background thread — Task.Run work items execute
                // on the main thread between frames and can starve INDEFINITELY under continuous
                // requestAnimationFrame load (observed: with two lotties building every frame, the
                // first-queued control's pump never ran once, leaving it an empty box forever).
                // Total main-thread cost is identical either way, so drain synchronously instead —
                // guaranteed execution, no scheduler involved.
                DrainOffscreenQueueInline();
            }
            else
            {
                // DEDICATED workers, not Task.Run: offscreen bakes are latency-critical render work.
                // On the shared threadpool they queue behind everything else the app schedules at the
                // same moment (startup fetch/measure tasks), and the pool grows by ~1 thread/500ms —
                // cold cells then materialize ONE BY ONE over seconds (observed on-device, Release).
                // The per-control semaphore still serializes pumps of the same control.
                OffscreenRenderingService.Enqueue(this);
            }
        }
    }

    /// <summary>
    /// Dedicated worker threads draining controls' offscreen cache queues
    /// (<see cref="ProcessOffscreenCacheRenderingAsync"/>). Offscreen bakes must not compete with
    /// ordinary Task.Run work: threadpool injection latency serialized cold-cell first-bakes at
    /// app startup (cells appearing one after another). Bakes raster to CPU surfaces, no GRContext
    /// affinity, so plain background threads are safe.
    /// </summary>
    /// <summary>
    /// Set while a CellPreparationService worker is measuring this (templated cell) view off-thread.
    /// The render thread treats a flagged cell as not-yet-prepared (draws its placeholder instead of
    /// touching half-measured state). Volatile: written by the prep worker, read by the render thread.
    /// </summary>
    internal volatile bool IsPreparingOffthread;

    /// <summary>
    /// Set when a recycled templated cell is REBOUND to a different BindingContext (ViewsAdapter.AttachView):
    /// the caches (<see cref="RenderObject"/>/<see cref="RenderObjectPrevious"/>) still hold the PREVIOUS
    /// context's pixels. While set, the prepared-views stale-serve must NOT blit those caches (would paint
    /// the wrong item's content at this slot). Cleared when a fresh <see cref="RenderObject"/> is created.
    /// Volatile: written by prep worker / render thread, read by the render thread.
    /// </summary>
    internal volatile bool PixelsForeign;

    /// <summary>
    /// Atomic measure claim for templated cells: taken (0→1 via Interlocked) by whichever side measures
    /// this instance — the CellPreparationService worker or the render thread's gap-rescue inline
    /// measure — so the two can never measure the same view concurrently. Released back to 0 in finally.
    /// </summary>
    internal int MeasureClaim;

    internal static class OffscreenRenderingService
    {
        private static readonly System.Collections.Concurrent.BlockingCollection<SkiaControl> Work = new();
        private static int _started;

        public static void Enqueue(SkiaControl control)
        {
            EnsureWorkers();
            try
            {
                Work.Add(control);
            }
            catch (InvalidOperationException)
            {
                // completed adding (shutdown) — nothing to do
            }
        }

        static void EnsureWorkers()
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
                return;

            int workers = Math.Clamp(Environment.ProcessorCount / 2, 2, 4);
            for (int i = 0; i < workers; i++)
            {
                var thread = new Thread(WorkerLoop)
                {
                    IsBackground = true, Name = $"DrawnUi-OffscreenBake-{i}", Priority = ThreadPriority.AboveNormal,
                };
                thread.Start();
            }
        }

        static void WorkerLoop()
        {
            foreach (var control in Work.GetConsumingEnumerable())
            {
                try
                {
                    if (control.IsDisposed || control.IsDisposing)
                        continue;

                    // drains the control's own action queue; per-control semaphore keeps this safe
                    // against a concurrent pump for the same control on another worker
                    control.ProcessOffscreenCacheRenderingAsync().GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    Super.Log(e);
                }
            }
        }
    }

    void DrainOffscreenQueueInline()
    {
        var action = _offscreenCacheRenderingQueue.Pop();
        while (!IsDisposed && !IsDisposing && action != null)
        {
            try
            {
                action.Invoke();
            }
            catch (Exception e)
            {
                Super.Log(e);
            }

            action = _offscreenCacheRenderingQueue.Pop();
        }
    }

    protected SemaphoreSlim semaphoreOffsecreenProcess = new(1);

    public async Task ProcessOffscreenCacheRenderingAsync()
    {
        await semaphoreOffsecreenProcess.WaitAsync();

        try
        {
            if (_offscreenCacheRenderingQueue.Count == 0)
                return;

            Action action = _offscreenCacheRenderingQueue.Pop();
            while (!IsDisposed && !IsDisposing && action != null)
            {
                try
                {
                    action.Invoke();

                    if (_offscreenCacheRenderingQueue.Count > 0)
                        action = _offscreenCacheRenderingQueue.Pop();
                    else
                        break;
                }
                catch (Exception e)
                {
                    Super.Log(e);
                }
            }
        }
        finally
        {
            semaphoreOffsecreenProcess.Release();
        }
    }


    /// <summary>
    /// Used by the UseCacheDoubleBuffering process. This is the new cache beign created in background. It will be copied to RenderObject when ready.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public CachedObject RenderObjectPreparing

    {
        get { return _renderObjectPreparing; }
        set
        {
            RenderObjectNeedsUpdate = false;
            if (_renderObjectPreparing != value)
            {
                var previous = _renderObjectPreparing;
                _renderObjectPreparing = value;

                if (previous != null
                    && !ReferenceEquals(previous, RenderObject)
                    && !ReferenceEquals(previous, RenderObjectPrevious))
                {
                    DisposeObject(previous);
                }
            }
        }
    }

    CachedObject _renderObjectPreparing;

    /// <summary>
    /// Used by ImageDoubleBuffering cache
    /// </summary>
    protected bool NeedUpdateFrontCache
    {
        get => _needUpdateFrontCache;
        set => _needUpdateFrontCache = value;
    }

    private bool _RenderObjectPreviousNeedsUpdate;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool RenderObjectPreviousNeedsUpdate

    {
        get { return _RenderObjectPreviousNeedsUpdate; }
        set
        {
            if (_RenderObjectPreviousNeedsUpdate != value)
            {
                _RenderObjectPreviousNeedsUpdate = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Should delete RenderObject when starting new frame rendering
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool RenderObjectNeedsUpdate

    {
        get { return _renderObjectNeedsUpdate; }

        set
        {
            if (_renderObjectNeedsUpdate != value)
            {
                _renderObjectNeedsUpdate = value;
            }
        }
    }

    bool _renderObjectNeedsUpdate = true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void InvalidateCache()
    {
        RenderObjectNeedsUpdate = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void InvalidateCacheWithPrevious()
    {
        InvalidateCache();

        if (IsCacheComposite)
        {
            RenderObjectPreviousNeedsUpdate = true;
        }
    }

    /// <summary>
    /// Used for the Operations cache type to record inside the changed area, if your control is not inside the DrawingRect due to transforms/translations. This is NOT changing the rendering object 
    /// </summary>
    protected virtual SKRect GetCacheRecordingArea(SKRect drawingRect)
    {
        return drawingRect;
    }

    /// <summary>
    /// Normally cache is recorded inside DrawingRect, but you might want to expand this to include shadows around, for example.
    /// </summary>
    protected virtual SKRect GetCacheArea(SKRect value)
    {
        var expand = GetRenderingExpandPixels();
        if (expand.Left != 0 || expand.Top != 0 || expand.Right != 0 || expand.Bottom != 0)
        {
            return new(
                value.Left - (float)Math.Round(expand.Left),
                value.Top - (float)Math.Round(expand.Top),
                value.Right + (float)Math.Round(expand.Right),
                value.Bottom + (float)Math.Round(expand.Bottom)
            );
        }

        return value;
    }

    /// <summary>
    /// Returns true if had drawn.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="widthRequest"></param>
    /// <param name="heightRequest"></param>
    /// <param name="destination"></param>
    /// <param name="scale"></param>
    /// <returns></returns>
    public virtual bool DrawUsingRenderObject(DrawingContext context,
        float widthRequest, float heightRequest)
    {
        if (IsDisposed || IsDisposing || !IsVisible)
            return false;

        Arrange(context.Destination, widthRequest, heightRequest, context.Scale);

        bool willDraw = !CheckIsGhost();
        if (willDraw)
        {
            if (UsingCacheType != SkiaCacheType.None)
            {
                var destination = DrawingRect;
                var recordArea = destination;
                if (UsingCacheType == SkiaCacheType.OperationsFull)
                {
                    recordArea = context.Context.Canvas.LocalClipBounds;
                    destination = recordArea;
                }

                //paint from cache
                var clone = AddPaintArguments(context).WithDestination(destination);

                if (TryUseExistingRenderingObjectOrCreateNewAndPaint(clone, recordArea))
                {
                    ExistingCacheWasRendered = true;
                }
            }
            else
            {
                // NO CACHE, DIRECT PAINT
                DrawDirectInternal(context, DrawingRect);
            }
        }

        FinalizeDrawingWithRenderObject(context); //NeedUpdate will go false

        return willDraw;
    }


    public void PrepareOffscreenCache(DrawingContext clone, SKRect recordArea)
    {
        //use cloned struct in another thread
        _offscreenBakeBusy = true;
        PushToOffscreenRendering(() =>
        {
            try
            {
                //will be executed on background thread in parallel
                var prepared = CreateRenderingObject(clone, recordArea, RenderObjectPreparing,
                    UsingCacheType,
                    (ctx) => { PaintWithEffects(ctx); });

                RenderObjectPreparing = prepared;
                if (prepared != null)
                {
                    RenderObject = prepared;
                    _renderObjectPreparing = null;
                }
            }
            finally
            {
                _offscreenBakeBusy = false;
            }

            // The offscreen bake changed this control's PIXELS without going through Update():
            // a parent that caches OVER us (composite/plane) must be told, or it keeps serving
            // its old snapshot with our placeholder/stale content until an unrelated
            // invalidation. Bare Repaint() only scheduled a frame — the parent's cache stayed
            // valid, so the frame blitted the same stale plane (visible as gaps/stale cells
            // while scrolling a plane-cached list).
            // UNCONDITIONAL: gating on Parent.UpdateLocks lost the wakeup when the bake finished
            // during the queuing frame (parent locked mid-paint) — placeholder frozen until user
            // interaction. A locked parent defers via _neededUpdate; LockUpdate(false) replays.
            Parent?.UpdateByChild(this);
        });
    }

    /// <summary>
    /// Returns true if existing cache was rendered, without creating new one and painting it.
    /// </summary>
    /// <returns></returns>
    public virtual bool TryUseExistingRenderingObjectOrCreateNewAndPaint(DrawingContext ctx, SKRect recordArea)
    {
        if (!UseRenderingObject(ctx, recordArea))
        {
            //record to cache and paint
            if (UsesCacheDoubleBuffering)
            {
                if (!ExistingCacheWasRendered)
                    DrawPlaceholder(ctx);

                PrepareOffscreenCache(ctx, recordArea);
            }
            else
            {
                CreateRenderingObjectAndPaint(ctx, recordArea,
                    (ctx) => { PaintWithEffects(ctx.WithDestination(DrawingRect)); });
            }

            return false;
        }
        else
        {
            return true;
        }
    }

    public virtual VisualLayer? PrepareNode(DrawingContext context, float widthRequest, float heightRequest)
    {
        //this will measure too if needed including deeper
        Arrange(context.Destination, widthRequest, heightRequest, context.Scale);

        bool willDraw = !CheckIsGhost();
        if (willDraw)
        {
            CreateTransformationMatrix(context.Context, DrawingRect);
            var node = CreateRenderedNode(DrawingRect, context.Scale);

            //note UsesCacheDoubleBuffering is going deprecated with 2 passes rendering tree
            //and new logic for ImageCacheComposite needs to be implemented for nodes
            if (UsingCacheType != SkiaCacheType.None)
            {
                var destination = DrawingRect;
                var recordArea = destination;
                if (UsingCacheType == SkiaCacheType.OperationsFull)
                {
                    recordArea = context.Context.Canvas.LocalClipBounds;
                }

                var clone = AddPaintArguments(context).WithDestination(destination);
                CachedObject cache = null;
                if (RenderObject == null || !CheckCachedObjectValid(RenderObject, recordArea, clone.Context))
                {
                    cache = CreateRenderingObject(clone, recordArea, RenderObjectPrevious, UsingCacheType,
                        (ctx) => { PaintWithEffects(ctx); });
                }

                node.Cache = cache;
            }

            // todo doubling children nodes for some reason
            foreach (var child in Views)
            {
                if (child.CanDraw)
                {
                    var childNode = child.PrepareNode(context, SizeRequest.Width, SizeRequest.Height);
                    if (childNode != null)
                    {
                        node.Children.Add(childNode);
                    }
                }
            }

            return node;
        }

        return null;
    }

    /// <summary>
    ///  Not using cache
    /// </summary>
    /// <param name="context">context.Destination can be bigger than drawingRect</param>
    /// <param name="drawingRect">normally equal to DrawingRect</param>
    public virtual void DrawDirectInternal(DrawingContext context, SKRect drawingRect)
    {
        // NO CACHE, DIRECT PAINT
        var destination = context.Destination;

        var clone = AddPaintArguments(context).WithDestination(drawingRect);
        DrawWithClipAndTransforms(clone, drawingRect, true, true, (ctx) =>
        {
            PaintWithEffects(ctx);

            foreach (var postRenderer in EffectPostRenderers)
            {
                postRenderer.Render(ctx.WithDestination(destination));
            }
        });
    }

    /// <summary>
    /// This is NOT calling FinalizeDraw()!
    /// parameter 'area' Usually is equal to DrawingRect
    /// </summary>
    /// <param name="context"></param>
    /// <param name="recordArea"></param>
    /// <param name="action"></param>
    protected void CreateRenderingObjectAndPaint(
        DrawingContext context,
        SKRect recordingArea,
        Action<DrawingContext> action)
    {
        if (recordingArea.Width <= 0 || recordingArea.Height <= 0 || float.IsInfinity(recordingArea.Height) ||
            float.IsInfinity(recordingArea.Width) || IsDisposed || IsDisposing)
        {
            return;
        }

        if (RenderObject != null && !UsesCacheDoubleBuffering)
        {
            //we might come here with an existing RenderingObject if UseRenderingObject returned False
            RenderObject = null;
        }

        RenderObjectNeedsUpdate = false;

        CachedObject oldObject = null; //reusing this
        if (UsesCacheDoubleBuffering)
        {
            oldObject = RenderObject;
        }
        else

            //tried to reuse surface for image SkiaCacheType.Image
            //but is seems to be GCed after GC hits randomly  along with some other object, like shader or something unsure
            //so safer not to reusage at this stage
        if (IsCacheComposite) //_ || UsingCacheType == SkiaCacheType.Image)
        {
            oldObject = RenderObjectPrevious;
        }

        if (oldObject is ISkiaDisposable reused && reused.IsAlive != ObjectAliveType.Alive)
        {
            oldObject = null;
        }

        var created = CreateRenderingObject(context, recordingArea, oldObject, UsingCacheType, action);

        if (created == null)
        {
            return;
        }

        var notValid = RenderObjectNeedsUpdate;
        RenderObject = created;

        if (RenderObject != null)
        {
            DrawRenderObjectInternal(context.WithDestination(RenderObject.RecordingArea), RenderObject);
        }
        else
        {
            notValid = true;
        }

        if (NeedUpdate || notValid) //someone changed us while rendering inner content
        {
            InvalidateCache();
            //Update();
        }
    }
}
