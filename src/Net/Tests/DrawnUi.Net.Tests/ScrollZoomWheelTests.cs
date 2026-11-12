using AppoMobi.Gestures;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>Tests that hold a key in the process-wide KeyboardManager.</summary>
[CollectionDefinition("GlobalKeyboard", DisableParallelization = true)]
public class GlobalKeyboardCollection
{
}

/// <summary>
/// Ctrl/Cmd + wheel is a zoom gesture: a SkiaScroll offers it to the control under the pointer first. A child that
/// takes it (a zoomable view) gets it and the scroll stays still; when no child takes it the scroll scrolls as for any
/// wheel. A plain wheel is unchanged: the scroll takes it first.
/// </summary>
[Collection("GlobalKeyboard")]
public class ScrollZoomWheelTests
{
    /// <summary>A view that zooms on a Ctrl/Cmd wheel and takes nothing else.</summary>
    sealed class ZoomView : SkiaControl
    {
        public int Zooms;

        public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
        {
            if (args.Type == TouchActionResult.Wheel && (KeyboardManager.IsControlPressed || KeyboardManager.IsMetaPressed))
            {
                Zooms++;
                return this;
            }
            return base.ProcessGestures(args, apply);
        }
    }

    static (HeadlessCanvasHost Host, SkiaScroll Scroll, ZoomView View) Page()
    {
        var host = new HeadlessCanvasHost(400, 600, 1f, Colors.Black);
        SkiaScroll scroll;
        ZoomView view;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Spacing = 0,
                Children =
                {
                    new ZoomView { HeightRequest = 200, HorizontalOptions = LayoutOptions.Fill }.Assign(out view),
                    new SkiaControl { HeightRequest = 2000, HorizontalOptions = LayoutOptions.Fill },
                }
            }
        }.Assign(out scroll);
        for (var i = 0; i < 3; i++)
            host.RenderFrame(16.7);
        return (host, scroll, view);
    }

    static void Wheel(HeadlessCanvasHost host, GestureRobot robot, SkiaScroll scroll, float y, bool ctrl)
    {
        if (ctrl)
            KeyboardManager.KeyboardPressed(InputKey.ControlLeft);
        try
        {
            for (var i = 0; i < 3; i++)
                robot.WheelScroll(200, y, -120);
        }
        finally
        {
            if (ctrl)
                KeyboardManager.KeyboardReleased(InputKey.ControlLeft);
        }
        robot.SettleFling(scroll);
    }

    [Fact]
    public void Ctrl_wheel_over_a_zoomable_child_zooms_it_and_the_scroll_stays()
    {
        var (host, scroll, view) = Page();
        using var _ = host;
        Wheel(host, new GestureRobot(host), scroll, 100, ctrl: true);
        Assert.Equal(3, view.Zooms);
        Assert.Equal(0, scroll.ViewportOffsetY, 1);
    }

    [Fact]
    public void Ctrl_wheel_over_a_plain_child_scrolls()
    {
        var (host, scroll, view) = Page();
        using var _ = host;
        Wheel(host, new GestureRobot(host), scroll, 400, ctrl: true);
        Assert.Equal(0, view.Zooms);
        Assert.True(scroll.ViewportOffsetY < -100, $"scrolled: {scroll.ViewportOffsetY}");
    }

    [Fact]
    public void A_plain_wheel_over_a_zoomable_child_scrolls_as_before()
    {
        var (host, scroll, view) = Page();
        using var _ = host;
        Wheel(host, new GestureRobot(host), scroll, 100, ctrl: false);
        Assert.Equal(0, view.Zooms);
        Assert.True(scroll.ViewportOffsetY < -100, $"scrolled: {scroll.ViewportOffsetY}");
    }
}
