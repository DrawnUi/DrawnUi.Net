using AppoMobi.Gestures;
using DrawnUi.Draw;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Tap and press animations (Ripple, Shimmer) play for a finger, a pen or the left mouse button only: a right or
/// middle click still raises Tapped, with no tap feedback.
/// </summary>
public class TapAnimationButtonTests
{
    static SkiaGesturesParameters Args(PointerData pointer) => new()
    {
        Type = TouchActionResult.Tapped,
        Event = new TouchActionEventArgs(0, TouchActionType.Released, new System.Drawing.PointF(10, 10), null, 1f) { Pointer = pointer },
    };

    [Fact]
    public void Only_the_primary_pointer_plays_tap_animations()
    {
        Assert.True(SkiaControl.IsPrimaryPointer(Args(null))); // a finger: no pointer data
        Assert.True(SkiaControl.IsPrimaryPointer(Args(new PointerData { DeviceType = PointerDeviceType.Pen, Button = MouseButton.Left })));
        Assert.True(SkiaControl.IsPrimaryPointer(Args(new PointerData { DeviceType = PointerDeviceType.Mouse, Button = MouseButton.Left })));
        Assert.False(SkiaControl.IsPrimaryPointer(Args(new PointerData { DeviceType = PointerDeviceType.Mouse, Button = MouseButton.Right })));
        Assert.False(SkiaControl.IsPrimaryPointer(Args(new PointerData { DeviceType = PointerDeviceType.Mouse, Button = MouseButton.Middle })));
    }
}
