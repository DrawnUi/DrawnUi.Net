using System.Numerics;
using System.Runtime.CompilerServices;

namespace DrawnUi.Draw;

public partial class SkiaScroll
{
    /// <summary>
    /// Keeps visible content pinned when content above the viewport changes size (e.g. a virtualizing
    /// layout removes/inserts items above the viewport and re-flows the rest). Shifts the vertical
    /// viewport offset by <paramref name="deltaPoints"/>. Called by the content layout after it
    /// resolves the new position of the anchored (first-visible) item. No-op for tiny deltas.
    /// </summary>
    public void OffsetVisibleAnchorY(float deltaPoints)
    {
        if (Math.Abs(deltaPoints) < 0.01f)
            return;

        ViewportOffsetY += deltaPoints;

        // Panning is incremental over _panningCurrentOffsetPts; without shifting that baseline the
        // next pan move would compute from the stale base and revert the anchor correction.
        _panningCurrentOffsetPts.Y += deltaPoints;

        // A running fling/scroll animator writes ViewportOffsetY every frame from its own trajectory and
        // would instantly revert the anchor correction. Translate the active trajectory by the same delta
        // so it keeps targeting the same content after the window shifted.
        if (_animatorFlingY != null && _animatorFlingY.IsRunning)
        {
            _animatorFlingY.Shift(deltaPoints);

            // A fast fling gets its duration CUT to stop exactly at the content edge
            // (PrepareToFlingAfterInitialized). The content just grew past that edge, so the cut
            // trajectory would race at full speed and slam-stop at the OLD edge's content position.
            // Re-plan it with the remaining velocity once this frame's bounds are refreshed.
            if (_changeSpeed != null)
                _replanFlingY = true;
        }

        if (_vectorAnimatorBounceY != null && _vectorAnimatorBounceY.IsRunning)
            _vectorAnimatorBounceY.Stop(); // bounce target is stale after a content shift; let it re-evaluate
    }

    /// <summary>
    /// Set when a duration-cut fling must be re-planned against refreshed content bounds
    /// (content grew during the fling, e.g. backward LoadMore prepend). Consumed in Draw.
    /// </summary>
    protected bool _replanFlingY;

    public float ViewportOffsetY
    {
        get { return _viewportOffsetY; }

        set
        {
            if (_viewportOffsetY != value)
            {
                _viewportOffsetY = value;
                if (!NeedUpdate)
                    Update();
                //OnPropertyChanged();
            }
        }
    }

    protected float _viewportOffsetY;

    public float ViewportOffsetX
    {
        get { return _viewportOffsetX; }

        set
        {
            if (_viewportOffsetX != value)
            {
                _viewportOffsetX = value;
                if (!NeedUpdate)
                    Update();
                //OnPropertyChanged();
            }
        }
    }

    protected float _viewportOffsetX;

    /// <summary>
    /// 0.0 - 1.0
    /// </summary>
    public double ScrollProgressY
    {
        get
        {
            if (ContentOffsetBounds.Height == 0)
            {
                return 0;
            }

            return 1 - (ContentOffsetBounds.Height + InternalViewportOffset.Pixels.Y) / ContentOffsetBounds.Height;
        }
    }

    /// <summary>
    /// 0.0 - 1.0
    /// </summary>
    public double ScrollProgressX
    {
        get
        {
            if (ContentOffsetBounds.Width == 0)
            {
                return 0;
            }

            return 1 - (ContentOffsetBounds.Width + InternalViewportOffset.Pixels.X) / ContentOffsetBounds.Width;
        }
    }

    private SKRect _lastContentBounds = new SKRect();

    protected virtual void InitializeViewport(float scale)
    {
        _loadMoreBottomTriggeredAt = 0;
        _loadMoreTopTriggeredAt = 0;

        _lastContentBounds = ContentOffsetBounds;

        ContentOffsetBounds = GetContentOffsetBounds();

        HasContentToScroll = ptsContentHeight > Viewport.Units.Height || ptsContentWidth > Viewport.Units.Width;

        _scrollMinX = ContentOffsetBounds.Left;
        if (_scrollMinX >= 0)
        {
            ViewportOffsetX = 0;
        }

        _scrollMaxX = 0;

        _scrollMinY = ContentOffsetBounds.Top;
        if (_scrollMinY >= 0)
        {
            ViewportOffsetY = 0;
        }

        _scrollMaxY = 0;

        IsViewportReady = true;
        onceAfterInitializeViewport = true;
    }

    bool onceAfterInitializeViewport;

    public bool IsViewportReady { get; protected set; }

    public LinearDirectionType ScrollingDirection { get; protected set; }

    protected virtual void CheckAndSetIsStillAnimating()
    {
        if (!_animatorFlingY.IsRunning
            && !_animatorFlingX.IsRunning
            && !_vectorAnimatorBounceY.IsRunning
            && !_vectorAnimatorBounceX.IsRunning)
        {
            IsAnimating = false;
            Repaint(); //we need this for after scrolling events
        }
    }

    protected virtual void InitializeScroller(float scale)
    {
        if (_vectorAnimatorBounceY == null)
        {
            _vectorAnimatorBounceY = new(this)
            {
                OnStart = () => { IsAnimating = true; },
                OnStop = () =>
                {
                    UpdateLoadingLock(false);
                    IsSnapping = false;
                    if (_vectorAnimatorBounceY.WasStarted)
                    {
                        CheckAndSetIsStillAnimating();
                    }
                },
                OnUpdated = (value) =>
                {
                    ViewportOffsetY = (float)value; //not clamped
                }
            };

            _vectorAnimatorBounceX = new(this)
            {
                OnStart = () => { IsAnimating = true; },
                OnStop = () =>
                {
                    UpdateLoadingLock(false);
                    IsSnapping = false;
                    if (_vectorAnimatorBounceX.WasStarted)
                    {
                        CheckAndSetIsStillAnimating();
                    }
                },
                OnUpdated = (value) =>
                {
                    ViewportOffsetX = (float)value; //not clamped
                }
            };

            _animatorFlingX = new(this)
            {
                UseInterpolator = true,
                OnStart = () =>
                {
                    //_isSnapping = false;
                    IsAnimating = true;
                    OnScrollerStarted();
                },
                OnStop = () =>
                {
                    if (_animatorFlingX.WasStarted)
                    {
                        OnScrollerStopped();
                        CheckAndSetIsStillAnimating();
                    }
                },
                OnUpdated = (value) =>
                {
                    var clamped = ClampOffset((float)value, 0, ContentOffsetBounds);
                    ViewportOffsetX = clamped.X;

                    OnScrollerUpdated();
                }
            };

            _animatorFlingY = new(this)
            {
                UseInterpolator = true,
                OnStart = () =>
                {
                    IsAnimating = true;
                    //_isSnapping = false;
                    OnScrollerStarted();
                },
                OnStop = () =>
                {
                    if (_animatorFlingY.WasStarted)
                    {
                        OnScrollerStopped();
                        CheckAndSetIsStillAnimating();
                    }
                },
                OnUpdated = (value) =>
                {
                    var clamped = ClampOffset(0, (float)value, ContentOffsetBounds);
                    ViewportOffsetY = clamped.Y;

                    OnScrollerUpdated();
                }
            };

            _scrollerX = new(this)
            {
                OnStart = () => { IsAnimating = true; },
                OnStop = () =>
                {
                    IsSnapping = false;
                    if (_scrollerX.WasStarted)
                    {
                        CheckAndSetIsStillAnimating();
                    }
                    //SkiaImageLoadingManager.Instance.IsLoadingLocked = false;
                }
            };

            _scrollerY = new(this)
            {
                OnStart = () => { IsAnimating = true; },
                OnStop = () =>
                {
                    IsSnapping = false;
                    if (_scrollerY.WasStarted)
                    {
                        CheckAndSetIsStillAnimating();
                    }
                }
            };
        }

        if (_vectorAnimatorBounceY.IsRunning)
        {
            _vectorAnimatorBounceY.Stop();
        }

        if (_vectorAnimatorBounceX.IsRunning)
        {
            _vectorAnimatorBounceX.Stop();
        }

        SetDetectIndexChildPoint(TrackIndexPosition);

        this.UpdateVisibleIndex();

        ExecuteDelayedScrollOrders();

        if (CheckNeedToSnap())
            Snap(0);
    }

    /// <summary>
    /// Use Range scroller, offset in Units
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="animate"></param>
    public void ScrollToX(float offset, bool animate)
    {
        if (animate)
        {
            _scrollerX.Start(
                (value) => { ViewportOffsetX = (float)value; },
                InternalViewportOffset.Units.X, offset, (uint)ScrollingSpeedMs, ScrollingEasing);
        }
        else
        {
            ViewportOffsetX = offset;
            IsSnapping = false;
        }
    }

    /// <summary>
    /// Use Range scroller, offset in Units
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="animate"></param>
    protected void ScrollToY(float offset, bool animate)
    {
        if (animate)
        {
            _scrollerY.Start(
                (value) => { ViewportOffsetY = (float)value; },
                InternalViewportOffset.Units.Y, offset, (uint)ScrollingSpeedMs, ScrollingEasing);
        }
        else
        {
            ViewportOffsetY = offset;
            IsSnapping = false;
        }
    }

    protected virtual void OnScrollerStarted()
    {
        UpdateLoadingLock(new Vector2(
            _animatorFlingX.Parameters.InitialVelocity,
            _animatorFlingY.Parameters.InitialVelocity)
        );
    }

    protected virtual void OnScrollerUpdated()
    {
        UpdateLoadingLock(new Vector2(
            _animatorFlingX.CurrentVelocity,
            _animatorFlingY.CurrentVelocity));
    }

    protected virtual void BounceIfNeeded(ScrollFlingAnimator animator)
    {
        if (animator.SelfFinished)
        {
            var remainingVelocity = animator.Parameters.VelocityAt(animator.Speed);

            var velocity = remainingVelocity;

            if (Math.Abs(remainingVelocity) > MaxBounceVelocity)
            {
                velocity = Math.Sign(remainingVelocity) * MaxBounceVelocity;
            }

            var swipeThreshold = ThesholdSwipeOnUp * RenderingScale;
            if (Math.Abs(velocity) > swipeThreshold)
            {
                if (animator == _animatorFlingY)
                {
                    BounceY((float)ViewportOffsetY, _axis.Y, velocity);
                }
                else if (animator == _animatorFlingX)
                {
                    BounceX((float)ViewportOffsetX, _axis.X, velocity);
                }
            }
        }
    }


    protected virtual void OnScrollerStopped()
    {
        UpdateLoadingLock(false);

        //if (CheckNeedToSnap())
        //{
        //    Snap(SystemAnimationTimeSecs);
        //}
        //else
        {
            //scroll ended prematurely by our intent because it would end past the bounds
            if (Bounces)
            {
                if (_changeSpeed != null)
                {
                    BounceIfNeeded(_animatorFlingY);
                    BounceIfNeeded(_animatorFlingX);
                }
            }
        }
    }

    public virtual void ExecuteDelayedScrollOrders()
    {
        if (OrderedScrollToIndex.IsSet)
        {
            ExecuteScrollToIndexOrder();
        }
        else
        {
            ExecuteScrollToOrder();
        }
    }

    /*

    basic concept:

    when finger goes up we check where the scrolling would end with current velocity.
    if it is outside of the bounds we adjust the scroling duration so it ends near the bounds,
    otherwise we start scrolling animator as usual.

    when scrolling animator stops natually
    we check if we are outside of the bounds then start bouncing animator if needed

    when animator passes offsets to props they get clamped, see below

    if the finger goes down we stop animators unnaturally

    when the finger is down we can pan: we apply rubber clamp to offsets if bounce prop is true,
    otherwise we apply simple clamp

     */

    //deceleration slow 0.999
    // deceleration normal 0.998
    // deceleration fast 0.99

    void BounceX(float offsetFrom, float offsetTo, float velocity)
    {
        //Super.Log($"[SCROLL] {this.Tag} *BOUNCE* to {offsetTo.Y} v {velocity.Y}..");

        var displacement = offsetFrom - offsetTo;

        //Debug.WriteLine($"[BOUNCE] {offsetFrom} - {offsetTo} with {velocity}");

        if (displacement != 0)
        {
            var spring = new Spring((float)(1 * (1 + RubberDamping)), 200, (float)(0.5f * (1 + RubberDamping)));
            _animatorFlingX.Stop();
            _vectorAnimatorBounceX.Initialize(offsetTo, displacement, velocity, spring);
            _vectorAnimatorBounceX.Start();
        }
        else
        {
            IsSnapping = false;
        }
    }

    void BounceY(float offsetFrom, float offsetTo, float velocity)
    {
        //Super.Log($"[SCROLL] {this.Tag} *BOUNCE* to {offsetTo.Y} v {velocity.Y}..");

        var displacement = offsetFrom - offsetTo;

        //Debug.WriteLine($"[BOUNCE] {offsetFrom} - {offsetTo} with {velocity}");

        if (displacement != 0)
        {
            _animatorFlingY.Stop();
            var spring = new Spring((float)(1 * (1 + RubberDamping)), 200, (float)(0.5f * (1 + RubberDamping)));
            _vectorAnimatorBounceY.Initialize(offsetTo, displacement, velocity, spring);
            _vectorAnimatorBounceY.Start();
        }
        else
        {
            IsSnapping = false;
        }
    }

    /// <summary>
    /// This uses whole viewport size, do not use this for snapping
    /// </summary>
    /// <param name="overscrollPoint"></param>
    /// <param name="contentRect"></param>
    /// <param name="viewportSize"></param>
    /// <returns></returns>
    public static SKPoint GetClosestSidePoint(SKPoint overscrollPoint, SKRect contentRect, SKSize viewportSize)
    {
        SKPoint closestPoint = new SKPoint();

        // The overscrollPoint represents the negative of the content offset, so we need to reverse it for calculation
        SKPoint contentOffset = new SKPoint(-overscrollPoint.X, -overscrollPoint.Y);

        var width = contentRect.Width - viewportSize.Width;
        if (width < 0)
            width = 0;

        if (contentOffset.X < 0) //scrolling to  right
            closestPoint.X = contentRect.Left;
        else if (contentOffset.X > 0) //scrolling to left
            closestPoint.X = width;
        else
            closestPoint.X = contentOffset.X;

        var height = contentRect.Height - viewportSize.Height;
        if (height < 0)
            height = 0;

        if (contentOffset.Y < 0) //scrolling to bottom
            closestPoint.Y = contentRect.Top;
        else if (contentOffset.Y > 0) //scrolling to top
            closestPoint.Y = height;
        else
            closestPoint.Y = contentOffset.Y;

        // Reverse the offset back to the overscroll representation for the result
        closestPoint.X = -closestPoint.X;
        closestPoint.Y = -closestPoint.Y;

        return closestPoint;
    }

    public static SKPoint ClosestPoint(SKRect rect, SKPoint point)
    {
        SKPoint result = point;

        if (!rect.ContainsInclusive(point))
        {
            if (point.X < rect.Left)
                result.X = rect.Left;
            else if (point.X > rect.Right)
                result.X = rect.Right;

            if (point.Y < rect.Top)
                result.Y = rect.Top;
            else if (point.Y > rect.Bottom)
                result.Y = rect.Bottom;
        }

        return result;
    }

    /// <summary>
    /// Whether the scrolling offset in inside scrollable bounds or not
    /// </summary>
    /// <param name="offset"></param>
    /// <returns></returns>
    protected virtual bool OffsetOk(Vector2 offset)
    {
        if (offset.Y >= ContentOffsetBounds.Top && offset.Y <= ContentOffsetBounds.Bottom
                                                && offset.X >= ContentOffsetBounds.Left &&
                                                offset.X <= ContentOffsetBounds.Right)
            return true;

        return false;
    }

    public bool OverScrolled
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { return OverscrollDistance != Vector2.Zero; }
    }

    protected float ptsContentWidth;

    protected float ptsContentHeight;

    /// <summary>
    /// There are the bounds the scroll offset can go to..
    /// This is NOT the bounds for the whole content.
    /// In POINTS not pixels!!!
    /// </summary>
    public virtual SKRect GetContentOffsetBounds()
    {
        ptsContentWidth = ContentSize.Units.Width;
        ptsContentHeight = ContentSize.Units.Height;

        // Managed (planes) virtualization renders per-plane sliding windows, so the shared measured
        // ContentSize is only a small seed window and can even collapse once everything is measured.
        // Derive the scroll extent from a STABLE average-item estimate (avg * item count) that matches
        // the per-plane window grid, so the scroll range always spans the whole virtual list.
        if (UseVirtual && Content is SkiaLayout vlayout && vlayout.IsTemplated
            && vlayout.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible
            && vlayout.ItemsSource != null && vlayout.ItemsSource.Count > 0)
        {
            float scale = (float)RenderingScale;
            if (scale <= 0) scale = 1;
            float avgPx = vlayout.GetAverageItemHeightPixels(scale);
            float spacingPx = (float)(vlayout.Spacing * scale);
            double estTotalPts = ((avgPx + spacingPx) * vlayout.ItemsSource.Count) / scale;

            if (Orientation == ScrollOrientation.Vertical && estTotalPts > ptsContentHeight)
                ptsContentHeight = (float)estTotalPts;
            else if (Orientation == ScrollOrientation.Horizontal && estTotalPts > ptsContentWidth)
                ptsContentWidth = (float)estTotalPts;
        }

        if (Orientation == ScrollOrientation.Vertical)
        {
            ptsContentHeight += HeaderSize.Units.Height + FooterSize.Units.Height + (float)ContentOffset;
        }

        if (Orientation == ScrollOrientation.Horizontal)
        {
            ptsContentWidth += HeaderSize.Units.Width + FooterSize.Units.Width + (float)ContentOffset;
        }

        var width = ptsContentWidth - MeasuredSize.Units.Width;
        var height = ptsContentHeight - MeasuredSize.Units.Height;

        if (height < 0)
            height = 0;

        if (width < 0)
            width = 0;

        var rect = new SKRect(-width, -height, 0, 0);

        return rect;
    }

    /// <summary>
    ///
    /// In POINTS not pixels!!!
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public Vector2 CalculateOverscrollDistance(float x, float y)
    {
        float overscrollX = 0f;
        float overscrollY = 0f;

        if (x > _scrollMaxX)
        {
            overscrollX = x - _scrollMaxX;
        }
        else if (x < _scrollMinX)
        {
            overscrollX = -(_scrollMinX - x);
        }

        if (y > _scrollMaxY)
        {
            overscrollY = y - _scrollMaxY;
        }
        else if (y < _scrollMinY)
        {
            overscrollY = -(_scrollMinY - y);
        }

        //if (overscrollY != 0)
        //{
        //    Debug.WriteLine($"[SCROLL] overscroll Y {overscrollY}");
        //}

        return new Vector2(overscrollX, overscrollY);
    }

    protected double _minVelocity = 1.5;

    private float _DecelerationRatio = 0.002f;

    public float DecelerationRatio
    {
        get { return _DecelerationRatio; }
        set
        {
            if (_DecelerationRatio != value)
            {
                _DecelerationRatio = value;
                OnPropertyChanged();
            }
        }
    }

    public void UpdateFriction()
    {
        var friction = FrictionScrolled;
        if (friction < 0.1)
        {
            //silent clamp
            friction = 0.1f;
        }

        DecelerationRatio = (float)friction / 100f; // 0.2 => 0.002
    }

    public virtual bool StartToFlingFrom(ScrollFlingAnimator animator, float from, float velocity)
    {
        var contentOffset = from;

        animator.InitializeWithVelocity(contentOffset, velocity, 1f - DecelerationRatio);

        if (PrepareToFlingAfterInitialized(animator))
        {
            animator.RunAsync(null).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    protected virtual async Task<bool> FlingFrom(ScrollFlingAnimator animator, float from, float velocity)
    {
        //todo - add cancellation support

        //	Trace.WriteLine($"[FLING] velocity {velocity}");

        var contentOffset = from; // new float((float)ViewportOffsetX, (float)ViewportOffsetY);

        animator.InitializeWithVelocity(contentOffset, velocity, 1f - DecelerationRatio);

        return await FlingAfterInitialized(animator);
    }

    protected virtual async Task<bool> FlingToAuto(ScrollFlingAnimator animator, float from, float to,
        float changeSpeedSecs = 0)
    {
        var velocity = animator.Parameters.VelocityToZero(from, to, changeSpeedSecs);

        animator.InitializeWithVelocity(from, velocity, 1f - DecelerationRatio);

        if (changeSpeedSecs > 0)
            animator.Speed = changeSpeedSecs;

        return await FlingAfterInitialized(animator);
    }

    protected virtual async Task<bool> FlingTo(ScrollFlingAnimator animator, float from, float to, float timeSeconds)
    {
        var velocity = animator.Parameters.VelocityTo(from, to, timeSeconds);

        animator.InitializeWithVelocity(from, velocity, 1f - DecelerationRatio);

        animator.Speed = timeSeconds;

        return await FlingAfterInitialized(animator);
    }

    protected virtual bool PrepareToFlingAfterInitialized(ScrollFlingAnimator animator)
    {
        var destination = animator.Parameters.Destination;
        bool offsetOk = true;

        var destinationPoint = SKPoint.Empty;
        if (animator == _animatorFlingX)
        {
            destinationPoint = new SKPoint(destination, 0);
            offsetOk = OffsetOk(new(destination, 0));
        }
        else if (animator == _animatorFlingY)
        {
            destinationPoint = new SKPoint(0, destination);
            offsetOk = OffsetOk(new(0, destination));
        }

        _changeSpeed = null;

        if (!offsetOk) //detected that scroll will end past the bounds
        {
            var clamped = ClampOffset((float)destinationPoint.X, (float)destinationPoint.Y, ContentOffsetBounds, true);
            var closestPoint = new SKPoint(clamped.X, clamped.Y);

            if (animator == _animatorFlingX)
            {
                _axis = _axis with { X = closestPoint.X };
                _changeSpeed = animator.Parameters.DurationToValue(closestPoint.X);
                animator.Speed = _changeSpeed.Value;
            }
            else if (animator == _animatorFlingY)
            {
                _axis = _axis with { Y = closestPoint.Y };
                _changeSpeed = animator.Parameters.DurationToValue(closestPoint.Y);
                animator.Speed = _changeSpeed.Value;
            }
        }

        return animator.Speed > 0;
    }

    protected async Task<bool> FlingAfterInitialized(ScrollFlingAnimator animator)
    {
        if (PrepareToFlingAfterInitialized(animator))
        {
            await animator.RunAsync(null);

            IsSnapping = false;

            return true;
        }

        return false;
    }

    /// <summary>
    /// We might order a scroll before the control was drawn, so it's a kind of startup position
    /// saved every time one calls ScrollTo
    /// </summary>
    public ScrollToPointOrder OrderedScrollTo = ScrollToPointOrder.NotValid;

    /// <summary>
    /// We might order a scroll before the control was drawn, so it's a kind of startup position
    /// saved every time one calls ScrollToIndex
    /// </summary>
    protected ScrollToIndexOrder OrderedScrollToIndex;

    public bool OrderedScrollToIndexIsSet
    {
        get
        {
            return OrderedScrollToIndex.IsSet;
        }
    }

    /// <summary>
    /// True while an explicit ScrollToIndex order is pending. The head-insert viewport pin
    /// (CommitPendingHeadInsert) is suppressed when this is set: an explicit scroll target and the
    /// position-preserving pin are mutually exclusive intents — honoring both in the same frame causes
    /// a 1-frame blink (e.g. a just-sent message that orders ScrollToIndex(0)).
    /// </summary>
    public bool HasPendingScrollOrder => OrderedScrollToIndex.IsSet;

    /// <summary>
    /// In Units
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="animate"></param>
    protected void ScrollToOffset(Vector2 targetOffset, float maxTimeSecs)
    {
        StopScrolling();

        if (maxTimeSecs > 0 && Height > 0)
        {
            ScrollToX(targetOffset.X, true);
            ScrollToY(targetOffset.Y, true);
        }
        else
        {
            ViewportOffsetX = targetOffset.X;
            ViewportOffsetY = targetOffset.Y;
            IsSnapping = false;
            this.UpdateVisibleIndex();
        }
    }

    public virtual void MoveToY(float value)
    {
        if (!ScrollLocked)
        {
            ViewportOffsetY = value;
        }
    }

    public virtual void MoveToX(float value)
    {
        if (!ScrollLocked)
        {
            ViewportOffsetX = value;
        }
    }

    public void ScrollToIndex(int index, bool animate, RelativePositionType option = RelativePositionType.Start,
        bool clamp = false)
    {
        //saving to use upon creating control if this was called before its internal structure was really created
        OrderedScrollToIndex = new()
        {
            Animated = animate,
            RelativePosition = option,
            Index = index,
            Clamp = clamp
        };

        ExecuteScrollToIndexOrder();
    }

    public bool ExecuteScrollToOrder()
    {
        if (OrderedScrollTo.IsValid)
        {
            ScrollToOffset(new Vector2(OrderedScrollTo.Location.X, OrderedScrollTo.Location.Y), OrderedScrollTo.MaxTimeSecs);
            OrderedScrollTo = ScrollToPointOrder.NotValid;
            return true;
        }

        return false;
    }

    public bool ExecuteScrollToIndexOrder()
    {
        if (OrderedScrollToIndex.IsSet)
        {
            // Already AT the target? Consume the order now — there is nothing to scroll. This runs even
            // while structure is pending/measuring (unlike the deferral guards below): an order issued
            // while the view is pinned at the target (e.g. ScrollToIndex(0) right after a head-insert at
            // the newest message) would otherwise stay pending and re-fire on the next user scroll,
            // yanking the view back to the target before continuing. We only consume on an EXACT,
            // computable match, so a deferred-because-unmeasured order is left to retry as before.
            var reached = CalculateScrollOffsetForIndex(OrderedScrollToIndex.Index,
                OrderedScrollToIndex.RelativePosition);
            if (PointIsValid(reached)
                && AreEqual((float)InternalViewportOffset.Units.X, reached.X, 0.5)
                && AreEqual((float)InternalViewportOffset.Units.Y, reached.Y, 0.5))
            {
                OrderedScrollToIndex = ScrollToIndexOrder.Default;
                return true;
            }

            if (Content is SkiaLayout layout)
            {
                if (layout.HasPendingStructureChanges)
                    return false;

                // Target still in BACKGROUND measurement (e.g. right after a window rebase the oldest item
                // isn't really measured yet): its Destination.Top AND the content bounds are estimates, so the
                // offset is wrong and gets clamped to a stale-short max. Wait for real measurement, then retry.
                if (layout.IsBackgroundMeasuring && layout.BackgroundMeasurementProgress < OrderedScrollToIndex.Index)
                    return false;
            }

            //saving to use upon creating control if this was called before its internal structure was really created
            var offset = CalculateScrollOffsetForIndex(OrderedScrollToIndex.Index,
                OrderedScrollToIndex.RelativePosition);

            if (PointIsValid(offset))
            {
                var time = 0f;
                if (OrderedScrollToIndex.Animated)
                    time = SystemAnimationTimeSecs;

                //Debug.WriteLine($"[STI] idx={OrderedScrollToIndex.Index} contentH={ptsContentHeight:0} " +
                //                $"bgMeasuring={(Content as SkiaLayout)?.IsBackgroundMeasuring} " +
                //                $"bgProgress={(Content as SkiaLayout)?.BackgroundMeasurementProgress} " +
                //                $"totalMeasured={(Content as SkiaLayout)?.TotalMeasuredItems} " +
                //                $"count={(Content as SkiaLayout)?.ItemsSource?.Count}");

                ScrollTo(offset.X, offset.Y, time, OrderedScrollToIndex.Clamp);
                OrderedScrollToIndex = ScrollToIndexOrder.Default;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Instant scroll to top
    /// </summary>
    public virtual void ResetPosition()
    {
        SetContentOffset(Vector2.Zero, false, false);
    }

    /// <summary>
    /// Easy-to-use helper around using a lower level ScrollTo function
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="animated"></param>
    public void SetContentOffset(Vector2 offset, bool animated, bool clamp)
    {
        var speed = animated ? AutoScrollingSpeedMs : 0;

        ScrollTo(offset.X, offset.Y, speed, clamp);
    }

    public virtual void ScrollTo(float x, float y, float maxSpeedSecs, bool clamp)
    {
        StopScrolling();

        var clamped = ClampOffsetHard(x, y);

        OrderedScrollTo = ScrollToPointOrder.ToCoords(clamped.X, clamped.Y, maxSpeedSecs);

        if (!ExecuteScrollToOrder())
        {
            this.UpdateVisibleIndex();
        }
    }

    public void ScrollToTop(float maxTimeSecs)
    {
        if (Orientation == ScrollOrientation.Vertical)
        {
            ScrollTo(InternalViewportOffset.Units.X, 0, maxTimeSecs, false);
        }
        else if (Orientation == ScrollOrientation.Horizontal)
        {
            ScrollTo(0, InternalViewportOffset.Units.Y, maxTimeSecs, false);
        }
        else
        {
            ScrollTo(0, 0, maxTimeSecs, false);
        }
    }

    public void ScrollToBottom(float maxTimeSecs)
    {
        // For virtualized lists with unmeasured items, use estimated bottom position
        if (UseVirtual && Content is SkiaLayout layout && layout.IsTemplated &&
            layout.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
        {
            ScrollToEstimatedBottom(maxTimeSecs);
            return;
        }

        // Standard scroll to bottom using measured content
        if (Orientation == ScrollOrientation.Vertical)
        {
            ScrollTo(InternalViewportOffset.Units.X, -ContentSize.Pixels.Height, maxTimeSecs, true);
        }
        else if (Orientation == ScrollOrientation.Horizontal)
        {
            ScrollTo(_scrollMinX, InternalViewportOffset.Units.Y, maxTimeSecs, true);
        }
        else
        {
            ScrollTo(_scrollMinX, _scrollMinY, maxTimeSecs, true);
        }
    }

    /// <summary>
    /// Scrolls to estimated bottom position for virtualized lists with unmeasured items
    /// </summary>
    private void ScrollToEstimatedBottom(float maxTimeSecs)
    {
        if (!(Content is SkiaLayout layout) || !layout.IsTemplated)
            return;

        var estimatedSize = layout.GetEstimatedContentSize(RenderingScale);

        Debug.WriteLine(
            $"[ScrollToEstimatedBottom] Current content size: {ContentSize.Pixels.Width}x{ContentSize.Pixels.Height}, estimated: {estimatedSize.Pixels.Width}x{estimatedSize.Pixels.Height}");

        if (Orientation == ScrollOrientation.Vertical)
        {
            // Calculate estimated bottom position
            var estimatedContentHeight = estimatedSize.Pixels.Height;
            var viewportHeight = Viewport.Pixels.Height;
            var estimatedScrollY = -(estimatedContentHeight - viewportHeight);

            // Clamp to reasonable bounds
            var minScrollY = Math.Min(0, estimatedScrollY);

            Debug.WriteLine(
                $"[ScrollToEstimatedBottom] Scrolling to estimated Y: {minScrollY} (content: {estimatedContentHeight}, viewport: {viewportHeight})");

            ScrollTo(InternalViewportOffset.Units.X, minScrollY, maxTimeSecs, true);
        }
        else if (Orientation == ScrollOrientation.Horizontal)
        {
            // Calculate estimated right position
            var estimatedContentWidth = estimatedSize.Pixels.Width;
            var viewportWidth = Viewport.Pixels.Width;
            var estimatedScrollX = -(estimatedContentWidth - viewportWidth);

            // Clamp to reasonable bounds
            var minScrollX = Math.Min(0, estimatedScrollX);

            Debug.WriteLine(
                $"[ScrollToEstimatedBottom] Scrolling to estimated X: {minScrollX} (content: {estimatedContentWidth}, viewport: {viewportWidth})");

            ScrollTo(minScrollX, InternalViewportOffset.Units.Y, maxTimeSecs, true);
        }
    }

    private bool _Snapped;

    public bool Snapped
    {
        get { return _Snapped; }
        set
        {
            if (_Snapped != value)
            {
                _Snapped = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _IsSnapping;

    public bool IsSnapping
    {
        get { return _IsSnapping; }
        set
        {
            if (_IsSnapping != value)
            {
                _IsSnapping = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsAnimating { get; set; }
    public bool IsBouncing { get; set; }

    Vector2 _axis;
    double? _changeSpeed = null;
}
