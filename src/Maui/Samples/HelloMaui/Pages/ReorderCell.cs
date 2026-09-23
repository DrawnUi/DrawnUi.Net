using AppoMobi.Gestures;
using DrawnUi.Models;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;

namespace HelloMaui.Pages;

/// <summary>One language row of the reorder list.</summary>
public sealed class ReorderItem
{
    public int Id { get; init; }
    public string Title { get; init; }
    public string Tag { get; init; }
    public string Color { get; init; }
}

/// <summary>What the page lends every cell so a drag can move the item, scroll the list under it and carry the ghost.</summary>
public interface IDragHost
{
    SkiaScroll Scroll { get; }
    int IndexOf(ReorderItem item);
    /// <summary>Writes the new order; false when the target index is outside the list.</summary>
    bool Move(int from, int to);
    /// <summary>The item currently being dragged, so every cell knows which row the ghost is standing in for.</summary>
    ReorderItem Dragging { get; }
    /// <summary>Picks the row up: the ghost takes its place under the pointer and the real row goes blank.</summary>
    void Lift(ReorderItem item, int index, float pointerY);
    /// <summary>Carries the ghost with the pointer (canvas pixels).</summary>
    void Carry(float pointerY);
    /// <summary>Released: the ghost glides into the slot at index, then the real row draws itself again.</summary>
    void Drop(int index);
    float Spacing { get; }
}

/// <summary>
/// One row of the reorder list: a grip, a caption and the item tag. Ported from the React demo's
/// ReorderCell.ts.
/// <para>
/// The drag lives on the grip. A vertical pan inside a vertical SkiaScroll belongs to the scroll, so
/// the grip stands the scroll down on Down (RespondsToGestures=false) and gives it back on Up. Travel
/// is counted in CONTENT space: the pointer's own movement plus whatever the list scrolled underneath
/// it. Each step is one Move on the ObservableCollection, which the layout applies to the cells it
/// already has (HandleStructurePreservingMove), so the list reorders live under the pointer.
/// </para>
/// </summary>
public class ReorderCell : SkiaDynamicDrawnCell
{
    /// <summary>Points from either end of the viewport where a resting pointer keeps the list moving.</summary>
    private const float EdgeZone = 44;
    /// <summary>Points per frame it moves there.</summary>
    private const float EdgeStep = 7;

    private readonly IDragHost _host;
    private SkiaShape _frame;
    private SkiaLayer _grip;
    private SkiaLabel _title;
    private SkiaLabel _badge;

    private int _dragIndex = -1;
    private int _startIndex = -1;
    private float _travel;
    private float _stride;
    private float _pointerY;
    private float _lastOffset;
    private SkiaScroll _scroll;
    private SkiaValueAnimator _ticker;

    /// <summary>Builds the row visuals once; SetContent rebinds them.</summary>
    public ReorderCell(IDragHost host)
    {
        _host = host;
        Type = LayoutType.Absolute;
        HeightRequest = 44;
        HorizontalOptions = LayoutOptions.Fill;

        Children = new List<SkiaControl>
        {
            new SkiaShape { Type = ShapeType.Rectangle, CornerRadius = 8, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, BackgroundColor = Color.Parse("#111827"), StrokeWidth = 1 }.Assign(out _frame),
            // absolute: a stack would pack the bars at the top of the row; the whole row height is grabbable
            new SkiaLayer { WidthRequest = 26, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Fill, AccessibilityRole = Aria.RoleButton, Margin = new Thickness(10, 0, 0, 0), Children = { Bars("#94A3B8") } }
                .Assign(out _grip)
                .Adapt(me => me.ConsumeGestures += OnGrip),
            new SkiaLabel { FontSize = 14, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(46, 0, 60, 0) }.Assign(out _title),
            new SkiaLabel { FontSize = 12, TextColor = Color.Parse("#94A3B8"), HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(0, 0, 14, 0) }.Assign(out _badge),
        };
    }

    /// <summary>The three grip bars, shared by a row and by the ghost that stands in for it.</summary>
    public static SkiaStack Bars(string color) => new()
    {
        Spacing = 3,
        VerticalOptions = LayoutOptions.Center,
        HorizontalOptions = LayoutOptions.Fill,
        Children = Enumerable.Range(0, 3).Select(_ => (SkiaControl)new SkiaShape { Type = ShapeType.Rectangle, CornerRadius = 1, HeightRequest = 2, WidthRequest = 16, BackgroundColor = Color.Parse(color), HorizontalOptions = LayoutOptions.Center }).ToList(),
    };

    /// <summary>Re-applies the look when the list itself did not change: a lift blanks one row, the landing brings it back.</summary>
    public void Refresh()
    {
        if (BindingContext != null)
            SetContent(BindingContext);
    }

    /// <inheritdoc/>
    protected override void SetContent(object ctx)
    {
        if (ctx is not ReorderItem item)
            return;

        _grip.AccessibilityLabel = $"Reorder {item.Title}";
        _title.Text = item.Title;
        _badge.Text = item.Tag;
        _frame.StrokeColor = Color.Parse(item.Color);

        // the ghost is standing in for this row: leave the gap it is going to drop into
        var opacity = ReferenceEquals(_host.Dragging, item) ? 0 : 1;
        if (Math.Abs(Opacity - opacity) > 0.001)
        {
            Opacity = opacity;
            Update();
        }
    }

    private void OnGrip(object sender, SkiaGesturesInfo e)
    {
        var type = e.Args.Type;

        if (type == TouchActionResult.Down)
        {
            EndDrag(); // a previous drag that never saw its Up
            var item = BindingContext as ReorderItem;
            _dragIndex = item != null ? _host.IndexOf(item) : -1;
            e.Consumed = _dragIndex >= 0;
            if (!e.Consumed || item == null)
                return;

            _startIndex = _dragIndex;
            _travel = 0;
            _stride = MeasuredSize.Units.Height + _host.Spacing;
            _pointerY = e.Args.Event.Location.Y;
            _scroll = _host.Scroll;
            _lastOffset = _scroll != null ? (float)_scroll.ViewportOffsetY : 0;
            if (_scroll != null)
                _scroll.RespondsToGestures = false; // the pan is ours for the whole drag
            _host.Lift(item, _dragIndex, _pointerY);
            _ticker = new SkiaValueAnimator(this) { mMinValue = 0, mMaxValue = 1, Speed = 1000, Repeat = -1, OnUpdated = _ => Tick() };
            _ticker.Start();
            return;
        }

        if (_dragIndex < 0)
            return;

        if (type == TouchActionResult.Panning)
        {
            _pointerY = e.Args.Event.Location.Y;
            _travel += e.Args.Event.Distance.Delta.Y / RenderingScale;
            e.Consumed = true;
            return;
        }

        if (type is TouchActionResult.Up or TouchActionResult.Tapped)
        {
            // released outside the list is a cancel: the row goes back to where it was picked up from
            var viewport = _scroll?.DrawingRect;
            var p = e.Args.Event.Location;
            var outside = viewport.HasValue
                && (p.Y < viewport.Value.Top || p.Y > viewport.Value.Bottom || p.X < viewport.Value.Left || p.X > viewport.Value.Right);
            if (outside && _dragIndex != _startIndex && _host.Move(_dragIndex, _startIndex))
                _dragIndex = _startIndex;

            _host.Drop(_dragIndex);
            EndDrag();
        }
    }

    /// <summary>One frame of the drag: hold at an edge and the list keeps moving, so the row keeps advancing.</summary>
    private void Tick()
    {
        if (_dragIndex < 0)
            return;

        var scroll = _scroll;
        if (scroll != null)
        {
            var scale = scroll.RenderingScale > 0 ? scroll.RenderingScale : 1;
            var zone = EdgeZone * scale;
            var direction = _pointerY < scroll.DrawingRect.Top + zone ? 1
                : _pointerY > scroll.DrawingRect.Bottom - zone ? -1 : 0;
            if (direction != 0)
                scroll.ScrollTo((float)scroll.ViewportOffsetX, (float)scroll.ViewportOffsetY + direction * EdgeStep, 0, true);

            var offset = (float)scroll.ViewportOffsetY;
            _travel += _lastOffset - offset; // the list moving under the pointer advances the row as well
            _lastOffset = offset;
        }

        _host.Carry(_pointerY);

        while (_stride > 0 && Math.Abs(_travel) >= _stride)
        {
            var step = Math.Sign(_travel);
            if (!_host.Move(_dragIndex, _dragIndex + step))
            {
                _travel = 0; // hit an end
                break;
            }

            _dragIndex += step;
            _travel -= step * _stride;
        }
    }

    /// <summary>Ends a drag: stops the ticker and gives the scroll its gestures back. Settling the ghost is the page's job.</summary>
    private void EndDrag()
    {
        _ticker?.Stop();
        _ticker = null;
        _dragIndex = -1;
        _startIndex = -1;
        if (_scroll != null)
        {
            _scroll.RespondsToGestures = true;
            _scroll = null;
        }
    }

    /// <inheritdoc/>
    public override void OnDisposing()
    {
        EndDrag();
        base.OnDisposing();
    }
}
