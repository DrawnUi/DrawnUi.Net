namespace DrawnUi.Draw;

/// <summary>
/// Built-in <see cref="IWindowHost"/> over a <see cref="SkiaScroll"/> + templated <see cref="SkiaLayout"/>.
/// Wires a <see cref="WindowedSource{T}"/> to the controls with no subclassing: uses the layout's own
/// <see cref="SkiaLayout.SuppressLoadMore"/> and <see cref="SkiaLayout.MeasurementApplied"/> primitives.
/// The layout's <c>ItemsSource</c> must be the window's <c>Items</c> collection.
/// </summary>
public sealed class SkiaScrollWindowHost : IWindowHost
{
    private readonly SkiaScroll _scroll;
    private readonly SkiaLayout _layout;
    private Action _onMeasured;

    public SkiaScrollWindowHost(SkiaScroll scroll, SkiaLayout layout)
    {
        _scroll = scroll;
        _layout = layout;
        // Single-slot callback bridged to the multicast event: WindowedSource assigns/clears OnMeasured as a
        // one-shot trim hook, so we keep one subscription and invoke whatever delegate is currently set.
        _layout.MeasurementApplied += () => _onMeasured?.Invoke();
    }

    public int VisibleCount => _layout.ItemsSource?.Count ?? 0;
    public int LastVisibleIndex => _layout.LastVisibleIndex;
    public bool OrderedScrollInProgress => _scroll.OrderedScrollToIndexIsSet;
    public bool SuppressLoadMore { get => _layout.SuppressLoadMore; set => _layout.SuppressLoadMore = value; }
    public Action OnMeasured { get => _onMeasured; set => _onMeasured = value; }

    public void ScrollToLocal(int local, RelativePositionType align, bool animate)
        => _scroll.ScrollToIndex(local, animate, align, true);

    public void SnapToStart() => _scroll.ScrollTo(0, 0, 0, false);

    public void StopAnimations() => _scroll.StopScrolling();
}
