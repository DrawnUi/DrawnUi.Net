using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A scroll inside another one on the same axis (a list in a page), decided once, when the drag starts: the list keeps
/// a drag that starts while it can still move that way, to the drag's end; a drag that starts while the list is at its
/// end in the drag's direction moves the page. A scroll with nothing around it keeps its edge bounce. The same rule as
/// DrawnUI for Rust and React.
/// </summary>
public class NestedScrollChainingTests
{
    private readonly ITestOutputHelper _out;
    public NestedScrollChainingTests(ITestOutputHelper o) { _out = o; }

    const float ListTop = 200, ListHeight = 300, ListContent = 1200;

    static (HeadlessCanvasHost host, SkiaScroll page, SkiaScroll list) Scene()
    {
        var host = new HeadlessCanvasHost(400, 600, 1f, Colors.Black);
        SkiaScroll page = null, list = null;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Spacing = 0,
                HorizontalOptions = LayoutOptions.Fill,
                Children =
                {
                    new SkiaShape { HeightRequest = ListTop, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.DarkSlateGray },
                    new SkiaScroll
                    {
                        HeightRequest = ListHeight,
                        HorizontalOptions = LayoutOptions.Fill,
                        Content = new SkiaShape { HeightRequest = ListContent, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.SteelBlue },
                    }.Assign(out list),
                    new SkiaShape { HeightRequest = 800, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.DarkOliveGreen },
                }
            }
        }.Assign(out page);
        host.AdvanceFrames(6);
        return (host, page, list);
    }

    static void Settle(HeadlessCanvasHost host, GestureRobot robot, params SkiaScroll[] scrolls)
    {
        foreach (var s in scrolls)
            robot.SettleFling(s);
        host.AdvanceFrames(4);
    }

    static float ListEnd => -(ListContent - ListHeight);

    // a drag of 150 pt over the list's middle row, up (content goes on) or down (content goes back)
    void Drag(GestureRobot robot, float y, float by) => robot.Pan(200, y, 200, y + by, durationMs: 400, steps: 20);

    [Fact]
    public void A_drag_on_the_list_in_its_middle_moves_only_the_list()
    {
        var (host, page, list) = Scene();
        using var _ = host;
        var robot = new GestureRobot(host);
        list.ScrollTo(0, -400, 0, true);
        host.AdvanceFrames(4);

        Drag(robot, ListTop + 200, -150);
        _out.WriteLine($"page {page.ViewportOffsetY} list {list.ViewportOffsetY}");

        Assert.Equal(0, page.ViewportOffsetY, 0.5f);
        Assert.True(list.ViewportOffsetY < -450, $"the list did not move: {list.ViewportOffsetY}");
    }

    [Fact]
    public void A_drag_on_the_list_at_its_end_moves_the_page()
    {
        var (host, page, list) = Scene();
        using var _ = host;
        var robot = new GestureRobot(host);
        list.ScrollTo(0, ListEnd, 0, true);
        host.AdvanceFrames(4);

        Drag(robot, ListTop + 200, -150);
        _out.WriteLine($"page {page.ViewportOffsetY} list {list.ViewportOffsetY}");

        Assert.True(page.ViewportOffsetY < -100, $"the page did not move: {page.ViewportOffsetY}");
        Assert.Equal(ListEnd, list.ViewportOffsetY, 0.5f);
    }

    [Fact]
    public void A_drag_back_on_the_list_at_its_start_moves_the_page()
    {
        var (host, page, list) = Scene();
        using var _ = host;
        var robot = new GestureRobot(host);
        page.ScrollTo(0, -150, 0, true);
        host.AdvanceFrames(4);

        // the list now sits 150 pt higher on the screen
        Drag(robot, ListTop - 150 + 100, 120);
        _out.WriteLine($"page {page.ViewportOffsetY} list {list.ViewportOffsetY}");

        Assert.True(page.ViewportOffsetY > -60, $"the page did not move back: {page.ViewportOffsetY}");
        Assert.Equal(0, list.ViewportOffsetY, 0.5f);
    }

    [Fact]
    public void A_drag_on_the_list_near_its_end_stays_with_the_list_to_the_drag_end()
    {
        var (host, page, list) = Scene();
        using var _ = host;
        var robot = new GestureRobot(host);
        list.ScrollTo(0, ListEnd + 30, 0, true);
        host.AdvanceFrames(4);

        Drag(robot, ListTop + 200, -150);
        Settle(host, robot, list, page);
        _out.WriteLine($"page {page.ViewportOffsetY} list {list.ViewportOffsetY}");

        Assert.Equal(0, page.ViewportOffsetY, 0.5f);
        Assert.Equal(ListEnd, list.ViewportOffsetY, 1f);
    }

    [Fact]
    public void A_scroll_with_nothing_around_keeps_its_bounce_at_the_edge()
    {
        using var host = new HeadlessCanvasHost(400, 600, 1f, Colors.Black);
        SkiaScroll scroll = null;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaShape { HeightRequest = 1500, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.SteelBlue },
        }.Assign(out scroll);
        host.AdvanceFrames(6);
        var end = -(1500 - 600);
        scroll.ScrollTo(0, end, 0, true);
        host.AdvanceFrames(4);
        var robot = new GestureRobot(host);

        robot.PointerDown(200, 400);
        for (var i = 1; i <= 10; i++)
            robot.PointerMoveTo(200, 400 - i * 15);
        var during = scroll.ViewportOffsetY;
        robot.PointerUp();
        _out.WriteLine($"during {during} end {end}");

        Assert.True(during < end - 5, $"no rubber band past the end: {during} vs {end}");
    }
}
