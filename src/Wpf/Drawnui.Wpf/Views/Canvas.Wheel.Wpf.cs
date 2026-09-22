using AppoMobi.Gestures;
using System.Drawing;

namespace DrawnUi.Views;

/// <summary>
/// Mouse wheel entry point for the desktop canvas, mirroring the browser heads
/// (<c>WebInput.OnWheel</c>): a <see cref="TouchActionType.Wheel"/> gesture carrying
/// <see cref="WheelEventArgs"/>, which is what <c>SkiaScroll</c> listens for.
/// <para>
/// It lives here rather than beside the other <c>HandleDesktop*</c> methods in
/// <c>src/Net/DrawnUi/Views/Canvas.OpenTk.cs</c> only because that file is outside the WPF head.
/// Moving it there would give the OpenTK head wheel support too, which it currently also lacks.
/// </para>
/// </summary>
public partial class Canvas
{
    /// <summary>
    /// Feeds a wheel notch to the drawn tree.
    /// </summary>
    /// <param name="x">Pointer X in canvas pixels.</param>
    /// <param name="y">Pointer Y in canvas pixels.</param>
    /// <param name="delta">WPF wheel delta: +120 per notch away from the user, -120 toward.</param>
    /// <param name="clientW">Canvas width in pixels.</param>
    /// <param name="clientH">Canvas height in pixels.</param>
    public void HandleDesktopWheel(float x, float y, int delta, float clientW, float clientH)
    {
        if (delta == 0)
            return;

        var location = new PointF(x, y);
        var args = MakeDesktopTouchArgs(TouchActionType.Wheel, location, clientW, clientH);
        args.NumberOfTouches = 1;

        // No sign flip here, unlike the browser: WPF already reports a negative delta for scrolling
        // toward the user, which is the direction DrawnUI expects for scrolling content down.
        args.Wheel = new WheelEventArgs
        {
            Delta = delta,
            Scale = 1f,
            Center = location,
        };

        OnGestureEvent(TouchActionType.Wheel, args, TouchActionResult.Wheel);
    }
}
