using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using AppoMobi.Gestures;
using DrawnUi.Views;

namespace DrawnUi.Draw;

/// <summary>
/// Input handling for DrawnUI.Web - routes JS input to DrawnUI gesture system
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class WebInput
{
    private static TouchActionEventArgs? _pointerDownArgs;
    private static TouchActionEventArgs? _previousArgs;
    private static readonly HashSet<long> ActiveTouchIds = new();

    /// <summary>
    /// The canvas that will receive input events
    /// </summary>
    public static Canvas? TargetCanvas { get; set; }

    /// <summary>
    /// Current rendering scale
    /// </summary>
    public static float RenderingScale { get; set; } = 1.0f;

    /// <summary>
    /// Called by JS when pointer goes down
    /// </summary>
    [JSExport]
    public static void OnPointerDown(int pointerId, double x, double y, int button, int buttons)
    {
        var location = new PointF((float)x, (float)y);
        var args = MakeTouchArgs(pointerId, TouchActionType.Pressed, location);

        ActiveTouchIds.Add(pointerId);
        args.NumberOfTouches = ActiveTouchIds.Count;
        args.StartingLocation = location;
        args.IsInsideView = true;
        args.IsInContact = true;
        args.Distance = new TouchActionEventArgs.DistanceInfo();

        _pointerDownArgs = args;
        _previousArgs = args;

        TargetCanvas?.OnGestureEvent(TouchActionType.Pressed, args, TouchActionResult.Down);
    }

    /// <summary>
    /// Called by JS when pointer moves
    /// </summary>
    [JSExport]
    public static void OnPointerMove(int pointerId, double x, double y, int buttons)
    {
        var location = new PointF((float)x, (float)y);
        var isDragging = buttons != 0;
        var actionType = isDragging ? TouchActionType.Moved : TouchActionType.Pointer;
        var args = MakeTouchArgs(pointerId, actionType, location);

        args.NumberOfTouches = ActiveTouchIds.Count;

        if (_previousArgs != null)
        {
            TouchActionEventArgs.FillDistanceInfo(args, _previousArgs);
        }

        if (_pointerDownArgs != null)
        {
            args.StartingLocation = _pointerDownArgs.StartingLocation;
        }
        else
        {
            args.StartingLocation = location;
        }

        if (actionType == TouchActionType.Pointer)
        {
            TargetCanvas?.OnGestureEvent(actionType, args, TouchActionResult.Pointer);
            _previousArgs = args;
            return;
        }

        if (args.Distance.Delta.X != 0 || args.Distance.Delta.Y != 0)
        {
            TargetCanvas?.OnGestureEvent(actionType, args, TouchActionResult.Panning);
        }

        _previousArgs = args;
    }

    /// <summary>
    /// Called by JS when pointer goes up
    /// </summary>
    [JSExport]
    public static void OnPointerUp(int pointerId, double x, double y, int button, int buttons)
    {
        var location = new PointF((float)x, (float)y);
        var args = MakeTouchArgs(pointerId, TouchActionType.Released, location);

        ActiveTouchIds.Remove(pointerId);
        args.NumberOfTouches = ActiveTouchIds.Count;
        args.IsInContact = ActiveTouchIds.Count > 0;

        if (_previousArgs != null)
        {
            TouchActionEventArgs.FillDistanceInfo(args, _previousArgs);
        }

        if (_pointerDownArgs != null)
        {
            args.StartingLocation = _pointerDownArgs.StartingLocation;

            // Check for tap
            var threshold = TouchEffect.TappedCancelMoveThresholdPoints * Math.Max(0.1f, TouchEffect.Density);
            if (Math.Abs(args.Distance.Total.X) < threshold && Math.Abs(args.Distance.Total.Y) < threshold)
            {
                TargetCanvas?.OnGestureEvent(TouchActionType.Released, args, TouchActionResult.Tapped);
            }
        }

        TargetCanvas?.OnGestureEvent(TouchActionType.Released, args, TouchActionResult.Up);

        _pointerDownArgs = null;
        _previousArgs = null;
    }

    /// <summary>
    /// Called by JS when pointer is cancelled
    /// </summary>
    [JSExport]
    public static void OnPointerCancel(int pointerId)
    {
        var location = new PointF(0, 0);
        var args = MakeTouchArgs(pointerId, TouchActionType.Cancelled, location);

        ActiveTouchIds.Remove(pointerId);
        args.NumberOfTouches = ActiveTouchIds.Count;
        args.IsInContact = false;

        TargetCanvas?.OnGestureEvent(TouchActionType.Cancelled, args, TouchActionResult.Up);

        _pointerDownArgs = null;
        _previousArgs = null;
    }

    /// <summary>
    /// Called by JS when wheel/scrolling occurs
    /// </summary>
    [JSExport]
    public static void OnWheel(double deltaX, double deltaY, int deltaMode)
    {
        // TODO: Implement wheel handling
        Console.WriteLine($"Wheel: dx={deltaX}, dy={deltaY}, mode={deltaMode}");
    }

    private static TouchActionEventArgs MakeTouchArgs(long pointerId, TouchActionType type, PointF location)
    {
        var args = new TouchActionEventArgs(
            pointerId,
            type,
            location,
            null,
            Math.Max(0.1f, RenderingScale));

        args.IsInsideView = true;
        args.NumberOfTouches = ActiveTouchIds.Count;

        if (_pointerDownArgs != null)
        {
            args.StartingLocation = _pointerDownArgs.StartingLocation;
        }
        else
        {
            args.StartingLocation = location;
        }

        return args;
    }
}
