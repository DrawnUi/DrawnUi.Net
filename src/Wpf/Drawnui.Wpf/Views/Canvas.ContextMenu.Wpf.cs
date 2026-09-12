using AppoMobi.Gestures;
using System.Drawing;

namespace DrawnUi.Views;

/// <summary>
/// Context-menu entry point for the desktop canvas, the twin of <c>WebInput.OnContextMenu</c> on the
/// browser heads: a <see cref="TouchActionResult.ContextMenu"/> gesture routed like a tap — deepest
/// child first — to whichever control set <c>SkiaControl.ContextMenu</c>.
/// <para>
/// Lives here rather than beside the other <c>HandleDesktop*</c> methods in
/// <c>src/Net/DrawnUi/Views/Canvas.OpenTk.cs</c> only because that file is outside the WPF head.
/// </para>
/// </summary>
public partial class Canvas
{
    /// <summary>
    /// Feeds a context-menu request to the drawn tree.
    /// </summary>
    /// <param name="x">Pointer X in canvas pixels.</param>
    /// <param name="y">Pointer Y in canvas pixels.</param>
    /// <param name="clientW">Canvas width in pixels.</param>
    /// <param name="clientH">Canvas height in pixels.</param>
    /// <param name="device">Mouse (right click) or Touch / Pen (long press); null for the keyboard Menu key, which carries no pointer.</param>
    /// <returns>True when a control took the request, so the host suppresses its own menu.</returns>
    public bool HandleDesktopContextMenu(float x, float y, float clientW, float clientH, PointerDeviceType? device)
    {
        var location = new PointF(x, y);
        var args = MakeDesktopTouchArgs(TouchActionType.ContextMenu, location, clientW, clientH);
        args.IsInsideView = true;
        args.StartingLocation = location;
        args.Distance = new TouchActionEventArgs.DistanceInfo();
        // No pointer = keyboard: that is how SkiaControl derives ContextMenuSource.
        if (device.HasValue)
        {
            args.Pointer = new PointerData
            {
                Button = AppoMobi.Gestures.MouseButton.Right,
                ButtonNumber = 2,
                State = AppoMobi.Gestures.MouseButtonState.Released,
                DeviceType = device.Value,
            };
        }

        OnGestureEvent(TouchActionType.ContextMenu, args, TouchActionResult.ContextMenu);

        return args.Handled;
    }
}
