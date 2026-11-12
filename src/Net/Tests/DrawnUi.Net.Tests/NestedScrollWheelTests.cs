using AppoMobi.Gestures;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Wheel chaining, as browsers do and as drags already chain: a plain wheel over a scroll inside a page goes to that
/// scroll while it can move that way; at its end the page moves. Decided once per wheel gesture and held for it. A
/// zoomable view inside the page never takes a plain wheel (it zooms on Ctrl/Cmd + wheel or a pinch).
/// </summary>
[Collection("GlobalKeyboard")]
public class NestedScrollWheelTests
{
    /// <summary>Wheel events further apart than the gesture gap: a new wheel gesture.</summary>
    static void NewWheelGesture() => Thread.Sleep((int)SkiaScroll.WheelGestureGapMs + 80);

    static (HeadlessCanvasHost Host, SkiaScroll Page, SkiaScroll List, ZoomContent Zoom) Scene()
    {
        var host = new HeadlessCanvasHost(400, 600, 1f, Colors.Black);
        SkiaScroll page, list;
        ZoomContent zoom;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Spacing = 0,
                Children =
                {
                    new SkiaControl { HeightRequest = 100, HorizontalOptions = LayoutOptions.Fill },
                    // a list 200 high over 1000 of rows: it scrolls 800
                    new SkiaScroll
                    {
                        HeightRequest = 200,
                        HorizontalOptions = LayoutOptions.Fill,
                        Content = new SkiaControl { HeightRequest = 1000, HorizontalOptions = LayoutOptions.Fill },
                    }.Assign(out list),
                    new ZoomContent
                    {
                        HeightRequest = 200,
                        HorizontalOptions = LayoutOptions.Fill,
                        Content = new SkiaControl { HeightRequest = 200, HorizontalOptions = LayoutOptions.Fill },
                    }.Assign(out zoom),
                    new SkiaControl { HeightRequest = 2000, HorizontalOptions = LayoutOptions.Fill },
                }
            }
        }.Assign(out page);
        for (var i = 0; i < 3; i++)
            host.RenderFrame(16.7);
        return (host, page, list, zoom);
    }

    static void WheelDown(GestureRobot robot, SkiaScroll settle, float y, int notches)
    {
        for (var i = 0; i < notches; i++)
            robot.WheelScroll(200, y, -120);
        robot.SettleFling(settle);
    }

    [Fact]
    public void Wheel_over_a_list_midway_moves_the_list_and_the_page_stays()
    {
        var (host, page, list, _) = Scene();
        using var _h = host;
        NewWheelGesture();
        WheelDown(new GestureRobot(host), list, 200, 2);
        Assert.True(list.ViewportOffsetY < -100, $"list: {list.ViewportOffsetY}");
        Assert.Equal(0, page.ViewportOffsetY, 1);
    }

    [Fact]
    public void Wheel_over_a_list_at_its_end_moves_the_page()
    {
        var (host, page, list, _) = Scene();
        using var _h = host;
        list.ScrollTo(0, -800, 0, true);
        host.AdvanceFrames(5, 16.7);
        NewWheelGesture();
        WheelDown(new GestureRobot(host), page, 200, 2);
        Assert.True(page.ViewportOffsetY < -100, $"page: {page.ViewportOffsetY}");
        Assert.Equal(-800, list.ViewportOffsetY, 1);
    }

    [Fact]
    public void One_wheel_gesture_stays_on_the_list_past_its_end_and_the_next_one_moves_the_page()
    {
        var (host, page, list, _) = Scene();
        using var _h = host;
        list.ScrollTo(0, -700, 0, true);
        host.AdvanceFrames(5, 16.7);
        NewWheelGesture();
        var robot = new GestureRobot(host);
        WheelDown(robot, list, 200, 4); // one spin: 100 to the end, the rest held by the list
        Assert.Equal(-800, list.ViewportOffsetY, 1);
        Assert.Equal(0, page.ViewportOffsetY, 1);

        NewWheelGesture();
        WheelDown(robot, page, 200, 1);
        Assert.True(page.ViewportOffsetY < -50, $"page: {page.ViewportOffsetY}");
    }

    [Fact]
    public void Wheel_up_over_a_list_at_its_start_moves_the_page()
    {
        var (host, page, list, _) = Scene();
        using var _h = host;
        page.ScrollTo(0, -50, 0, true);
        host.AdvanceFrames(5, 16.7);
        NewWheelGesture();
        var robot = new GestureRobot(host);
        robot.WheelScroll(200, 160, 120); // list at its start, wheel up
        robot.SettleFling(page);
        Assert.Equal(0, page.ViewportOffsetY, 1);
        Assert.Equal(0, list.ViewportOffsetY, 1);
    }

    [Fact]
    public void A_plain_wheel_over_a_zoomable_view_scrolls_the_page_and_does_not_zoom()
    {
        var (host, page, _, zoom) = Scene();
        using var _h = host;
        NewWheelGesture();
        WheelDown(new GestureRobot(host), page, 400, 2);
        Assert.True(page.ViewportOffsetY < -100, $"page: {page.ViewportOffsetY}");
        Assert.Equal(1, zoom.ViewportZoom, 3);
    }
}
