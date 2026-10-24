using AppoMobi.Gestures;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A gesture belongs to the control that started panning with it until the finger is up. A side drawer (a modal
/// sliding in from the right, IgnoreWrongDirection) holding a vertical scroll (IgnoreWrongDirection): panning the
/// list down and then dragging right without lifting must keep scrolling the list. The drawer used to take the
/// gesture at the first horizontal move the scroll let through and slide toward closing.
/// </summary>
public class GestureOwnershipTests
{
    const int W = 400, H = 700;

    static (HeadlessCanvasHost host, SkiaDrawer drawer, SkiaScroll scroll) Scene(SkiaControl extra = null)
    {
        var host = new HeadlessCanvasHost(W, H, scale: 1f, background: Colors.Black);
        var list = new SkiaStack { Spacing = 4 };
        for (int i = 0; i < 40; i++)
            list.AddSubView(new SkiaShape { BackgroundColor = Colors.DarkSlateBlue, HeightRequest = 80, HorizontalOptions = LayoutOptions.Fill });

        SkiaDrawer drawer = null;
        SkiaScroll scroll = null;
        var page = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            BackgroundColor = Colors.Gray,
            Children =
            {
                new SkiaScroll
                {
                    Orientation = ScrollOrientation.Vertical,
                    IgnoreWrongDirection = true,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Margin = new Thickness(0, extra != null ? 120 : 0, 0, 0),
                    Content = list,
                }.Assign(out scroll),
            }
        };
        if (extra != null)
            page.AddSubView(extra);

        host.Canvas.Content = new SkiaDrawer
        {
            Direction = DrawerDirection.FromRight,
            RespondsToGestures = true,
            IgnoreWrongDirection = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = page,
        }.Assign(out drawer);
        host.AdvanceFrames(4);
        drawer.IsOpen = true;
        host.AdvanceFrames(60);
        return (host, drawer, scroll);
    }

    [Fact]
    public void VerticalThenHorizontal_ScrollKeepsTheGesture_DrawerStays()
    {
        var (host, drawer, scroll) = Scene();
        using (host)
        {
            Assert.True(drawer.IsOpen);
            var openX = drawer.TranslationX;
            var robot = new GestureRobot(host);

            robot.PointerDown(200, 500);
            for (int i = 1; i <= 8; i++)
                robot.PointerMoveTo(200, 500 - i * 20); // up: the list scrolls down
            var scrolled = scroll.ViewportOffsetY;
            Assert.True(scrolled < -50, $"the list did not scroll: {scrolled}");

            float maxShift = 0;
            for (int i = 1; i <= 10; i++)
            {
                robot.PointerMoveTo(200 + i * 25, 340); // then right, without lifting
                maxShift = Math.Max(maxShift, (float)Math.Abs(drawer.TranslationX - openX));
            }
            robot.PointerUp();
            host.AdvanceFrames(60);

            Assert.True(maxShift < 1, $"the drawer moved {maxShift} px during the list's gesture");
            Assert.True(drawer.IsOpen, "the drawer closed during the list's gesture");
        }
    }

    [Fact]
    public void HorizontalStart_GoesToTheDrawer()
    {
        var (host, drawer, scroll) = Scene();
        using (host)
        {
            var openX = drawer.TranslationX;
            var robot = new GestureRobot(host);

            robot.PointerDown(100, 400);
            float maxShift = 0;
            for (int i = 1; i <= 12; i++)
            {
                robot.PointerMoveTo(100 + i * 22, 400);
                maxShift = Math.Max(maxShift, (float)(drawer.TranslationX - openX));
            }
            robot.PointerUp();
            host.AdvanceFrames(90);

            Assert.True(maxShift > 100, $"the drawer did not follow a horizontal swipe: {maxShift}");
            Assert.False(drawer.IsOpen, "a long swipe right did not close the drawer");
            Assert.Equal(0, scroll.ViewportOffsetY, 1);
        }
    }

    [Fact]
    public void HorizontalScrollerInside_TakesAHorizontalStart_DrawerStays()
    {
        var row = new SkiaRow { Spacing = 4 };
        for (int i = 0; i < 20; i++)
            row.AddSubView(new SkiaShape { BackgroundColor = Colors.Teal, WidthRequest = 100, HeightRequest = 100 });
        var strip = new SkiaScroll
        {
            Orientation = ScrollOrientation.Horizontal,
            IgnoreWrongDirection = true,
            HeightRequest = 100,
            HorizontalOptions = LayoutOptions.Fill,
            Content = row,
        };

        var (host, drawer, _) = Scene(strip);
        using (host)
        {
            var openX = drawer.TranslationX;
            var robot = new GestureRobot(host);

            robot.PointerDown(300, 50);
            float maxShift = 0;
            for (int i = 1; i <= 10; i++)
            {
                robot.PointerMoveTo(300 - i * 20, 50); // left: the strip scrolls
                maxShift = Math.Max(maxShift, (float)Math.Abs(drawer.TranslationX - openX));
            }
            for (int i = 1; i <= 6; i++)
            {
                robot.PointerMoveTo(100, 50 + i * 25); // then down, without lifting
                maxShift = Math.Max(maxShift, (float)Math.Abs(drawer.TranslationX - openX));
            }
            robot.PointerUp();
            host.AdvanceFrames(60);

            Assert.True(strip.ViewportOffsetX < -50, $"the strip did not scroll: {strip.ViewportOffsetX}");
            Assert.True(maxShift < 1, $"the drawer moved {maxShift} px during the strip's gesture");
            Assert.True(drawer.IsOpen);
        }
    }

    [Fact]
    public void Tap_ReachesAControlInsideTheDrawer()
    {
        int tapped = 0;
        var button = new SkiaButton("Tap") { WidthRequest = 120, HeightRequest = 60 }.OnTapped(me => tapped++);
        var (host, drawer, _) = Scene(button);
        using (host)
        {
            new GestureRobot(host).Tap(60, 30);
            host.AdvanceFrames(10);
            Assert.Equal(1, tapped);
            Assert.True(drawer.IsOpen);
        }
    }

    static SkiaScroll Strip(float top) => new SkiaScroll
    {
        Orientation = ScrollOrientation.Horizontal,
        HeightRequest = 100,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Start,
        Margin = new Thickness(0, top, 0, 0),
        Content = new SkiaRow
        {
            Spacing = 4,
            Children = Enumerable.Range(0, 30)
                .Select(i => (SkiaControl)new SkiaShape { BackgroundColor = Colors.Teal, WidthRequest = 80, HeightRequest = 80 })
                .ToList(),
        },
    };

    /// <summary>
    /// FiltersCamera: a horizontal strip over a full-screen swipe layer (a lower sibling), in a page root that keeps
    /// gestures from the pages below. Drag the strip left, then down out of it without lifting: the strip keeps the
    /// gesture. The canvas used to offer each move first to the page root, which took the very first move, and the
    /// root gave the moves to the swipe layer under the finger once it left the strip.
    /// </summary>
    [Fact]
    public void PanningStrip_KeepsTheGestureOutsideItsBounds_SiblingBelowGetsNothing()
    {
        SkiaScroll strip = Strip(300);
        int siblingPans = 0;
        using var host = new HeadlessCanvasHost(W, H, scale: 1f, background: Colors.Black);
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            BlockGesturesBelow = true,
            Children =
            {
                new SkiaLayout { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill }
                    .WithGestures((me, args, apply) =>
                    {
                        if (args.Type == TouchActionResult.Panning && strip.IsUserPanning)
                            siblingPans++;
                        return null;
                    }),
                strip,
            }
        };
        host.AdvanceFrames(4);
        var robot = new GestureRobot(host);

        robot.PointerDown(300, 350);
        for (int i = 1; i <= 10; i++)
            robot.PointerMoveTo(300 - i * 15, 350); // left: the strip scrolls
        Assert.True(strip.ViewportOffsetX < -50, $"the strip did not scroll: {strip.ViewportOffsetX}");
        for (int i = 1; i <= 8; i++)
            robot.PointerMoveTo(150, 350 + i * 15); // then down, out of the strip, without lifting
        robot.PointerUp();
        host.AdvanceFrames(30);

        Assert.Equal(0, siblingPans);
    }

    /// <summary>A horizontal strip inside a vertical page scroll: dragged left, then down, the page does not move.</summary>
    [Fact]
    public void PanningStrip_InsideAVerticalScroll_ThenDown_PageStays()
    {
        SkiaScroll strip = Strip(200);
        var content = new SkiaLayer { HorizontalOptions = LayoutOptions.Fill, Children = { strip } };
        for (int i = 0; i < 12; i++)
            content.AddSubView(new SkiaShape { BackgroundColor = Colors.DarkSlateBlue, HeightRequest = 80, HorizontalOptions = LayoutOptions.Fill, Margin = new Thickness(0, 320 + i * 90, 0, 0) });

        SkiaScroll page = null;
        using var host = new HeadlessCanvasHost(W, H, scale: 1f, background: Colors.Black);
        host.Canvas.Content = new SkiaScroll
        {
            Orientation = ScrollOrientation.Vertical,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = content,
        }.Assign(out page);
        host.AdvanceFrames(4);
        var robot = new GestureRobot(host);

        robot.PointerDown(300, 250);
        for (int i = 1; i <= 10; i++)
            robot.PointerMoveTo(300 - i * 15, 250);
        Assert.True(strip.ViewportOffsetX < -50, $"the strip did not scroll: {strip.ViewportOffsetX}");
        float maxPage = 0;
        for (int i = 1; i <= 8; i++)
        {
            robot.PointerMoveTo(150, 250 - i * 15); // then up (the page would scroll down), without lifting
            maxPage = Math.Max(maxPage, Math.Abs(page.ViewportOffsetY));
        }
        robot.PointerUp();
        host.AdvanceFrames(30);

        Assert.True(maxPage < 1, $"the page scrolled {maxPage} pt during the strip's gesture");
    }

    /// <summary>The drawer decides its own direction once: a gesture that starts vertical where nothing scrolls
    /// (a header) and then turns right does not move it either.</summary>
    [Fact]
    public void VerticalStartOnStaticArea_ThenHorizontal_DrawerStays()
    {
        var header = new SkiaShape { BackgroundColor = Colors.Orange, HeightRequest = 120, HorizontalOptions = LayoutOptions.Fill };
        var (host, drawer, _) = Scene(header);
        using (host)
        {
            var openX = drawer.TranslationX;
            var robot = new GestureRobot(host);

            robot.PointerDown(150, 20);
            for (int i = 1; i <= 4; i++)
                robot.PointerMoveTo(150, 20 + i * 20);
            float maxShift = 0;
            for (int i = 1; i <= 10; i++)
            {
                robot.PointerMoveTo(150 + i * 25, 100);
                maxShift = Math.Max(maxShift, (float)Math.Abs(drawer.TranslationX - openX));
            }
            robot.PointerUp();
            host.AdvanceFrames(60);

            Assert.True(maxShift < 1, $"the drawer moved {maxShift} px after the gesture started vertical");
            Assert.True(drawer.IsOpen);
        }
    }
}
