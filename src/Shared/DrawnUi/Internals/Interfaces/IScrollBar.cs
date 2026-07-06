namespace DrawnUi.Draw;

/// <summary>
/// Implement to be used as a scroll bar indicator overlay for SkiaScroll, set via the ScrollBar property.
/// The scroll owner pushes state here whenever scroll position, content size or scrolling state change.
/// </summary>
public interface IScrollBar : IDrawnBase
{
    /// <summary>
    /// Called by the scroll owner when scroll offset, content size or scrolling state change.
    /// </summary>
    /// <param name="orientation">Axis being reported.</param>
    /// <param name="progress">Scroll position along the axis, 0.0 - 1.0. Can go slightly outside this range while overscrolling.</param>
    /// <param name="thumbSizeRatio">Viewport size divided by total scrollable content size. Value >= 1 means content fits the viewport and there is nothing to scroll.</param>
    /// <param name="overscrollPts">Current overscroll distance in points, 0 when not overscrolled.</param>
    /// <param name="isScrolling">Whether the user is panning or a scroll animation is running.</param>
    void SetScrollProgress(ScrollOrientation orientation, float progress, float thumbSizeRatio,
        float overscrollPts, bool isScrolling);
}
