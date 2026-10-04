using System.Diagnostics;
using DrawnUi.Draw;

namespace DrawnUi.OpenTk;

/// <summary>
/// Frames one refresh apart where the GL driver does not wait for vsync (drawnui-cross 5d, the same as DrawnUi.Rust's
/// desktop host). A driver can accept swap interval 1 and still return from every swap at once (WSLg's software
/// OpenGL: hundreds of frames a second, shown at the compositor's own moments, so movement stutters).
/// Judged on the first 60 frames in a row: when their median interval is under half the refresh period, frames are
/// spaced on a refresh grid. The next slot is one period after the last slot, whenever the wake-up came, so late
/// wake-ups do not add up, and animations step with the slot, not with the moment the frame happened to start.
/// </summary>
internal sealed class FramePacing
{
    private const int JudgedFrames = 60;

    private readonly long _period;
    private readonly float[] _intervals = new float[JudgedFrames];
    private int _count;

    // null: not judged yet; true: pacing; false: the driver keeps the pace (vsync works)
    private bool? _on;

    private long _lastStart;
    private bool _hasLastStart;
    private long _slot;
    private bool _hasSlot;

    /// <param name="refreshRate">The monitor's refresh rate, Hz.</param>
    /// <param name="alwaysOn">Pace from the first frame without judging the driver (vsync is off).</param>
    public FramePacing(double refreshRate, bool alwaysOn)
    {
        _period = (long)(Stopwatch.Frequency / Math.Max(20.0, refreshRate));
        if (alwaysOn)
            _on = true;
    }

    /// <summary>
    /// A frame starts at <paramref name="start"/> (Stopwatch ticks). When pacing, returns the frame's slot on the
    /// refresh grid: the one it was held for, else its start (after a pause, or when far behind). While the driver is
    /// not judged yet, counts the interval (frames in a row only) and returns null.
    /// </summary>
    public long? Frame(long start)
    {
        var hadPrevious = _hasLastStart;
        var previous = _lastStart;
        _lastStart = start;
        _hasLastStart = true;

        if (_on == true)
        {
            var slot = start;
            if (_hasSlot)
            {
                var next = _slot + _period;
                if (start >= next && start - next < _period)
                    slot = next;
            }

            _slot = slot;
            _hasSlot = true;
            return slot;
        }

        if (_on.HasValue || !hadPrevious)
            return null;

        var intervalMs = (start - previous) * 1000.0 / Stopwatch.Frequency;
        if (intervalMs > 100)
        {
            _count = 0;
            return null;
        }

        _intervals[_count++] = (float)intervalMs;
        if (_count == JudgedFrames)
        {
            Array.Sort(_intervals);
            var median = _intervals[JudgedFrames / 2];
            var periodMs = _period * 1000.0 / Stopwatch.Frequency;
            var ignored = median < periodMs / 2;
            if (ignored)
                Super.Log($"[DrawnUiWindow] the driver does not wait for vsync (frames {median:0.0} ms apart): frames paced at {1000.0 / periodMs:0} Hz");
            _on = ignored;
        }

        return null;
    }

    /// <summary>When pacing: the time (Stopwatch ticks) a frame asked for at <paramref name="now"/> may start, if later.</summary>
    public long? Hold(long now)
    {
        if (_on != true)
            return null;

        long basis;
        if (_hasSlot)
            basis = _slot;
        else if (_hasLastStart)
            basis = _lastStart;
        else
            return null;

        var at = basis + _period;
        return at > now ? at : null;
    }
}
