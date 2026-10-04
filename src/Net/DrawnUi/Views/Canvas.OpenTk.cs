using AppoMobi.Gestures;
using System.Drawing;

namespace DrawnUi.Views;

public partial class Canvas
{
    private TouchActionEventArgs? _desktopPointerDownArgs;
    private TouchActionEventArgs? _desktopPreviousArgs;
    private System.Threading.CancellationTokenSource? _desktopLongPressCts;
    private const long DesktopPointerId = 1;

    // The pressed pointer's recent positions (time ns, pixels), for the move velocity: see DesktopMoveVelocity.
    private readonly List<(long Time, PointF Location)> _desktopTrail = new(16);
    private long _desktopPressTime;
    private const long BurstNanos = 1_000_000;        // moves closer than this are one position
    private const long VelocityWindowNanos = 16_000_000;
    private const long NoVelocityAfterPressNanos = 4_000_000;

    public void ConnectDesktopDrawable(ISkiaDrawable drawable)
    {
        AttachCanvasView(drawable);
        ConnectedHandler();
    }

    /// <summary>
    /// Pointer pressed. <paramref name="pointer"/> carries which mouse button (and device) did it — every
    /// button taps; controls filter on <c>Pointer.Button</c> or set <c>ContextMenu</c>. Null = unknown, as
    /// hosts that only forward the primary button pass.
    /// </summary>
    public void HandleDesktopPointerDown(float x, float y, float clientW, float clientH, PointerData? pointer = null)
    {
        var location = new PointF(x, y);
        var args = MakeDesktopTouchArgs(TouchActionType.Pressed, location, clientW, clientH);
        args.Pointer = pointer;
        args.IsInContact = true;
        args.Distance = new TouchActionEventArgs.DistanceInfo();
        _desktopPointerDownArgs = args;
        _desktopPreviousArgs = args;
        _desktopPressTime = DesktopClockNanos();
        _desktopTrail.Clear();
        _desktopTrail.Add((_desktopPressTime, location));
        ScheduleDesktopLongPress(args);
        OnGestureEvent(TouchActionType.Pressed, args, TouchActionResult.Down);
    }

    public void HandleDesktopPointerMove(float x, float y, bool isDragging, float clientW, float clientH, PointerData? pointer = null)
    {
        var location = new PointF(x, y);
        var actionType = isDragging ? TouchActionType.Moved : TouchActionType.Pointer;
        var args = MakeDesktopTouchArgs(actionType, location, clientW, clientH);
        args.Pointer = pointer;

        if (_desktopPreviousArgs != null)
        {
            TouchActionEventArgs.FillDistanceInfo(args, _desktopPreviousArgs);
            if (actionType == TouchActionType.Moved && _desktopPointerDownArgs != null)
            {
                args.Distance.Velocity = DesktopMoveVelocity(DesktopClockNanos(), location);
                args.Distance.TotalVelocity = _desktopPreviousArgs.Distance.TotalVelocity.Add(args.Distance.Velocity);
            }
        }

        if (actionType == TouchActionType.Pointer)
        {
            OnGestureEvent(actionType, args, TouchActionResult.Pointer);
            return;
        }

        if (args.Distance.Delta.X != 0 || args.Distance.Delta.Y != 0)
        {
            OnGestureEvent(actionType, args, TouchActionResult.Panning);

            var moveThreshold = TouchEffect.TappedCancelMoveThresholdPoints * Math.Max(0.1f, TouchEffect.Density);
            if (Math.Abs(args.Distance.Total.X) >= moveThreshold || Math.Abs(args.Distance.Total.Y) >= moveThreshold)
                CancelDesktopLongPress();
        }

        _desktopPreviousArgs = args;
    }

    /// <summary>
    /// The mouse left the canvas: hover and pointer-over end. Desktop heads call it from their window's
    /// mouse-leave event, so a hovered control or a scroll showing its bars on hover lets go.
    /// </summary>
    public void HandleDesktopPointerLeave()
    {
        lock (LockIterateListeners)
        {
            HasHover = null;
            ClearPointerOver();
        }
    }

    public void HandleDesktopPointerUp(float x, float y, float clientW, float clientH, PointerData? pointer = null)
    {
        CancelDesktopLongPress();

        if (_desktopPointerDownArgs == null)
            return;

        var location = new PointF(x, y);
        var args = MakeDesktopTouchArgs(TouchActionType.Released, location, clientW, clientH);
        args.Pointer = pointer;
        args.IsInContact = false;

        if (_desktopPreviousArgs != null)
            TouchActionEventArgs.FillDistanceInfo(args, _desktopPreviousArgs);

        var threshold = TouchEffect.TappedCancelMoveThresholdPoints * Math.Max(0.1f, TouchEffect.Density);
        if (Math.Abs(args.Distance.Total.X) < threshold && Math.Abs(args.Distance.Total.Y) < threshold)
            OnGestureEvent(TouchActionType.Released, args, TouchActionResult.Tapped);

        OnGestureEvent(TouchActionType.Released, args, TouchActionResult.Up);
        _desktopPointerDownArgs = null;
        _desktopPreviousArgs = null;
        _desktopTrail.Clear();
    }

    /// <summary>
    /// The pressed pointer's velocity at a move, pixels per second: its displacement over the last 16 ms, the position
    /// 16 ms back interpolated between the recent ones. Dividing each move by the gap to the previous one, as before,
    /// breaks where moves arrive in bursts (WSLg / X11: two moves 0.01 ms apart every ~15 ms): the second move of a
    /// pair measured millions of px/s and every fling left at the velocity limit, about twice as far as on Windows.
    /// Moves under 1 ms apart are one position (the burst keeps its first time, takes the last position). When the
    /// previous move is already 16 ms or more old, the move's own velocity is used, so evenly spaced input measures
    /// as before. No velocity in the first 4 ms after the press. Same rules as DrawnUi.Rust (gestures.rs, 6d525f7).
    /// </summary>
    private PointF DesktopMoveVelocity(long now, PointF location)
    {
        var trail = _desktopTrail;
        if (trail.Count > 0 && now - trail[^1].Time < BurstNanos)
            trail[^1] = (trail[^1].Time, location);
        else
            trail.Add((now, location));

        // positions older than the window are only needed as the one before it
        while (trail.Count > 2 && trail[1].Time <= now - VelocityWindowNanos * 3)
            trail.RemoveAt(0);

        if (trail.Count < 2)
            return PointF.Empty;

        var (time, position) = trail[^1];
        if (time - _desktopPressTime < NoVelocityAfterPressNanos)
            return PointF.Empty;

        var previous = trail[^2];
        var target = time - VelocityWindowNanos;
        PointF from;
        long fromTime;
        if (previous.Time <= target)
        {
            from = previous.Location;
            fromTime = previous.Time;
        }
        else
        {
            // the newest position at or before the target, then interpolate toward the next one
            var i = trail.Count - 2;
            while (i > 0 && trail[i].Time > target)
                i--;
            if (trail[i].Time > target)
            {
                from = trail[i].Location; // the press is newer than the window: measure from the press
                fromTime = trail[i].Time;
            }
            else
            {
                var (t0, p0) = trail[i];
                var (t1, p1) = trail[i + 1];
                var k = t1 > t0 ? (float)(target - t0) / (t1 - t0) : 1f;
                from = new PointF(p0.X + (p1.X - p0.X) * k, p0.Y + (p1.Y - p0.Y) * k);
                fromTime = target;
            }
        }

        var seconds = (time - fromTime) / 1_000_000_000f;
        return seconds > 0
            ? new PointF((position.X - from.X) / seconds, (position.Y - from.Y) / seconds)
            : PointF.Empty;
    }

    private static long DesktopClockNanos() => VelocityAccumulator.ClockOverrideNanos?.Invoke() ?? Super.GetCurrentTimeNanos();

    /// <summary>
    /// Mouse wheel entry point for the desktop heads (OpenTK, WPF), mirroring the browser heads
    /// (<c>WebInput.OnWheel</c>): a <see cref="TouchActionType.Wheel"/> gesture carrying
    /// <see cref="WheelEventArgs"/>, which is what <c>SkiaScroll</c> listens for.
    /// </summary>
    /// <param name="x">Pointer X in canvas pixels.</param>
    /// <param name="y">Pointer Y in canvas pixels.</param>
    /// <param name="delta">Wheel delta in the Windows convention: +120 per notch away from the user, -120 toward.</param>
    /// <param name="clientW">Canvas width in pixels.</param>
    /// <param name="clientH">Canvas height in pixels.</param>
    /// <param name="horizontal">A horizontal wheel event: <paramref name="delta"/> is along X, positive toward the
    /// left (the start), like a vertical wheel turned away from the user.</param>
    public void HandleDesktopWheel(float x, float y, int delta, float clientW, float clientH, bool horizontal = false)
    {
        if (delta == 0)
            return;

        WheelDeltaPerNotch = 120; // this entry point takes Windows units: a touchpad sends fractions of 120

        var location = new PointF(x, y);
        var args = MakeDesktopTouchArgs(TouchActionType.Wheel, location, clientW, clientH);

        // No sign flip here, unlike the browser: a negative delta (toward the user) is the direction
        // DrawnUI expects for scrolling content down.
        args.Wheel = new WheelEventArgs
        {
            Delta = delta,
            Scale = 1f,
            Center = location,
            IsHorizontal = horizontal,
        };

        OnGestureEvent(TouchActionType.Wheel, args, TouchActionResult.Wheel);
    }

    private void ScheduleDesktopLongPress(TouchActionEventArgs downArgs)
    {
        CancelDesktopLongPress();
        var cts = new System.Threading.CancellationTokenSource();
        _desktopLongPressCts = cts;
        _ = System.Threading.Tasks.Task.Delay(TouchEffect.LongPressTimeMsDefault, cts.Token).ContinueWith(t =>
        {
            if (t.IsCanceled || _desktopPointerDownArgs == null)
                return;
            OnGestureEvent(TouchActionType.Pressing, downArgs, TouchActionResult.LongPressing);
        }, System.Threading.Tasks.TaskContinuationOptions.NotOnCanceled);
    }

    private void CancelDesktopLongPress()
    {
        _desktopLongPressCts?.Cancel();
        _desktopLongPressCts?.Dispose();
        _desktopLongPressCts = null;
    }

    public void HandleDesktopTextInput(string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        if (FocusedChild is SkiaEditor editor)
        {
            editor.StubTypeText(value);
            Repaint();
        }
    }

    public void DesktopEditorBackspace() => EditorAction(e => e.StubBackspace());
    public void DesktopEditorDelete() => EditorAction(e => e.StubDelete());
    public void DesktopEditorEnter(bool splitLine = false, bool shift = false) => EditorAction(e => e.StubPressEnter(splitLine, shift));
    public void DesktopEditorMoveCursor(int delta, bool select) => EditorAction(e => e.StubMoveCursor(delta, select));
    public void DesktopEditorMoveToStart(bool select) => EditorAction(e => e.StubMoveCursor(-e.CursorPosition, select));
    public void DesktopEditorMoveToEnd(bool select) => EditorAction(e => e.StubMoveCursor((e.Text?.Length ?? 0) - e.CursorPosition, select));
    public void DesktopEditorSelectAll() => EditorAction(e => e.StubSelectAll());

    private void EditorAction(Action<SkiaEditor> action)
    {
        if (FocusedChild is SkiaEditor editor)
        {
            action(editor);
            Repaint();
        }
    }

    private TouchActionEventArgs MakeDesktopTouchArgs(TouchActionType type, PointF location, float clientW, float clientH)
    {
        var args = new TouchActionEventArgs(
            DesktopPointerId,
            type,
            location,
            null,
            (float)Math.Max(0.1, RenderingScale));

        args.IsInsideView = location.X >= 0 && location.Y >= 0
            && location.X <= clientW && location.Y <= clientH;
        args.NumberOfTouches = 1;
        args.StartingLocation = _desktopPointerDownArgs?.StartingLocation ?? location;
        return args;
    }
}
