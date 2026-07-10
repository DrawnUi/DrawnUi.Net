#define TMP

using System.Collections;
using System.Collections.Specialized;
using System.Numerics;
using System.Runtime.InteropServices;


namespace DrawnUi.Draw
{
    public partial class SkiaLayout : SkiaControl, ISkiaGridLayout
    {
        public override void OnScaleChanged()
        {
            base.OnScaleChanged();

            InvalidateWithChildren();
        }

        public override bool PreArrange(SKRect destination, float widthRequest, float heightRequest, float scale)
        {
            if (!CanDraw)
                return false;

            if (WillInvalidateMeasure)
            {
                WillInvalidateMeasure = false;
                InvalidateMeasureInternal();
            }

            if (NeedMeasure)
            {
                MeasureSelf(destination, GetWidthRequestPixelsWIthMargins(scale), GetHeightRequestPixelsWIthMargins(scale), scale);
            }
            else
            {
                LastArrangedInside = destination;
            }

            return true;
        }

        public override bool IsGestureForChild(SkiaControlWithRect child, SKPoint point)
        {
            if (this.IsStack)
            {
                bool inside = false;
                if (child.Control != null && !child.Control.IsDisposing && !child.Control.IsDisposed &&
                    !child.Control.InputTransparent && child.Control.CanDraw)
                {
                    var transformed = child.Control.ApplyTransforms(child.HitRect); //instead of HitRect
                    inside = transformed.ContainsInclusive(point.X,
                        point.Y); // || child.Control == Superview.FocusedChild;

                    if (inside)
                    {
                        var stop = 1;
                    }
                }

                return inside;
            }

            return base.IsGestureForChild(child, point);
        }

        public override void ApplyBindingContext()
        {
            UpdateRowColumnBindingContexts();

            base.ApplyBindingContext();
        }

        //todo use rendering tree for templated!!
        //protected override void OnParentVisibilityChanged(bool newvalue)
        //{

        //    base.OnParentVisibilityChanged(newvalue);
        //}


        public override bool ShouldInvalidateByChildren
        {
            get
            {
                if (Type == LayoutType.Grid)
                {
                    return true; //we need this to eventually recalculate spans
                }

                if (!IsTemplated && IsStack)
                    return true;

                // do not invalidate if template didnt change from last time?
                // NOPE template could be the same but size could be different!
                //if (IsTemplated && _measuredNewTemplates)
                //{
                //    return false;
                //}

                return base.ShouldInvalidateByChildren;
            }
        }

        bool _measuredNewTemplates;


        public virtual void OnTemplatesAvailable()
        {
            _measuredNewTemplates = false;
            NeedMeasure = true;
            InvalidateParent();

            WillDrawFromFreshItemssSource = 0;
            WillMeasureFromFreshItemssSource = true;
        }

        protected override ScaledSize SetMeasured(float width, float height, bool widthCut, bool heightCut, float scale)
        {
            _measuredNewTemplates = true;

            return base.SetMeasured(width, height, widthCut, heightCut, scale);
        }


        //bindable property RecyclingTemplate
        public static readonly BindableProperty RecyclingTemplateProperty = BindableProperty.Create(
            nameof(RecyclingTemplate),
            typeof(RecyclingTemplate),
            typeof(SkiaLayout),
            RecyclingTemplate.Enabled,
            propertyChanged: ItemTemplateChanged);

        /// <summary>
        /// In case of ItemsSource+ItemTemplate set will define should we reuse already created views: hidden items views will be reused for currently visible items on screen.
        /// If set to true inside a SkiaScrollLooped will cause it to redraw constantly even when idle because of the looped scroll mechanics.
        /// </summary>
        public RecyclingTemplate RecyclingTemplate
        {
            get { return (RecyclingTemplate)GetValue(RecyclingTemplateProperty); }
            set { SetValue(RecyclingTemplateProperty, value); }
        }

        /// <summary>
        /// Layouts can use this property for custom logic appropriate to layout type.
        /// </summary>
        public bool Invert
        {
            get { return (bool)GetValue(InvertProperty); }
            set { SetValue(InvertProperty, value); }
        }

        public static readonly BindableProperty InvertProperty = BindableProperty.Create(
            nameof(Invert),
            typeof(bool),
            typeof(SkiaLayout),
            false, propertyChanged: NeedInvalidateMeasure);

        //protected override void AdaptCachedLayout(SKRect destination, float scale)
        //{
        //    base.AdaptCachedLayout(destination, scale);

        //    if (Parent == null || Parent is not IDefinesViewport)
        //    {
        //        RenderingViewport = new(DrawingRect);
        //    }
        //}

        //protected override void OnLayoutChanged()
        //{
        //    base.OnLayoutChanged();

        //    if (Parent == null || Parent is not IDefinesViewport)
        //    {
        //        RenderingViewport = new(DrawingRect);
        //    }

        //}

        public static readonly BindableProperty TemplatedHeaderProperty = BindableProperty.Create(
            nameof(TemplatedHeader), typeof(SkiaControl),
            typeof(SkiaControl), null, propertyChanged: ItemTemplateChanged);

        /// <summary>
        /// Kind of BindableLayout.DrawnTemplate
        /// </summary>
        public SkiaControl TemplatedHeader
        {
            get { return (SkiaControl)GetValue(TemplatedHeaderProperty); }
            set { SetValue(TemplatedHeaderProperty, value); }
        }

        public static readonly BindableProperty TemplatedFooterProperty = BindableProperty.Create(
            nameof(TemplatedFooter), typeof(SkiaControl),
            typeof(SkiaControl), null, propertyChanged: ItemTemplateChanged);

        /// <summary>
        /// Kind of BindableLayout.DrawnTemplate
        /// </summary>
        public SkiaControl TemplatedFooter
        {
            get { return (SkiaControl)GetValue(TemplatedFooterProperty); }
            set { SetValue(TemplatedFooterProperty, value); }
        }


        public override bool IsTemplated =>
            ((this.ItemTemplate != null || ItemTemplateType != null) && this.ItemsSource != null);

        public SKRect GetChildRect(int index)
        {
            ISkiaControl child = null;
            if (IsTemplated)
            {
                throw new Exception("Cannot get child rect for a templated view");

                return SKRect.Empty;
            }

            return GetChildRect(child);
        }

        public SKRect GetChildRect(ISkiaControl child)
        {
            if (IsTemplated)
            {
                throw new Exception("Cannot get child rect for a templated view");
            }
            else
            {
                if (child != null)
                    return child.Destination;
            }

            return SKRect.Empty;
        }

        /*
        /// <summary>
        /// TODO for templated stacks this is not optimized to handle cell size
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public SkiaControl GetChildAt(int index)
        {
            if (IsTemplated)
            {
                //todo
                bool useOneTemplate = IsTemplated && //ItemSizingStrategy == ItemSizingStrategy.MeasureFirstItem &&
                                      RecyclingTemplate != RecyclingTemplate.Disabled;

                SkiaControl template = null;
                if (useOneTemplate)
                {
                    template = ChildrenFactory.GetTemplateInstance();
                }

                var child = ChildrenFactory.GetViewForIndex(index, template, 0, true);

                return child;
            }

            return GetOrderedSubviews()[index];
        }
        */

        public SkiaControl GetChildAt(float x, float y)
        {
            if (IsTemplated)
            {
                //todo 
                throw new Exception("Cannot get child at for a templated view");
            }
            else
            {
                foreach (var child in GetUnorderedSubviews())
                {
                    var rect = GetChildRect(child);
                    if (rect.ContainsInclusive(x, y))
                    {
                        return child as SkiaControl;
                    }
                }
            }

            return null;
        }


        public SkiaLayout()
        {
            ChildrenFactory = new(this);

            //PostponeInvalidation(nameof(ApplyItemsSource), ApplyItemsSource);
            //OnItemSourceChanged();
        }

        #region PROPERTIES


        public static readonly BindableProperty RecyclingBufferProperty = BindableProperty.Create(
            nameof(RecyclingBuffer),
            typeof(double),
            typeof(SkiaLayout),
            100.0);

        /// <summary>
        /// Extra buffer zone for avoiding recycling  
        /// Default is 500pts - increase for less jaggy scroll, decrease for more memory efficiency
        /// </summary>
        public double RecyclingBuffer
        {
            get { return (double)GetValue(RecyclingBufferProperty); }
            set { SetValue(RecyclingBufferProperty, value); }
        }

        #endregion

        #region STACK ROW/GRID

        protected List<ISkiaControl> ChildrenGrid { get; set; }


        public void BreakLine()
        {
            LineBreaks.Add(Views.Count);
        }

        protected List<int> LineBreaks = new List<int>();


        public override ScaledRect GetOnScreenVisibleArea(DrawingContext context, Vector2 inflateByPixels = default)
        {
            if (DelegateGetOnScreenVisibleArea != null)
            {
                return DelegateGetOnScreenVisibleArea(inflateByPixels);
            }

            var onscreen = base.GetOnScreenVisibleArea(context, inflateByPixels);

            var visible = SKRect.Intersect(onscreen.Pixels, context.Destination);

            return ScaledRect.FromPixels(visible, RenderingScale);
        }

        /// <summary>
        /// Todo where is this used i forgot completely
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cache"></param>
        public override void DrawRenderObject(DrawingContext context, CachedObject cache)
        {
            var visibleArea = GetOnScreenVisibleArea(context);

            base.DrawRenderObject(context.WithDestination(visibleArea.Pixels), cache);
        }

        protected SkiaControl _emptyView;

        protected virtual void SetupViews()
        {
            if (EmptyView != _emptyView)
            {
                RemoveSubView(_emptyView);
                _emptyView?.Dispose();

                if (EmptyView != null)
                {
                    _emptyView = EmptyView;
                    CheckAndSetupIfEmpty();
                    AddSubView(_emptyView);
                }

                Update();
            }
        }

        private bool _IsEmpty;

        public bool IsEmpty
        {
            get { return _IsEmpty; }
            set
            {
                if (_IsEmpty != value)
                {
                    _IsEmpty = value;
                    OnPropertyChanged();
                    IsEmptyChanged?.Invoke(this, value);
                }
            }
        }

        public event EventHandler<bool> IsEmptyChanged;


        protected virtual void ApplyIsEmpty(bool value)
        {
            IsEmpty = value;

            if (_emptyView != null)
            {
                _emptyView.IsVisible = value;
            }
        }

        protected virtual bool CheckAndSetupIfEmpty()
        {
            var value = false;

            if (ItemTemplate != null)
            {
                value = EffectiveItemsSource == null || EffectiveItemsSource.Count == 0;
            }
            else
            {
                value = this.ChildrenFactory.GetChildrenCount() == 0;
            }

            ApplyIsEmpty(value);

            return value;
        }

        public override string DebugString
        {
            get
            {
                // logical (ItemsSource-space) indices — what the user scrolls by; window offset applied
                var output =
                    $"{Type} `{Tag}`, {MeasuredSize.Pixels.Width:0}x{MeasuredSize.Pixels.Height:0}, visible {FirstVisibleIndex}-{LastVisibleIndex} ({_countVisible}), ";

                if (IsTemplated && MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
                {
                    output += $"measured {LastMeasuredIndex + 1}, ";
                }

                if (IsTemplated || RenderTree == null)
                    return output + ChildrenFactory.GetDebugInfo();

                return output +
                       $"visible {RenderTree.Count}, skipped {ChildrenFactory.GetChildrenCount() - RenderTree.Count}, total {ChildrenFactory.GetChildrenCount()}";
            }
        }

        public ViewsAdapter ChildrenFactory { get; protected set; }


        public static readonly BindableProperty SplitProperty = BindableProperty.Create(
            nameof(Split),
            typeof(int),
            typeof(SkiaLayout),
            0,
            propertyChanged: NeedUpdateItemsSource);

        /// <summary>
        /// For Wrap number of columns/rows to split into, If 0 will use auto, if 1+ will have 1+ columns.
        /// </summary>
        public int Split
        {
            get { return (int)GetValue(SplitProperty); }
            set { SetValue(SplitProperty, value); }
        }

        public static readonly BindableProperty SplitAlignProperty = BindableProperty.Create(
            nameof(SplitAlign),
            typeof(bool),
            typeof(SkiaLayout),
            true, propertyChanged: NeedUpdateItemsSource);

        /// <summary>
        /// Whether should keep same column width among rows
        /// </summary>
        public bool SplitAlign
        {
            get { return (bool)GetValue(SplitAlignProperty); }
            set { SetValue(SplitAlignProperty, value); }
        }

        public static readonly BindableProperty SplitSpaceProperty = BindableProperty.Create(
            nameof(SplitSpace),
            typeof(SpaceDistribution),
            typeof(SkiaLayout),
            SpaceDistribution.Auto,
            propertyChanged: NeedUpdateItemsSource);

        /// <summary>
        /// How to distribute free space between children TODO
        /// </summary>
        public SpaceDistribution SplitSpace
        {
            get { return (SpaceDistribution)GetValue(SplitSpaceProperty); }
            set { SetValue(SplitSpaceProperty, value); }
        }

        public static readonly BindableProperty DynamicColumnsProperty = BindableProperty.Create(
            nameof(DynamicColumns),
            typeof(bool),
            typeof(SkiaLayout),
            false, propertyChanged: NeedUpdateItemsSource);

        /// <summary>
        /// If true, will not create additional columns to match SplitMax if there are less real columns, and take additional space for drawing
        /// </summary>
        public bool DynamicColumns
        {
            get { return (bool)GetValue(DynamicColumnsProperty); }
            set { SetValue(DynamicColumnsProperty, value); }
        }

        #endregion

        #region RENDERiNG

        protected bool ViewportWasChanged { get; set; }

        protected virtual bool DrawChild(DrawingContext ctx, ISkiaControl child)
        {
            if (child == null || child.IsDisposed || child.IsDisposing)
            {
                return false;
            }

            child.OptionalOnBeforeDrawing(); //could set IsVisible or whatever inside

            if (!child.CanDraw)
                return false; //child set himself invisible

            if (ViewportWasChanged)
            {
                //if (child is IInsideViewport viewport)
                //{
                //    var intersection = SKRect.Intersect(_viewport.Pixels, dest);
                //    viewport.OnViewportWasChanged(ScaledRect.FromPixels(intersection, RenderingScale));
                //}

                if (child is ISkiaCell cell)
                {
                    cell.OnScrolled();

                    //Task.Run(() =>
                    //{
                    //    cell.OnScrolled();
                    //}).ConfigureAwait(false);
                }
            }

            child.Render(ctx);

            return true;
        }


        //protected void Build()
        //{
        //	if (AvailableDestination != SKRect.Empty)
        //	{
        //		Measure(AvailableDestination.Width, AvailableDestination.Height);
        //	}
        //	Update();
        //}

        public override void SetChildren(IEnumerable<SkiaControl> views)
        {
            base.SetChildren(views);

            Invalidate();
        }


        protected override void OnMeasured()
        {
            base.OnMeasured();

            _measuredStamp++;
        }

        public override void InvalidateInternal()
        {
            templatesInvalidated = true;

            base.InvalidateInternal();
        }

        protected bool templatesInvalidated;

        public override void InvalidateViewsList()
        {
            base.InvalidateViewsList();

            ActualizeSubviews();
        }

        public virtual void ActualizeSubviews()
        {
            needUpdateViews = false;

            ChildrenFactory?.UpdateViews();
        }

        bool needUpdateViews;

        protected virtual int GetTemplatesPoolPrefill()
        {
            if (RecyclingTemplate == RecyclingTemplate.Disabled && ItemTemplatePoolSize<1)
            {
                return EffectiveItemsSource.Count;
            }

            if (ItemTemplatePoolSize>0)
            {
                return GetTemplatesPoolLimit();
            }

            var mult = 1;
            if (Split > 0)
                mult = Split;

            return mult * 3;
        }

        protected virtual int GetTemplatesPoolLimit()
        {
            if (ItemTemplatePoolSize > 0)
                return ItemTemplatePoolSize;

            if (EffectiveItemsSource == null)
                return 0;

            var mult = 1;
            if (Split > 0)
                mult = Split;

            if (IsTemplated && RecyclingTemplate != RecyclingTemplate.Disabled)
            {
                // TRUE recycling default: a small rotating set derived from the viewport, NEVER the whole
                // source (1000 items must not mean 1000 cells). Floor = what the layout actually REALIZES
                // at once — the expanded viewport (VirtualisationInflated overscan) holds ~3 viewports of
                // cells and a cap below realized demand returns null views (holes). +1 viewport of slack
                // for in-flight returns/preparation. Override explicitly via ItemTemplatePoolSize.
                // Pre-first-layout both indices are the -1 sentinel: (-1)-(-1)+1 computes to a BOGUS 1
                // (not caught by a "< 1" check), capping the pool at ~6 and starving the ReserveTemplates
                // prefill ("Reserve 10 -> 5 cells"). Unknown viewport = use the fallback estimate.
                var visible = LastVisibleIndexLocal - FirstVisibleIndexLocal + 1;
                if (FirstVisibleIndexLocal < 0 || LastVisibleIndexLocal < FirstVisibleIndexLocal)
                    visible = 8; // viewport unknown before first layout, corrected on the next contexts swap
                // An EXPLICIT ReserveTemplates must fit the cap, or the background warm-up can never reach
                // its target no matter how correct the viewport estimate is.
                var floor = Math.Max(visible * 4, visible + ReserveTemplates);
                // Cache-all ceiling: lists at/under this keep EVERY cell baked (re-scroll never rebakes = smooth);
                // above it, cap to the viewport-realized floor for memory ("1000 items != 1000 cells").
                // 256 is the tunable memory/smoothness line — raise for smoother huge lists, lower to save RAM.
                const int cacheAllLimit = 256;
                if (EffectiveItemsSource.Count <= Math.Max(floor + mult * 2, cacheAllLimit))
                    return EffectiveItemsSource.Count + mult * 2;
                return floor + mult * 2;
            }

            return EffectiveItemsSource.Count + mult * 2;
        }

        public override void OnChildrenChanged()
        {
            base.OnChildrenChanged();

            if (!NeedMeasure && Type != LayoutType.Absolute)
            {
                Invalidate();
            }
            else
            {
                Update();
            }
        }

        public override void Invalidate()
        {
            base.Invalidate();

            Update();
        }


        SemaphoreSlim semaphoreItemTemplates = new(1);

        protected async Task CreateTemplatesInBackground()
        {
            await semaphoreItemTemplates.WaitAsync();
            try
            {
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
            }
            finally
            {
                semaphoreItemTemplates.Release();
            }
        }


        public override ScaledSize MeasureAbsolute(SKRect rectForChildrenPixels, float scale)
        {
            var childrenCount = ChildrenFactory.GetChildrenCount(); // Cache count
            if (childrenCount > 0)
            {
                if (!IsTemplated)
                {
                    var children = GetUnorderedSubviews();
                    return MeasureContent(children, rectForChildrenPixels, scale);
                }

                //DATA TEMPLATED

                var maxHeight = 0.0f;
                var maxWidth = 0.0f;

                bool standalone = false;
                if (!ChildrenFactory.TemplatesAvailable)
                {
                    return ScaledSize.CreateEmpty(scale);
                }

                if (this.MeasureItemsStrategy == MeasuringStrategy.MeasureFirst)
                {
                    standalone = true;
                    var template = ChildrenFactory.GetTemplateInstance();
                    var child = ChildrenFactory.GetViewForIndex(0, template, 0, true);

                    var measured = MeasureChild(child, rectForChildrenPixels.Width, rectForChildrenPixels.Height,
                        scale);
                    if (!measured.IsEmpty)
                    {
                        // FastMeasurement: skip FILL checks for performance
                        if (FastMeasurement)
                        {
                            if (measured.Pixels.Width > maxWidth)
                                maxWidth = measured.Pixels.Width;
                            if (measured.Pixels.Height > maxHeight)
                                maxHeight = measured.Pixels.Height;
                        }
                        else
                        {
                            if (measured.Pixels.Width > maxWidth
                                && child.HorizontalOptions.Alignment != LayoutAlignment.Fill)
                                maxWidth = measured.Pixels.Width;

                            if (measured.Pixels.Height > maxHeight
                                && child.VerticalOptions.Alignment != LayoutAlignment.Fill)
                                maxHeight = measured.Pixels.Height;
                        }
                    }

                    ChildrenFactory.ReleaseTemplateInstance(template);
                }
                else if (this.MeasureItemsStrategy == MeasuringStrategy.MeasureAll
                         || RecyclingTemplate == RecyclingTemplate.Disabled)
                {
                    // Optimize: only allocate collection if templated
                    List<SkiaControl> cellsToRelease = null;
                    cellsToRelease = new List<SkiaControl>();

                    try
                    {
                        for (int index = 0; index < childrenCount; index++)
                        {
                            var child = ChildrenFactory.GetViewForIndex(index, null, 0, true);
                            cellsToRelease?.Add(child);

                            if (child == null)
                            {
                                break; //unexpected but..
                            }

                            var measured = MeasureChild(child, rectForChildrenPixels.Width,
                                rectForChildrenPixels.Height, scale);
                            if (!measured.IsEmpty)
                            {
                                // FastMeasurement: skip FILL checks for performance
                                if (true) //FastMeasurement)
                                {
                                    if (measured.Pixels.Width > maxWidth)
                                        maxWidth = measured.Pixels.Width;
                                    if (measured.Pixels.Height > maxHeight)
                                        maxHeight = measured.Pixels.Height;
                                }
                                else
                                {
                                    if (measured.Pixels.Width > maxWidth &&
                                        child.HorizontalOptions.Alignment != LayoutAlignment.Fill)
                                        maxWidth = measured.Pixels.Width;
                                    if (measured.Pixels.Height > maxHeight &&
                                        child.VerticalOptions.Alignment != LayoutAlignment.Fill)
                                        maxHeight = measured.Pixels.Height;
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (cellsToRelease?.Count > 0)
                        {
                            foreach (var cell in cellsToRelease)
                            {
                                ChildrenFactory.ReleaseViewInUse(cell);
                            }
                        }
                    }
                }

                return ScaledSize.FromPixels(maxWidth, maxHeight, scale);

            }
            //empty container
            else if (NeedAutoHeight || NeedAutoWidth)
            {
                return ScaledSize.CreateEmpty(scale);
                //return SetMeasured(0, 0, scale);
            }


            return ScaledSize.FromPixels(rectForChildrenPixels.Width, rectForChildrenPixels.Height, scale);
        }

        protected object lockMeasureLayout = new();

        public virtual ScaledSize MeasureLayout(MeasureRequest request, bool force)
        {
            //until we implement 2-threads rendering this is needed for ImageDoubleBuffered cache rendering
            if (IsDisposing || IsDisposed)
                return ScaledSize.Default;

            lock (lockMeasureLayout)
            {
                _measuredNewTemplates = false;
                CancelBackgroundMeasurement();
                _measuredItems.Clear();

                var constraints = GetMeasuringConstraints(request);

                GridStructureMeasured = null;

                if (!CheckAndSetupIfEmpty())
                {
                    if (IsTemplated) //fix threads conflict when templates are initialized in background thread
                    {
                        var canMeasureTemplates = ChildrenFactory.TemplatesAvailable || force;

                        if (!canMeasureTemplates)
                            return ScaledSize.CreateEmpty(request.Scale);

                        ChildrenFactory.InvalidateAllPooledCells();
                    }

                    switch (Type)
                    {
                        case LayoutType.Absolute:
                            ContentSize = MeasureAbsolute(constraints.Content, request.Scale);
                            break;

                        case LayoutType.Grid:

                            ContentSize = MeasureGrid(constraints.Content, request.Scale);
                            break;

                        case LayoutType.Column:
                        case LayoutType.Row:
                            if (IsTemplated) //fix threads conflict when templates are initialized in background thread
                            {
                                if (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
                                {
                                    ContentSize = MeasureList(constraints.Content, request.Scale);
                                    break;
                                }

                                ContentSize = MeasureStackTemplated(constraints.Content, request.Scale);
                            }
                            else
                            {
                                ContentSize = MeasureStackNonTemplated(constraints.Content, request.Scale);
                            }

                            break;

                        case LayoutType.Wrap:
                            ContentSize = MeasureWrap(constraints.Content, request.Scale);
                            break;

                        default:
                            ContentSize = ScaledSize.FromPixels(constraints.Content.Width, constraints.Content.Height,
                                request.Scale);
                            break;
                    }
                }
                else
                {
                    ContentSize = MeasureAbsoluteBase(constraints.Content, request.Scale);
                }

                if (MeasureItemsStrategy != MeasuringStrategy.MeasureVisible)
                {
                    WillMeasureFromFreshItemssSource = false;
                }

                return SetMeasuredAdaptToContentSize(constraints, request.Scale);
            }
        }

        protected readonly object _lockTemplates = new object();

        /// <summary>
        /// If you call this while measurement is in process (IsMeasuring==True) will return last measured value.
        /// </summary>
        /// <param name="widthConstraint"></param>
        /// <param name="heightConstraint"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
        {
            //background measuring or invisible
            if (IsMeasuring //|| !CanDraw
                || (IsTemplated && ChildrenFactory.TemplatesBusy))
            {
                NeedRemeasuring = true;
                return MeasuredSize;
            }

            if (!IsVisible)
            {
                return SetMeasuredAsEmpty(scale);
            }

            try
            {
                lock (LockMeasure)
                {
                    IsMeasuring = true;

                    InitializeDefaultContent();

                    var request = CreateMeasureRequest(widthConstraint, heightConstraint, scale);
                    //this optimization WAS nice (byebye) but not working for Grid inside a recycled cell where request is same but height is different 
                    //if (request.IsSame)
                    //{
                    //    return MeasuredSize;
                    //}

                    if (request.WidthRequest == 0 || request.HeightRequest == 0)
                    {
                        InvalidateCacheWithPrevious();
                        return SetMeasuredAsEmpty(request.Scale);
                    }

                    if (IsTemplated)
                    {
                        //this is used for initial views creation or a rare case when we manually invalidated templates
                        lock (_lockTemplates)
                        {
                            if (ChildrenFactory.TemplatesInvalidated
                                && !ChildrenFactory.TemplesInvalidating)
                            {
                                ChildrenFactory.TemplesInvalidating = true;
                                ApplyNewItemsSource = false;
                                ChildrenFactory.InitializeTemplates(
                                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset),
                                    CreateContentFromTemplate, EffectiveItemsSource,
                                    GetTemplatesPoolLimit(),
                                    GetTemplatesPoolPrefill());
                            }
                        }
                    }
                    else
                    {
                        if (needUpdateViews)
                        {
                            ActualizeSubviews();
                        }
                    }

                    var layout = MeasureLayout(request, false);

                    if (layout.Pixels.Height<0 || layout.Pixels.Width<0)
                    {
                        float width=0, height = 0;
                        bool widthCut = layout.Pixels.Width > 0;
                        bool heightCut = layout.Pixels.Height > 0;
                        if (widthCut)
                        {
                            width = layout.Pixels.Width;
                        }
                        if (heightCut)
                        {
                            height = layout.Pixels.Height;
                        }
                        return SetMeasured(width, height, widthCut, heightCut, scale);
                    }

                    return layout;
                } //end lock
            }
            catch (Exception e)
            {
                Super.Log(e);
                return MeasuredSize;
            }
            finally
            {
                IsMeasuring = false;

                //LockUpdate(false); never enable this here, breaks stuff
            }
        }

        public override void ApplyMeasureResult()
        {
            var hadStackMeasure = StackStructureMeasured != null;
            ApplyStackMeasureResult();
            if (hadStackMeasure)
                CheckAndSetupIfEmpty();

            if (GridStructureMeasured != null)
            {
                GridStructure = GridStructureMeasured;
                GridStructureMeasured = null;
                CheckAndSetupIfEmpty();
            }

            base.ApplyMeasureResult();
        }


        /// <summary>
        /// Will use this when drawing
        /// </summary>
        protected HashSet<SkiaControl> InvalidatedChildrenInternal { get; set; }

        protected override void Draw(DrawingContext context)
        {
            if (IsDisposed || IsDisposing)
                return;

            InvalidatedChildrenInternal = InvalidatedChildren;
            InvalidatedChildren = new();

            ApplyMeasureResult();

            base.Draw(context); //will call DrawStack

            ViewportWasChanged = false;
        }

        /// <summary>
        /// Will be modified by InvalidateByChild
        /// </summary>
        protected HashSet<SkiaControl> InvalidatedChildren { get; set; } = new();

        public override void InvalidateByChild(SkiaControl child)
        {
            InvalidatedChildren.Add(child);

            if (!IsStack && Type != LayoutType.Grid && (!NeedAutoSize && (child.NeedAutoSize || IsTemplated)) ||
                (IsTemplated && (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible || MeasureItemsStrategy == MeasuringStrategy.MeasureFirst)))
            {
                UpdateByChild(child); //simple update
                return;
            }

            base.InvalidateByChild(child); //calling Invalidate
        }

        bool _trackWasDrawn;
        protected long WillDrawFromFreshItemssSource;
        protected bool WillMeasureFromFreshItemssSource;

        protected override void Paint(DrawingContext ctx)
        {
            if (ctx.Destination.Width == 0 || ctx.Destination.Height == 0)
                return;

            // A plane-bake pass (SkiaCachedStack async record, worker thread) is a PURE READ: draining
            // pending structure changes here would consume them into the bake's frozen snapshot (the live
            // structure never receives them = lost updates, the historical corruption class), and the
            // update-lock/composition setup belong to the live frame.
            var bakePass = IsPlaneBakePass;

            if (!bakePass)
            {
                LockUpdate(true);
            }

            try
            {
                if (!bakePass)
                {
                    // Apply all pending structure changes to StackStructure
                    ApplyStructureChanges();

                    SetupRenderingWithComposition(ctx);
                }

                base.Paint(ctx);

                var rectForChildren = ContractPixelsRect(ctx.Destination, ctx.Scale, UsePadding);

                var drawnChildrenCount = 0;

                //placeholder for empty
                if (_emptyView != null && _emptyView.IsVisible)
                {
                    drawnChildrenCount = DrawViews(ctx.WithDestination(rectForChildren));
                }
                else if (Type == LayoutType.Grid) //todo add optimization for OptimizeRenderingViewport
                {
                    drawnChildrenCount = DrawChildrenGrid(ctx.WithDestination(rectForChildren));
                }
                else
                    //stacklayout
                    if (IsStack)
                    {
                        var structure = GetStackStructure();
                        if (structure != null)// && structure.GetCount() > 0)
                        {
                            //if (IsTemplated && MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
                            //{
                            //    drawnChildrenCount = DrawList(ctx.WithDestination(rectForChildren), structure);
                            //}
                            //else
                            {
                                drawnChildrenCount = DrawStack(ctx.WithDestination(rectForChildren), structure);
                            }
                        }
                    }
                    else
                    //absolute layout
                    {
                        drawnChildrenCount = DrawViews(ctx.WithDestination(rectForChildren));
                    }

                if (!bakePass)
                {
                    ApplyIsEmpty(drawnChildrenCount == 0);

                    if (!_trackWasDrawn && LayoutReady)
                    {
                        _trackWasDrawn = true;
                        OnAppeared();
                    }
                }
            }
            catch (Exception e)
            {
                Super.Log(e);
            }
            finally
            {
                if (!bakePass)
                {
                    LockUpdate(false);
                }
            }
        }

        public override void UpdateByChild(SkiaControl child)
        {
            base.UpdateByChild(child);

        }

        public override void OnDisposing()
        {
            CancelBackgroundMeasurement();
            _measuredItems.Clear();

            IsEmptyChanged = null;

            ChildrenFactory?.Dispose();

            ClearChildren();

            ClearDirtyChildren();

            DirtyChildrenInternal.Clear();

            StackStructure?.Clear();
            StackStructureMeasured?.Clear();

            OnGestures = null;

            base.OnDisposing();
        }

        protected override int DrawViews(DrawingContext context)
        {
            var drawn = 0;

            if (IsTemplated)
            {
                if (ChildrenFactory.TemplatesAvailable)
                {
                    using var children = ChildrenFactory.GetViewsIterator();
                    drawn = RenderViewsList(context, children);
                }

                if (drawn == 0 && _emptyView != null && _emptyView.IsVisible)
                {
                    var drawViews = new List<SkiaControl> { _emptyView };
                    RenderViewsList(context, drawViews);
                    return 0;
                }
            }
            else
            {
                drawn = base.DrawViews(context);

                if (drawn == 0 && _emptyView != null && _emptyView.IsVisible)
                {
                    var drawViews = new List<SkiaControl> { _emptyView };
                    RenderViewsList(context, drawViews);
                    return 0;
                }
            }

            return drawn;
        }

        /// <summary>
        /// Column/Row/Stack
        /// </summary>
        public bool IsStack
        {
            get { return this.Type == LayoutType.Column || Type == LayoutType.Row || Type == LayoutType.Wrap; }
        }

        public static readonly BindableProperty TypeProperty = BindableProperty.Create(nameof(Type), typeof(LayoutType),
            typeof(SkiaLayout),
            LayoutType.Absolute,
            propertyChanged: NeedInvalidateMeasure);

        public LayoutType Type
        {
            get { return (LayoutType)GetValue(TypeProperty); }
            set { SetValue(TypeProperty, value); }
        }

        #endregion

        #region ItemsSource

        //public static readonly BindableProperty InitializeTemplatesInBackgroundDelayProperty = BindableProperty.Create(
        //    nameof(InitializeTemplatesInBackgroundDelay),
        //    typeof(int),
        //    typeof(SkiaLayout),
        //    0, propertyChanged: NeedUpdateItemsSource);

        ///// <summary>
        ///// Whether should initialize templates in background instead of blocking UI thread, default is 0.
        ///// Set your delay in Milliseconds to enable.
        ///// When this is enabled and RecyclingTemplate is Disabled will also measure the layout in background
        ///// when templates are available without blocking UI-tread.
        ///// After that OnTemplatesAvailable will be called on parent layout.
        ///// </summary>
        //public int InitializeTemplatesInBackgroundDelay
        //{
        //    get { return (int)GetValue(InitializeTemplatesInBackgroundDelayProperty); }
        //    set { SetValue(InitializeTemplatesInBackgroundDelayProperty, value); }
        //}

        public static readonly BindableProperty MeasureItemsStrategyProperty = BindableProperty.Create(
            nameof(MeasureItemsStrategy),
            typeof(MeasuringStrategy),
            typeof(SkiaLayout),
            MeasuringStrategy.MeasureFirst,
            propertyChanged: NeedUpdateItemsSource);

        public MeasuringStrategy MeasureItemsStrategy
        {
            get { return (MeasuringStrategy)GetValue(MeasureItemsStrategyProperty); }
            set { SetValue(MeasureItemsStrategyProperty, value); }
        }

        public static readonly BindableProperty ItemTemplatePoolSizeProperty = BindableProperty.Create(
            nameof(ItemTemplatePoolSize),
            typeof(int),
            typeof(SkiaLayout),
            -1, propertyChanged: NeedUpdateItemsSource);

        /// <summary>
        /// Default is -1, the number od template instances will not be less than data collection count.
        /// You can manually set to ta specific number to fill your viewport etc.
        /// Beware that if you set this to a number that will not be enough to fill the viewport
        /// binding contexts will contasntly be changing triggering screen update.
        /// </summary>
        public int ItemTemplatePoolSize
        {
            get { return (int)GetValue(ItemTemplatePoolSizeProperty); }
            set { SetValue(ItemTemplatePoolSizeProperty, value); }
        }

        public static readonly BindableProperty ReserveTemplatesProperty = BindableProperty.Create(
            nameof(ReserveTemplates),
            typeof(int),
            typeof(SkiaLayout),
            2, propertyChanged: NeedUpdateItemsSource);

        /// <summary>
        /// For recycled cells: Default is 2, how many item templates above visible in viewport we must reserve in pool.
        /// </summary>
        public int ReserveTemplates
        {
            get { return (int)GetValue(ReserveTemplatesProperty); }
            set { SetValue(ReserveTemplatesProperty, value); }
        }

        public static readonly BindableProperty EmptyViewProperty = BindableProperty.Create(
            nameof(EmptyView),
            typeof(SkiaControl),
            typeof(SkiaLayout),
            null, propertyChanged: (b, o, n) =>
            {
                if (b is SkiaLayout control)
                {
                    control.SetupViews();
                }
            });

        public SkiaControl EmptyView
        {
            get { return (SkiaControl)GetValue(EmptyViewProperty); }
            set { SetValue(EmptyViewProperty, value); }
        }

        public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
            nameof(ItemsSource),
            typeof(IList),
            typeof(SkiaLayout),
            null,
            //validateValue: (bo, v) => v is IList,
            propertyChanged: ItemsSourcePropertyChanged);

        public IList ItemsSource
        {
            get => (IList)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }


        #region BUILT-IN SOURCE WINDOW

        /// <summary>
        /// Auto-engage the built-in sliding window (<see cref="ItemsSourceWindow"/>) when a templated
        /// ItemsSource holds more items than this. The pipeline then materializes only a bounded slice and
        /// pages within the source internally; LoadMoreCommand fires only at real source edges.
        /// 0 or negative disables auto-windowing.
        /// </summary>
        public static int WindowSourceThreshold = 300;

        /// <summary>Window capacity in viewports, tuned from the real visible range after first draw.</summary>
        public static int WindowSourceViewports = 4;

        private ItemsSourceWindow _itemsWindow;

        /// <summary>Active built-in window over a large ItemsSource; null when not engaged.</summary>
        public ItemsSourceWindow ItemsWindow => _itemsWindow;

        /// <summary>
        /// The collection the pipeline actually materializes: the internal window slice when engaged,
        /// else the raw ItemsSource. All measure/structure/pool/LoadMore internals read THIS.
        /// </summary>
        public IList EffectiveItemsSource => _itemsWindow?.Items ?? ItemsSource;

        protected void DetachItemsWindow()
        {
            if (_itemsWindow == null)
                return;

            _itemsWindow.Items.CollectionChanged -= OnItemsSourceCollectionChanged;
            _itemsWindow.Detach();
            _itemsWindow = null;
        }

        /// <summary>
        /// Engages the built-in window when the (templated) ItemsSource is big enough. The window's slice
        /// takes over pipeline eventing; the controller itself observes the user's source collection.
        /// </summary>
        protected void TryEngageItemsWindow()
        {
            var source = ItemsSource;
            if (_itemsWindow != null || source == null || WindowSourceThreshold <= 0
                || source.Count <= WindowSourceThreshold
                || (ItemTemplate == null && ItemTemplateType == null))
                return;

            _itemsWindow = new ItemsSourceWindow(this, source);
            _itemsWindow.Items.CollectionChanged += OnItemsSourceCollectionChanged;

            // the controller observes the user source now — pipeline must not also react to it directly
            if (source is INotifyCollectionChanged sourceCollection)
            {
                sourceCollection.CollectionChanged -= OnItemsSourceCollectionChanged;
            }

            Debug.WriteLine(
                $"[SkiaLayout] items window ENGAGED: {source.Count} source items, resident {_itemsWindow.Items.Count}");
        }

        #endregion

        private static void ItemsSourcePropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            var skiaControl = (SkiaLayout)bindable;

            if (oldvalue != null)
            {
                if (oldvalue is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= skiaControl.OnItemsSourceCollectionChanged;
                }
            }

            skiaControl.DetachItemsWindow();
            skiaControl.TryEngageItemsWindow();

            // window engaged: its slice raises the pipeline events, the controller observes the user source
            if (skiaControl._itemsWindow == null && newvalue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged -= skiaControl.OnItemsSourceCollectionChanged;
                newCollection.CollectionChanged += skiaControl.OnItemsSourceCollectionChanged;
            }

            skiaControl.OnItemSourceChanged();
        }

        private static void NeedUpdateItemsSource(BindableObject bindable, object oldvalue, object newvalue)
        {
            var skiaControl = (SkiaLayout)bindable;

            skiaControl.ApplyItemsSource();
        }

        public override void OnItemTemplateChanged()
        {
            // XAML may set ItemTemplate after ItemsSource — re-check window engagement now that we're templated
            TryEngageItemsWindow();
            ApplyItemsSource();
        }

        public bool ApplyNewItemsSource { get; set; }

        public virtual void OnItemSourceChanged()
        {
            ApplyItemsSource();
        }

        /// <summary>
        /// Invalidate and re-apply ItemsSource
        /// </summary>
        public virtual void ApplyItemsSource()
        {
            //if (!string.IsNullOrEmpty(Tag))
            //    Debug.WriteLine($"OnItemSourceChanged {Tag} {IsTemplated} {IsMeasuring}");
            if (!IsTemplated ||
                !BindingContextWasSet &&
                ItemsSource ==
                null) //do not create items from templates until the context was changed properly to avoid bugs
            {
                return;
            }

            this.ChildrenFactory.TemplatesInvalidated = true;
            ApplyNewItemsSource = true;
            Invalidate();

            PostDrawAction(OnItemsSourceChangesApplied);
        }

        public virtual void ResetScroll()
        {
            if (Parent is IDefinesViewport viewport)
            {
                viewport.ScrollTo(0, 0, 0, false);
            }
        }

        /// <summary>
        /// Determines if collection changes should preserve existing measurement structure
        /// </summary>
        protected virtual bool ShouldPreserveStructureOnCollectionChange(NotifyCollectionChangedEventArgs args)
        {
            if (args.Action == NotifyCollectionChangedAction.Reset || IsFullCollectionReplace(args)
                || StackStructure == null || StackStructure.Length == 0)
                return false;

            if (MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
                return true;

            // MeasureFirst = uniform rows (first item's size + spacing): Add/Remove/Replace are pure
            // arithmetic on the structure (followers slide by count*stride) — no rebuild, no binds,
            // no measures. Applied by TryApplyUniformAddMeasureFirst / the generic remove path
            // (Replace decomposes into Remove+Add). Move and resets keep the full rebuild.
            if (MeasureItemsStrategy == MeasuringStrategy.MeasureFirst
                && args.Action is NotifyCollectionChangedAction.Add
                    or NotifyCollectionChangedAction.Remove
                    or NotifyCollectionChangedAction.Replace)
                return true;

            return false;
        }

        /// <summary>
        /// A Replace that swaps the ENTIRE collection at once (windowed-list jump via ObservableRangeCollection
        /// .ReplaceRange: clears + re-adds all, raising one Replace at index 0 covering every item). The
        /// structure-preserving Replace path can't service this — it carries the old window's partial
        /// measurement onto unrelated new items and starves background measurement — so it routes through the
        /// clean reset cycle instead (which still resolves InitializeTemplates to InitializeSoft, because the
        /// ItemsSource reference is unchanged, so NO adapter rebuild happens).
        /// </summary>
        protected bool IsFullCollectionReplace(NotifyCollectionChangedEventArgs args)
        {
            return args.Action == NotifyCollectionChangedAction.Replace
                   && args.NewStartingIndex == 0
                   && args.NewItems != null
                   && EffectiveItemsSource != null
                   && args.NewItems.Count == EffectiveItemsSource.Count;
        }


        /// <summary>
        /// Enhanced collection change handler with smart handling and fallback
        /// </summary>
        protected virtual void OnItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (!IsTemplated)
                return;

            if (ViewsAdapter.LogEnabled)
            {
                Trace.WriteLine($"[SkiaLayout] {Tag} Collection changed: {args.Action}, " +
                                $"OldIndex: {args.OldStartingIndex}, NewIndex: {args.NewStartingIndex}, " +
                                $"OldCount: {args.OldItems?.Count ?? 0}, NewCount: {args.NewItems?.Count ?? 0}");
            }

            if (ShouldPreserveStructureOnCollectionChange(args))
            {
                // NEW: Structure-preserving logic for MeasureVisible strategy
                HandleCollectionChangeWithStructurePreservation(args);
                return;
            }

            lock (LockMeasure)
            {

                // For very rapid changes, fall back to full reset
                if (ChildrenFactory.TemplatesBusy || ChildrenFactory.TemplesInvalidating)
                {
                    if (ViewsAdapter.LogEnabled)
                    {
                        Trace.WriteLine($"[SkiaLayout] {Tag} Templates busy, deferring change handling");
                    }

                    // Defer the change until templates are ready
                    PostponeInvalidation($"collection_change_{args.Action}", () =>
                    {
                        OnItemsSourceCollectionChanged(sender, args); //re-enter
                    });
                    return;
                }

                var fullReplace = IsFullCollectionReplace(args);

                if (args.Action == NotifyCollectionChangedAction.Reset)
                {
                    ApplyResetChange();
                }
                else if (fullReplace)
                {
                    // Full-window swap: clear measurement/structure so background measurement runs fresh on
                    // the new window (exact content size), but keep templates intact. InitializeTemplates
                    // below resolves to InitializeSoft (Action=Replace, same ItemsSource ref) — no rebuild.
                    ResetMeasurementForReplace();
                }
                ApplyNewItemsSource = false;

                //we could enter here from a different thread:
                SafeAction(() =>
                {
                    ChildrenFactory.InitializeTemplates(args, CreateContentFromTemplate, EffectiveItemsSource,
                        GetTemplatesPoolLimit(),
                        GetTemplatesPoolPrefill());

                    if (args.Action == NotifyCollectionChangedAction.Reset)
                    {
                        ResetScroll();
                        Invalidate();
                    }
                    else if (fullReplace)
                    {
                        // Don't ResetScroll: the caller issues a ScrollToIndex for the jump target.
                        Invalidate();
                    }
                    else if (NeedAutoSize || MeasuredSize.Pixels.Height == 0 || MeasuredSize.Pixels.Width == 0 || MeasureItemsStrategy != MeasuringStrategy.MeasureVisible)
                    {
                        Invalidate();
                    }

                    PostDrawAction(OnItemsSourceChangesApplied);
                });
            }
        }

        /// <summary>
        /// Triggers after a new ItemsSource was set or an observable collection of an existing one was changed
        /// </summary>
        protected virtual void OnItemsSourceChangesApplied()
        {
            ItemsSourceChangesApplied?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler ItemsSourceChangesApplied;

        /// <summary>
        /// Handles collection changes while preserving existing measurement structure
        /// </summary>
        protected virtual void HandleCollectionChangeWithStructurePreservation(NotifyCollectionChangedEventArgs args)
        {
            if (ViewsAdapter.LogEnabled)
            {
                Trace.WriteLine($"[SkiaLayout] {Tag} Structure-preserving collection change: {args.Action}");
            }

            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    HandleStructurePreservingAdd(args);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    HandleStructurePreservingRemove(args);
                    break;

                case NotifyCollectionChangedAction.Replace:
                    HandleStructurePreservingReplace(args);
                    break;

                case NotifyCollectionChangedAction.Move:
                    HandleStructurePreservingMove(args);
                    break;

                case NotifyCollectionChangedAction.Reset:
                    HandleStructurePreservingReset(args);
                    break;

                default:
                    // Fallback to existing logic for unknown actions
                    goto ExistingLogic;
            }

            Repaint();

            return;

ExistingLogic:
// Fall back to existing logic if needed
            lock (LockMeasure)
            {
                SafeAction(() =>
                {
                    ChildrenFactory.InitializeTemplates(args, CreateContentFromTemplate, EffectiveItemsSource,
                        GetTemplatesPoolLimit(), GetTemplatesPoolPrefill());
                });

                Repaint();
            }
        }

        /// <summary>
        /// Stages a structure change for processing during rendering pipeline
        /// </summary>
        protected virtual void StageStructureChange(StructureChange change)
        {
            try
            {
                lock (_structureChangesLock)
                {
                    _pendingStructureChanges.Add(change);
                }

                if (ViewsAdapter.LogEnabled)
                {
                    Trace.WriteLine($"[SkiaLayout] {Tag} Staged structure change: {change.Type}");
                }
            }
            catch (Exception ex)
            {
                if (ViewsAdapter.LogEnabled)
                {
                    Trace.WriteLine($"[SkiaLayout] {Tag} Failed to stage structure change: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Called by templated cells to report visibility changes.
        /// This stages the visibility change to be applied during the next rendering cycle.
        /// </summary>
        /// <param name="cellIndex">The index of the cell in the ItemsSource</param>
        /// <param name="isVisible">The new visibility state</param>
        public virtual void ReportChildVisibilityChanged(int cellIndex, bool isVisible)
        {
            ReportChildVisibilityChanged(cellIndex, 1, isVisible);
        }

        /// <summary>
        /// Called by templated cells to report visibility changes for multiple cells.
        /// This stages the visibility change to be applied during the next rendering cycle.
        /// </summary>
        /// <param name="startIndex">The starting index of cells in the ItemsSource</param>
        /// <param name="count">The number of cells to change</param>
        /// <param name="isVisible">The new visibility state</param>
        public virtual void ReportChildVisibilityChanged(int startIndex, int count, bool isVisible)
        {
            if (!IsTemplated)
                return;

            StageStructureChange(new StructureChange(StructureChangeType.VisibilityChange, MeasureStamp)
            {
                StartIndex = startIndex,
                Count = count,
                IsVisible = isVisible
            });

            if (ViewsAdapter.LogEnabled)
            {
                Trace.WriteLine(
                    $"[SkiaLayout] {Tag} Staged visibility change for {count} cells starting at {startIndex}: {isVisible}");
            }
        }

        /// <summary>
        /// Handles Add collection changes while preserving existing structure
        /// </summary>
        protected virtual void HandleStructurePreservingAdd(NotifyCollectionChangedEventArgs args)
        {
            if (ViewsAdapter.LogEnabled)
            {
                Super.Log($"[SkiaLayout] {Tag} Structure-preserving ADD: {args.NewItems?.Count ?? 0} items at index {args.NewStartingIndex}");
            }

            // Cancel any ongoing background measurement to avoid conflicts
            CancelBackgroundMeasurement();

            // Capture the data-contexts snapshot NOW, on the mutating (UI) thread, serialized with the change.
            // The apply below (staged change) and the InitializeSoft closure both run on the render thread, where
            // reading live ItemsSource races the next main-thread mutation (Android) -> torn indices/overlap/crash.
            var contextsSnapshot = ChildrenFactory.CaptureContextsSnapshot(EffectiveItemsSource);

            // Stage the Add change for rendering pipeline
            StageStructureChange(new StructureChange(StructureChangeType.Add, MeasureStamp)
            {
                StartIndex = args.NewStartingIndex,
                Count = args.NewItems?.Count ?? 0,
                Items = args.NewItems?.Cast<object>().ToList(),
                ContextsSnapshot = contextsSnapshot
            });

            // Adapter refresh ownership per Add:
            // - Head inserts (any strategy) and ALL MeasureFirst adds: APPLY-OWNED (ApplyInsertShift or
            //   the apply-side net, in stage order). A stage refresh is queued (SafeAction) and executes
            //   AFTER the applies, re-applying its by-then-stale snapshot over newer state — in a window
            //   slide's add+trim turn that flipped the adapter back to the pre-trim count until the NEXT
            //   slide (the browser "Data jumping 48/64" oscillation with pool churn spikes).
            // - MeasureVisible non-head appends/mid-inserts (chat LoadMore): STAGE-OWNED for now — their
            //   apply side is not airtight (structure-null early return during seeding, stamp drops),
            //   and background measurement needs the new count immediately. They are the only mutation
            //   in their turn, so their snapshot cannot go stale. Unify once apply ownership is airtight
            //   (planned with MeasureVisible + built-in window work).
            bool applyRefreshesAdapter = args.NewStartingIndex == 0
                                         || MeasureItemsStrategy == MeasuringStrategy.MeasureFirst;

            lock (LockMeasure)
            {
                SafeAction(() =>
                {
                    if (!applyRefreshesAdapter)
                    {
                        // PRESERVE STRUCTURE: updates data contexts without destroying measurements
                        ChildrenFactory.InitializeSoft(false, EffectiveItemsSource, GetTemplatesPoolLimit(), contextsSnapshot);

                        if (ViewsAdapter.LogEnabled)
                        {
                            Super.Log($"[SkiaLayout] {Tag} Structure preserved using InitializeSoft");
                        }
                    }

                    Repaint();
                });
            }
        }

        /// <summary>
        /// Handles Remove collection changes while preserving existing structure
        /// </summary>
        protected virtual void HandleStructurePreservingRemove(NotifyCollectionChangedEventArgs args)
        {
            if (ViewsAdapter.LogEnabled)
            {
                Trace.WriteLine(
                    $"[SkiaLayout] {Tag} Structure-preserving REMOVE: {args.OldItems?.Count ?? 0} items at index {args.OldStartingIndex}");
            }

            // Cancel any ongoing background measurement to avoid conflicts
            CancelBackgroundMeasurement();

            // Capture the snapshot on the mutating (UI) thread (see HandleStructurePreservingAdd) before the
            // render-thread apply/InitializeSoft re-read the live collection off-thread.
            var contextsSnapshot = ChildrenFactory.CaptureContextsSnapshot(EffectiveItemsSource);

            // Stage the Remove change for rendering pipeline. Tail removal is detected HERE,
            // synchronously with the mutation: at apply time the live count may already include
            // a subsequent same-frame prepend (window trim before backward LoadMore).
            StageStructureChange(new StructureChange(StructureChangeType.Remove, MeasureStamp)
            {
                StartIndex = args.OldStartingIndex,
                Count = args.OldItems?.Count ?? 0,
                TailRemoval = args.OldStartingIndex == (EffectiveItemsSource?.Count ?? -1),
                ContextsSnapshot = contextsSnapshot
            });

            // NO adapter refresh here: EVERY Remove apply path (head trim fast path, tail trim fast
            // path, generic mid-remove) ends in ApplyRemoveShift, which releases removed views, rekeys
            // survivors and swaps this snapshot in — atomically with the structure shift on the render
            // thread. Refreshing here too served post-remove contexts against the pre-shift structure
            // until the apply landed: every visible cell rebound to a shifted context and re-baked its
            // cache, then rebound back after the shift (the window-slide lag spike / "0/24 in use").
            lock (LockMeasure)
            {
                SafeAction(() =>
                {
                    if (ViewsAdapter.LogEnabled)
                    {
                        Trace.WriteLine(
                            $"[SkiaLayout] {Tag} remove change staged, adapter refresh deferred to apply");
                    }

                    // Trigger repaint without invalidation to apply staged changes
                    Update();
                });
            }
        }

        /// <summary>
        /// Handles Replace collection changes while preserving existing structure
        /// </summary>
        protected virtual void HandleStructurePreservingReplace(NotifyCollectionChangedEventArgs args)
        {
            if (ViewsAdapter.LogEnabled)
            {
                Trace.WriteLine(
                    $"[SkiaLayout] {Tag} Structure-preserving REPLACE: {args.NewItems?.Count ?? 0} items at index {args.NewStartingIndex}");
            }

            // Cancel any ongoing background measurement to avoid conflicts
            CancelBackgroundMeasurement();

            // Capture the snapshot on the mutating (UI) thread (see HandleStructurePreservingAdd) before the
            // render-thread apply/InitializeSoft re-read the live collection off-thread.
            var contextsSnapshot = ChildrenFactory.CaptureContextsSnapshot(EffectiveItemsSource);

            // Stage the Replace change for rendering pipeline
            StageStructureChange(new StructureChange(StructureChangeType.Replace, MeasureStamp)
            {
                StartIndex = args.NewStartingIndex,
                Count = args.NewItems?.Count ?? 0,
                OldCount = args.OldItems?.Count ?? 0,
                Items = args.NewItems?.Cast<object>().ToList(),
                ContextsSnapshot = contextsSnapshot
            });

            lock (LockMeasure)
            {
                SafeAction(() =>
                {
                    // Use InitializeSoft to preserve structure while updating templates
                    ChildrenFactory.InitializeSoft(false, EffectiveItemsSource, GetTemplatesPoolLimit(), contextsSnapshot);

                    if (ViewsAdapter.LogEnabled)
                    {
                        Trace.WriteLine(
                            $"[SkiaLayout] {Tag} Structure preserved using InitializeSoft, replace change staged");
                    }

                    // Trigger repaint without invalidation to apply staged changes
                    Update();
                });
            }
        }

        /// <summary>
        /// Handles Move collection changes while preserving existing structure
        /// </summary>
        protected virtual void HandleStructurePreservingMove(NotifyCollectionChangedEventArgs args)
        {
            if (ViewsAdapter.LogEnabled)
            {
                Trace.WriteLine(
                    $"[SkiaLayout] {Tag} Structure-preserving MOVE: from index {args.OldStartingIndex} to {args.NewStartingIndex}");
            }

            // TODO: Implement move logic that updates StackStructure and _measuredItems
            // For now, fall back to existing logic
            lock (LockMeasure)
            {
                SafeAction(() =>
                {
                    ChildrenFactory.InitializeTemplates(args, CreateContentFromTemplate, EffectiveItemsSource,
                        GetTemplatesPoolLimit(), GetTemplatesPoolPrefill());
                    Invalidate();
                });
            }
        }


        /// <summary>
        /// Adapter-facing accessor for <see cref="GetTemplatesPoolLimit"/>: the ViewsAdapter re-applies the
        /// pool ceiling on every data-contexts swap so an UNSET <see cref="ItemTemplatePoolSize"/> tracks the
        /// live ItemsSource count (windowed sources shrink the pool after trims instead of hoarding).
        /// </summary>
        internal int GetTemplatesPoolLimitPublic() => GetTemplatesPoolLimit();

        /// <summary>
        /// Handles Reset collection changes while preserving existing structure
        /// </summary>
        protected virtual void HandleStructurePreservingReset(NotifyCollectionChangedEventArgs args)
        {
            if (ViewsAdapter.LogEnabled)
            {
                Trace.WriteLine($"[SkiaLayout] {Tag} Structure-preserving RESET");
            }

            // Reset requires full invalidation, but we can still be smarter about it
            lock (LockMeasure)
            {
                SafeAction(() =>
                {
                    ChildrenFactory.InitializeTemplates(args, CreateContentFromTemplate, EffectiveItemsSource,
                        GetTemplatesPoolLimit(), GetTemplatesPoolPrefill());
                    ResetScroll();
                    Invalidate();
                });
            }
        }

        /// <summary>
        /// Force a full refresh of all cached views (useful for debugging)
        /// </summary>
        public void RefreshAllViews()
        {
            if (!IsTemplated)
                return;

            lock (LockMeasure)
            {
                ChildrenFactory.MarkAllViewsAsHidden();
                Update();
            }
        }

        /// <summary>
        /// Get debug information about cached views
        /// </summary>
        public string GetCacheDebugInfo()
        {
            if (!IsTemplated || ChildrenFactory == null)
                return "Not templated";

            var info = ChildrenFactory.GetDebugInfo();

            // Add validation info
            var isValid = true;
            var issues = new List<string>();

            try
            {
                // This is a simplified validation - the full version is in ViewsAdapter
                if (EffectiveItemsSource != null && EffectiveItemsSource.Count > 0)
                {
                    // Add any specific validation checks here
                }
            }
            catch (Exception e)
            {
                isValid = false;
                issues.Add($"Validation error: {e.Message}");
            }

            return $"{info}, Valid: {isValid}" +
                   (issues.Count > 0 ? $", Issues: {string.Join(", ", issues)}" : "");
        }

        /// <summary>
        /// Enhanced debug printing
        /// </summary>
        public override void OnPrintDebug()
        {
            base.OnPrintDebug();

            if (IsTemplated)
            {
                Trace.WriteLine($"[SkiaLayout] {Tag} Cache Debug: {GetCacheDebugInfo()}");
                ChildrenFactory.PrintDebugVisible();
            }
        }

        //public override void OnPrintDebug()
        //{
        //    Trace.WriteLine($"ViewsAdapter tpls: {ChildrenFactory.PoolSize}/{ChildrenFactory.PoolMaxSize}");
        //    if (IsTemplated)
        //    {
        //        ChildrenFactory.PrintDebugVisible();
        //    }
        //}

        #endregion

        protected override void OnLayoutReady()
        {
            base.OnLayoutReady();
        }

        protected override void OnLayoutChanged()
        {
            base.OnLayoutChanged();

            _visibleAreaCache = null;
        }

        public virtual void OnAppearing()
        {
        }

        public virtual void OnDisappearing()
        {
        }

        public virtual void OnAppeared()
        {
        }

        public virtual void OnDisappeared()
        {
        }

        public virtual void OnLoaded()
        {
        }

        public virtual ContainsPointResult GetVisibleChildIndexAt(SKPoint point)
        {
            //relative inside parent:
            var asSpan = RenderTree.AsSpans();
            for (int i = 0; i < asSpan.Length; i++)
            {
                var child = asSpan[i];

                if (child.Rect.ContainsInclusive(point))
                {
                    return new ContainsPointResult() { Index = child.Index, Area = child.Rect, Point = point };
                }
            }

            return ContainsPointResult.NotFound();
        }

        public ContainsPointResult GetChildIndexAt(SKPoint point)
        {
            //todo

            return ContainsPointResult.NotFound();
        }
    }
}
