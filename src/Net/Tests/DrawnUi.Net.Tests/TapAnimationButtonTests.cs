using AppoMobi.Gestures;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
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

    [Theory]
    [InlineData(MouseButton.Right, false)]
    [InlineData(MouseButton.Middle, false)]
    [InlineData(MouseButton.Left, true)]
    public void Desktop_click_plays_the_ripple_for_the_left_button_only(MouseButton button, bool ripple)
    {
        using var host = new HeadlessCanvasHost(300, 300, 1f, Colors.Black);
        var taps = 0;
        host.Canvas.Content = new SkiaShape
        {
            WidthRequest = 200, HeightRequest = 200, BackgroundColor = Colors.Black,
            AnimationTapped = SkiaTouchAnimation.Ripple, TouchEffectColor = Colors.White,
        }.OnTapped(me => taps++);
        host.AdvanceFrames(3);
        var rest = host.NonBackgroundFraction(Colors.Black);

        var pointer = new PointerData { DeviceType = PointerDeviceType.Mouse, Button = button };
        host.Canvas.HandleDesktopPointerDown(100, 100, 300, 300, pointer);
        host.Canvas.HandleDesktopPointerUp(100, 100, 300, 300, pointer);
        host.AdvanceFrames(4);

        var drawn = host.NonBackgroundFraction(Colors.Black) > rest + 0.001;
        Assert.Equal(ripple, drawn);
    }
}
