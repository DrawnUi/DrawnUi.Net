namespace DrawnUi.Draw;

/// <summary>
/// Edge the scroll bar docks to, on the axis perpendicular to scrolling.
/// </summary>
public enum ScrollBarDock
{
    /// <summary>
    /// Right edge for vertical scrolling, bottom edge for horizontal. Default.
    /// </summary>
    End,

    /// <summary>
    /// Left edge for vertical scrolling, top edge for horizontal.
    /// Use for RTL layouts or 180-rotated (inverted chat) scrolls where Start renders at the visual right.
    /// </summary>
    Start,
}

/// <summary>
/// Default scroll bar indicator for SkiaScroll: thin rounded thumb over an optional track,
/// docked at the right edge for vertical scrolling or at the bottom edge for horizontal (see Dock).
/// Auto-hides after scrolling stops. Set via SkiaScroll.ScrollBar.
/// Subclass or implement IScrollBar directly for a fully custom look.
/// </summary>
public class SkiaScrollBar : SkiaLayout, IScrollBar
{
    /// <summary>
    /// Edge the bar docks to, perpendicular to the scrolling axis. Default is End.
    /// For a 180-rotated scroll (inverted chat) use Start to show the bar at the visual right.
    /// </summary>
    public ScrollBarDock Dock
    {
        get => _dock;
        set
        {
            if (_dock == value)
                return;
            _dock = value;
            ApplyOrientation();
        }
    }

    private ScrollBarDock _dock = ScrollBarDock.End;

    /// <summary>
    /// Color of the moving thumb.
    /// </summary>
    public Color ThumbColor
    {
        get => _thumbColor;
        set
        {
            if (_thumbColor == value)
                return;
            _thumbColor = value;
            if (_thumb != null)
                _thumb.BackgroundColor = value;
        }
    }

    private Color _thumbColor = Color.FromArgb("#66888888");

    /// <summary>
    /// Color of the static track behind the thumb. Transparent by default.
    /// </summary>
    public Color TrackColor
    {
        get => _trackColor;
        set
        {
            if (_trackColor == value)
                return;
            _trackColor = value;
            if (_track != null)
                _track.BackgroundColor = value;
        }
    }

    private Color _trackColor = Colors.Transparent;

    /// <summary>
    /// Thickness of the bar in points.
    /// </summary>
    public double Thickness
    {
        get => _thickness;
        set
        {
            if (_thickness == value)
                return;
            _thickness = value;
            ApplyOrientation();
        }
    }

    private double _thickness = 4;

    /// <summary>
    /// Distance in points from the docked edge.
    /// </summary>
    public double EdgeMargin
    {
        get => _edgeMargin;
        set
        {
            if (_edgeMargin == value)
                return;
            _edgeMargin = value;
            ApplyOrientation();
        }
    }

    private double _edgeMargin = 2;

    /// <summary>
    /// Minimum thumb length in points, so it stays visible/usable for very long content.
    /// </summary>
    public double MinThumbSize { get; set; } = 32;

    /// <summary>
    /// Whether the bar fades out after scrolling stops. Default is true.
    /// </summary>
    public bool AutoHide { get; set; } = true;

    /// <summary>
    /// Delay in seconds after scrolling stops before the bar fades out.
    /// </summary>
    public double HideDelaySecs { get; set; } = 1.0;

    /// <summary>
    /// Duration in seconds of the fade-out once <see cref="HideDelaySecs"/> has passed. Default is 0.25.
    /// </summary>
    public double HideDurationSecs { get; set; } = 0.25;

    /// <summary>
    /// Lets the user drag the thumb and press the track to jump there (desktop scroll bar behavior).
    /// The owning SkiaScroll then takes the whole gesture, from down to up, when it starts on the bar.
    /// Default is false: the bar only indicates and every gesture passes through to the content.
    /// </summary>
    public bool IsDraggable { get; set; }

    /// <summary>
    /// Extra grab area in points on both sides of the bar, across the scrolling axis, so a thin bar
    /// is easy to hit with a mouse. Used when <see cref="IsDraggable"/> is true.
    /// </summary>
    public double GrabPadding { get; set; } = 8;

    protected SkiaShape _thumb;
    protected SkiaShape _track;
    private CancellationTokenSource _ctsHide;
    private float _lastThumbLen = -1;
    private float _thumbOffsetPts;
    private float _dragGrabPx;
    private bool _hasTravel;
    private ScrollOrientation _orientation = ScrollOrientation.Vertical;

    public SkiaScrollBar()
    {
        UseCache = SkiaCacheType.Operations;
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        InputTransparent = true; // display only, gestures pass through
        Opacity = 0; // hidden until first scroll

        Children = new List<SkiaControl>
        {
            new SkiaShape
            {
                Type = ShapeType.Rectangle,
                BackgroundColor = _trackColor,
                UseCache = SkiaCacheType.Operations,
            }.Assign(out _track),
            new SkiaShape
            {
                Type = ShapeType.Rectangle,
                BackgroundColor = _thumbColor,
                UseCache = SkiaCacheType.Operations,
            }.Assign(out _thumb),
        };
    }

    public override void InitializeDefaultContent(bool force = false)
    {
        base.InitializeDefaultContent(force);

        ApplyOrientation();
    }

    /// <summary>
    /// Docks track and thumb to the edge matching the current orientation.
    /// </summary>
    protected virtual void ApplyOrientation()
    {
        if (_thumb == null)
            return;

        var radius = _thickness / 2;
        var dockStart = _dock == ScrollBarDock.Start;

        if (_orientation == ScrollOrientation.Horizontal)
        {
            var dockOptions = dockStart ? LayoutOptions.Start : LayoutOptions.End;
            var margin = dockStart
                ? new Thickness(0, _edgeMargin, 0, 0)
                : new Thickness(0, 0, 0, _edgeMargin);

            _track.HorizontalOptions = LayoutOptions.Fill;
            _track.VerticalOptions = dockOptions;
            _track.HeightRequest = _thickness;
            _track.WidthRequest = -1;
            _track.Margin = margin;
            _track.CornerRadius = radius;

            _thumb.HorizontalOptions = LayoutOptions.Start;
            _thumb.VerticalOptions = dockOptions;
            _thumb.HeightRequest = _thickness;
            _thumb.Margin = margin;
            _thumb.CornerRadius = radius;
        }
        else
        {
            var dockOptions = dockStart ? LayoutOptions.Start : LayoutOptions.End;
            var margin = dockStart
                ? new Thickness(_edgeMargin, 0, 0, 0)
                : new Thickness(0, 0, _edgeMargin, 0);

            _track.HorizontalOptions = dockOptions;
            _track.VerticalOptions = LayoutOptions.Fill;
            _track.WidthRequest = _thickness;
            _track.HeightRequest = -1;
            _track.Margin = margin;
            _track.CornerRadius = radius;

            _thumb.HorizontalOptions = dockOptions;
            _thumb.VerticalOptions = LayoutOptions.Start;
            _thumb.WidthRequest = _thickness;
            _thumb.Margin = margin;
            _thumb.CornerRadius = radius;
        }

        _lastThumbLen = -1;
    }

    public virtual void SetScrollProgress(ScrollOrientation orientation, float progress, float thumbSizeRatio,
        float overscrollPts, bool isScrolling)
    {
        if (orientation != _orientation)
        {
            _orientation = orientation;
            ApplyOrientation();
        }

        if (thumbSizeRatio >= 1f)
        {
            // content fits viewport, nothing to indicate
            _hasTravel = false;
            _ctsHide?.Cancel();
            Opacity = 0;
            return;
        }

        var track = orientation == ScrollOrientation.Horizontal
            ? (float)MeasuredSize.Units.Width
            : (float)MeasuredSize.Units.Height;

        if (track <= 0)
            return;

        var thumbLen = Math.Max((float)MinThumbSize, track * thumbSizeRatio);

        // squash thumb on overscroll bounce instead of moving it off-track
        if (overscrollPts != 0)
        {
            thumbLen = Math.Max((float)MinThumbSize / 2f, thumbLen - Math.Abs(overscrollPts));
        }

        var travel = track - thumbLen;
        var offset = Math.Clamp(progress, 0f, 1f) * travel;
        _thumbOffsetPts = offset;
        _hasTravel = travel > 0;

        if (Math.Abs(thumbLen - _lastThumbLen) > 0.5f)
        {
            _lastThumbLen = thumbLen;
            if (orientation == ScrollOrientation.Horizontal)
                _thumb.WidthRequest = thumbLen;
            else
                _thumb.HeightRequest = thumbLen;
        }

        if (orientation == ScrollOrientation.Horizontal)
        {
            _thumb.TranslationX = offset;
        }
        else
        {
            _thumb.TranslationY = offset;
        }

        _ctsHide?.Cancel();
        Opacity = 1;

        if (AutoHide && !isScrolling)
        {
            var cts = _ctsHide = new CancellationTokenSource();
            Tasks.StartDelayed(TimeSpan.FromSeconds(HideDelaySecs), cts.Token,
                async () => { await this.FadeToAsync(0, (uint)Math.Max(0, HideDurationSecs * 1000)); });
        }

        Update();
    }

    /// <summary>
    /// Called by the owning scroll on touch down, coordinates in canvas pixels. Returns true when the
    /// point is on the bar (track plus <see cref="GrabPadding"/>) and a drag starts: on the thumb it keeps
    /// the grab point, on the track the thumb jumps to center under the pointer.
    /// </summary>
    public virtual bool BeginDrag(float x, float y)
    {
        if (!IsDraggable || !_hasTravel || _track == null)
            return false;

        var scale = (float)RenderingScale;
        var track = _track.DrawingRect;
        var pad = (float)(GrabPadding * scale);
        var vertical = _orientation != ScrollOrientation.Horizontal;
        var hit = vertical
            ? new SKRect(track.Left - pad, track.Top, track.Right + pad, track.Bottom)
            : new SKRect(track.Left, track.Top - pad, track.Right, track.Bottom + pad);
        if (!hit.Contains(x, y))
            return false;

        // an auto-hidden bar that gets grabbed shows again right away: never drag something invisible
        _ctsHide?.Cancel();
        Opacity = 1;

        var pos = vertical ? y - track.Top : x - track.Left;
        var thumbStart = _thumbOffsetPts * scale;
        var thumbLen = _lastThumbLen * scale;
        _dragGrabPx = pos >= thumbStart && pos <= thumbStart + thumbLen
            ? pos - thumbStart
            : thumbLen / 2;
        return true;
    }

    /// <summary>
    /// Scroll progress 0.0 - 1.0 for the pointer position during a drag started by <see cref="BeginDrag"/>,
    /// coordinates in canvas pixels.
    /// </summary>
    public virtual float GetDragProgress(float x, float y)
    {
        var scale = (float)RenderingScale;
        var track = _track.DrawingRect;
        var vertical = _orientation != ScrollOrientation.Horizontal;
        var pos = (vertical ? y - track.Top : x - track.Left) - _dragGrabPx;
        var travel = (vertical ? track.Height : track.Width) - _lastThumbLen * scale;
        return travel > 0 ? Math.Clamp(pos / travel, 0f, 1f) : 0f;
    }

    public override void OnWillDisposeWithChildren()
    {
        base.OnWillDisposeWithChildren();
        _ctsHide?.Cancel();
    }
}
