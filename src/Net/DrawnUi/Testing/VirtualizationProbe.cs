using DrawnUi.Draw;

namespace DrawnUi.Testing;

/// <summary>
/// Drives and observes a virtualized <see cref="SkiaScroll"/>+<see cref="SkiaLayout"/> headlessly to
/// verify that item measurement advances in the background as the list is scrolled (and that the
/// scroll can progress through the whole data set). Scene-agnostic: works with any templated list.
/// </summary>
public sealed class VirtualizationProbe
{
    private readonly HeadlessCanvasHost _host;
    private readonly SkiaScroll _scroll;
    private readonly SkiaLayout _list;

    public VirtualizationProbe(HeadlessCanvasHost host, SkiaScroll scroll, SkiaLayout list)
    {
        _host = host;
        _scroll = scroll;
        _list = list;
    }

    /// <summary>Index of the last measured item (measurement frontier).</summary>
    public int Frontier => _list.LastMeasuredIndex;

    /// <summary>Index of the first measured item.</summary>
    public int Start => _list.FirstMeasuredIndex;

    public int ItemsCount => _list.ItemsSource?.Count ?? 0;
    public float OffsetY => _scroll.ViewportOffsetY;
    public float ContentHeight => _scroll.ContentSize.Pixels.Height;

    /// <summary>First item index currently inside the viewport.</summary>
    public int FirstVisible => _scroll.FirstVisibleIndex;

    /// <summary>Last item index currently inside the viewport.</summary>
    public int LastVisible => _scroll.LastVisibleIndex;

    /// <summary>
    /// Renders frames (with short waits) until the measurement frontier stops advancing, giving
    /// background <c>Task.Run</c> plane preparation / incremental measurement time to complete.
    /// Returns the number of frames rendered.
    /// </summary>
    public int SettleBackground(int maxFrames = 120, int stableNeeded = 4, int sleepMs = 8)
    {
        int stable = 0, frames = 0;
        int prev = Frontier;
        while (frames < maxFrames && stable < stableNeeded)
        {
            if (sleepMs > 0) Thread.Sleep(sleepMs);
            _host.RenderFrame(16);
            frames++;
            if (Frontier == prev) stable++;
            else { stable = 0; prev = Frontier; }
        }
        return frames;
    }

    /// <summary>
    /// Repeatedly drags upward (scrolls the content up) and settles the background after each drag,
    /// until the scroll offset stops progressing for <paramref name="stallRounds"/> consecutive rounds
    /// or <paramref name="maxRounds"/> is hit. Returns the measurement frontier reached.
    /// </summary>
    public int DriveDownByGesture(GestureRobot robot, int maxRounds = 60, int stallRounds = 3, float offsetEpsilon = 2f)
    {
        int stalls = 0;
        float prevOffset = OffsetY;

        float cx = _scroll.DrawingRect.Width / (2f * _host.Scale);
        if (cx <= 0) cx = 200;

        for (int round = 0; round < maxRounds; round++)
        {
            // a firm upward flick: short, fast drag near the bottom of the viewport
            robot.Pan(cx, 520, cx, 180, durationMs: 90, steps: 8);

            SettleBackground();

            if (Frontier >= ItemsCount - 1)
                break;

            if (Math.Abs(OffsetY - prevOffset) < offsetEpsilon)
            {
                if (++stalls >= stallRounds)
                    break;
            }
            else
            {
                stalls = 0;
                prevOffset = OffsetY;
            }
        }

        return Frontier;
    }

    /// <summary>
    /// Generic gesture driver: repeatedly drags (up to scroll content down when <paramref name="down"/>,
    /// else toward the top) and settles the background each round, until <paramref name="stop"/> returns
    /// true, the scroll stalls for <paramref name="stallRounds"/> rounds, or <paramref name="maxRounds"/>
    /// is hit. Returns the number of rounds performed.
    /// </summary>
    public int Drive(GestureRobot robot, Func<bool> stop, bool down = true, int maxRounds = 200,
        int stallRounds = 4, float offsetEpsilon = 2f)
    {
        int stalls = 0;
        float prevOffset = OffsetY;

        float cx = _scroll.DrawingRect.Width / (2f * _host.Scale);
        if (cx <= 0) cx = 200;

        // dragging finger up scrolls content down (deeper); dragging down returns toward the top
        float fromY = down ? 520 : 180;
        float toY = down ? 180 : 520;

        int round = 0;
        for (; round < maxRounds; round++)
        {
            if (stop())
                break;

            robot.Pan(cx, fromY, cx, toY, durationMs: 90, steps: 8);
            SettleBackground();

            if (Math.Abs(OffsetY - prevOffset) < offsetEpsilon)
            {
                if (++stalls >= stallRounds)
                    break;
            }
            else
            {
                stalls = 0;
                prevOffset = OffsetY;
            }
        }

        return round;
    }

    /// <summary>Scrolls deep enough that a cell at or beyond <paramref name="targetBoundIndex"/> is rendered.</summary>
    public int DriveUntilBound(GestureRobot robot, int targetBoundIndex, Func<int> maxBoundIndex, int maxRounds = 200)
        => Drive(robot, () => maxBoundIndex() >= targetBoundIndex, down: true, maxRounds: maxRounds);

    /// <summary>Drags back toward the top until the scroll offset returns to ~0 or stalls.</summary>
    public int DriveToTop(GestureRobot robot, int maxRounds = 200)
        => Drive(robot, () => OffsetY >= -1f, down: false, maxRounds: maxRounds);
}
