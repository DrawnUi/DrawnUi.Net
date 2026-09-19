using AppoMobi.Gestures;
using System.Drawing;

namespace DrawnUi.Views;

/// <summary>
/// Multi-touch entry points for the WPF canvas. The <c>HandleDesktopPointer*</c> methods model one
/// mouse pointer; touch needs an id per finger, a live touch count and the pinch / rotate
/// <see cref="TouchActionEventArgs.ManipulationInfo"/>, so this mirrors the gesture flow of the
/// browser heads (<c>AppoMobi.Blazor.Gestures.TouchEffect.OnTouchAction</c>) with the shared
/// <see cref="MultitouchTracker"/>.
/// </summary>
public partial class Canvas
{
    private readonly MultitouchTracker _touchTracker = new();
    private readonly Dictionary<long, TouchActionEventArgs> _touchLast = new();
    private readonly Dictionary<long, PointF> _touchStart = new();
    private System.Threading.CancellationTokenSource _touchLongPressCts;
    private bool _touchMaybeTapped;
    private bool _touchLongPressing;

    /// <summary>Touches currently down.</summary>
    public int ActiveTouches => _touchStart.Count;

    /// <summary>A finger went down. Coordinates in canvas pixels.</summary>
    public void HandleTouchDown(long id, float x, float y, float clientW, float clientH, PointerData pointer = null)
    {
        var location = new PointF(x, y);
        _touchStart[id] = location;
        _touchLast.Remove(id);

        var args = MakeTouchArgs(id, TouchActionType.Pressed, location, clientW, clientH, pointer);
        args.IsInContact = true;
        args.Distance = new TouchActionEventArgs.DistanceInfo();

        CancelTouchLongPress();
        _touchLongPressing = false;
        _touchMaybeTapped = _touchStart.Count == 1; // a second finger turns the gesture into a manipulation
        if (_touchStart.Count == 1)
        {
            _touchTracker.Restart(id, location); // Restart clears every tracked finger: first Down only
            ScheduleTouchLongPress(args);
        }
        else
        {
            _touchTracker.AddMovement(id, location); // seeds the extra finger; the count change resets the totals
        }

        _touchLast[id] = args;
        OnGestureEvent(TouchActionType.Pressed, args, TouchActionResult.Down);
    }

    /// <summary>A finger moved. Coordinates in canvas pixels.</summary>
    public void HandleTouchMove(long id, float x, float y, float clientW, float clientH, PointerData pointer = null)
    {
        if (!_touchStart.ContainsKey(id))
            return;

        var args = MakeTouchArgs(id, TouchActionType.Moved, new PointF(x, y), clientW, clientH, pointer);
        args.IsInContact = true;
        _touchLast.TryGetValue(id, out var previous);
        TouchActionEventArgs.FillDistanceInfo(args, previous);

        var manipulation = _touchTracker.AddMovement(id, args.Location);
        if (manipulation != null)
            args.Manipulation = manipulation;

        _touchLast[id] = args;

        if (args.Distance.Delta.X == 0 && args.Distance.Delta.Y == 0)
            return;

        var threshold = TouchEffect.TappedCancelMoveThresholdPoints * Math.Max(0.1f, TouchEffect.Density);
        if (Math.Abs(args.Distance.Total.X) >= threshold || Math.Abs(args.Distance.Total.Y) >= threshold)
        {
            _touchMaybeTapped = false;
            CancelTouchLongPress();
        }

        OnGestureEvent(TouchActionType.Moved, args, TouchActionResult.Panning);
    }

    /// <summary>A finger was lifted, or the touch was cancelled (capture lost).</summary>
    public void HandleTouchUp(long id, float x, float y, float clientW, float clientH, bool cancelled = false, PointerData pointer = null)
    {
        if (!_touchStart.ContainsKey(id))
            return;

        var type = cancelled ? TouchActionType.Cancelled : TouchActionType.Released;
        var args = MakeTouchArgs(id, type, new PointF(x, y), clientW, clientH, pointer);
        _touchLast.TryGetValue(id, out var previous);
        TouchActionEventArgs.FillDistanceInfo(args, previous);

        // NumberOfTouches still counts this finger: others remain in contact when it is 2 or more
        args.IsInContact = args.NumberOfTouches >= 2;

        if (!args.IsInContact)
        {
            _touchTracker.Reset();
            CancelTouchLongPress();

            var threshold = TouchEffect.TappedCancelMoveThresholdPoints * Math.Max(0.1f, TouchEffect.Density);
            if (!cancelled && !_touchLongPressing && _touchMaybeTapped
                && Math.Abs(args.Distance.Total.X) < threshold && Math.Abs(args.Distance.Total.Y) < threshold)
            {
                OnGestureEvent(type, args, TouchActionResult.Tapped);
            }
        }
        else
        {
            _touchMaybeTapped = false;
            _touchTracker.RemoveTouch(id);
        }

        OnGestureEvent(type, args, TouchActionResult.Up);

        _touchStart.Remove(id);
        _touchLast.Remove(id);
    }

    private TouchActionEventArgs MakeTouchArgs(long id, TouchActionType type, PointF location, float clientW, float clientH, PointerData pointer)
    {
        var args = new TouchActionEventArgs(id, type, location, null, (float)Math.Max(0.1, RenderingScale))
        {
            Pointer = pointer,
            IsInsideView = location.X >= 0 && location.Y >= 0 && location.X <= clientW && location.Y <= clientH,
            NumberOfTouches = Math.Max(1, _touchStart.Count),
            StartingLocation = _touchStart.TryGetValue(id, out var start) ? start : location,
        };
        return args;
    }

    private void ScheduleTouchLongPress(TouchActionEventArgs downArgs)
    {
        var cts = new System.Threading.CancellationTokenSource();
        _touchLongPressCts = cts;
        _ = Task.Delay(TouchEffect.LongPressTimeMsDefault, cts.Token).ContinueWith(t =>
        {
            if (t.IsCanceled || !_touchStart.ContainsKey(downArgs.Id))
                return;

            _touchLongPressing = true;
            OnGestureEvent(TouchActionType.Pressing, downArgs, TouchActionResult.LongPressing);
        }, TaskContinuationOptions.NotOnCanceled);
    }

    private void CancelTouchLongPress()
    {
        _touchLongPressCts?.Cancel();
        _touchLongPressCts?.Dispose();
        _touchLongPressCts = null;
    }
}
