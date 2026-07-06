namespace DrawnUi.Draw;

/// <summary>
/// The scroll + virtualizing-stack surface a <see cref="WindowedSource{T}"/> drives. One thin
/// adapter wires it to the concrete controls (<see cref="SkiaScroll"/> + <see cref="SkiaLayout"/>);
/// the controller itself stays free of any control type so it is fully reusable/testable.
/// The built-in adapter is <see cref="SkiaScrollWindowHost"/>.
/// </summary>
public interface IWindowHost
{
    /// <summary>Resident (windowed) item count = the bound ItemsSource length.</summary>
    int VisibleCount { get; }

    /// <summary>Highest LOCAL (resident) index currently visible. Used to detect arrival at the oldest end
    /// (LastVisibleIndex == VisibleCount-1) so a jump can release its LoadMore suppression only once landed.</summary>
    int LastVisibleIndex { get; }

    /// <summary>An ordered ScrollToIndex is still settling (target not yet measured/landed).</summary>
    bool OrderedScrollInProgress { get; }

    /// <summary>Blocks auto-LoadMore while a programmatic jump is in flight.</summary>
    bool SuppressLoadMore { get; set; }

    /// <summary>Trim-after-measure hook: invoked once the stack has applied a structure change.</summary>
    Action OnMeasured { get; set; }

    /// <summary>Ordered scroll to a LOCAL (resident) index with the given alignment.</summary>
    void ScrollToLocal(int local, RelativePositionType align, bool animate);

    /// <summary>Instant snap to content start (offset 0) — newest in an inverted list.</summary>
    void SnapToStart();

    /// <summary>Stop any in-flight fling/bounce/scroll animation. Called before a programmatic jump/rebase so
    /// an active fling can't keep scrolling (and triggering LoadMore) into the window swap.</summary>
    void StopAnimations();
}
