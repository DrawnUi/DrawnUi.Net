using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A touch on a drawer that is moving to a snap (IsOpen changed in code, or a released swipe) stops the move on
/// Down, so a pan can take the drawer from where it is. When no pan followed, the drawer stayed where the touch
/// caught it, between its snaps, for good (FiltersCamera, adjustments sheet closing: stuck halfway).
/// </summary>
public class DrawerInterruptedSnapTests
{
    const int W = 300, H = 600;

    static (HeadlessCanvasHost host, SkiaDrawer drawer) OpenDrawer(double panelHeight = -1, bool autoClose = false)
    {
        var host = new HeadlessCanvasHost(W, H, scale: 1f, background: Colors.Black);
        SkiaDrawer drawer = null;
        host.Canvas.Content = new SkiaDrawer
        {
            Direction = DrawerDirection.FromBottom,
            AutoClose = autoClose,
            AutoVelocity = 375, // a 0.56 s move, so a touch lands well inside it
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaShape
            {
                BackgroundColor = Colors.Gray,
                HeightRequest = panelHeight,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = panelHeight > 0 ? LayoutOptions.End : LayoutOptions.Fill,
            },
        }.Assign(out drawer);
        host.AdvanceFrames(4);
        drawer.IsOpen = true;
        host.AdvanceFrames(60);
        Assert.True(drawer.IsOpen);
        Assert.Equal(0, drawer.TranslationY, 1);
        return (host, drawer);
    }

    /// <summary>Runs the close move for some frames and checks the drawer is between its snaps.</summary>
    static void IntoTheMove(HeadlessCanvasHost host, SkiaDrawer drawer, int frames = 12)
    {
        host.AdvanceFrames(frames);
        Assert.InRange(drawer.TranslationY, 1, H - 1);
    }

    static void AssertClosed(HeadlessCanvasHost host, SkiaDrawer drawer)
    {
        host.AdvanceFrames(60);
        Assert.False(drawer.IsOpen);
        Assert.Equal(H, drawer.TranslationY, 1);
        Assert.False(drawer.InTransition);
    }

    [Fact]
    public void ClosedInCode_TapMidMove_EndsClosed()
    {
        var (host, drawer) = OpenDrawer();
        using (host)
        {
            drawer.IsOpen = false;
            IntoTheMove(host, drawer);

            new GestureRobot(host).Tap(150, H - 10);

            AssertClosed(host, drawer);
        }
    }

    [Fact]
    public void ClosedInCode_HoldMidMove_EndsClosedOnRelease()
    {
        var (host, drawer) = OpenDrawer();
        using (host)
        {
            drawer.IsOpen = false;
            IntoTheMove(host, drawer);

            var robot = new GestureRobot(host);
            robot.PointerDown(150, H - 10);
            robot.PointerHold(20);
            robot.PointerUp();

            AssertClosed(host, drawer);
        }
    }

    [Fact]
    public void SwipedClosed_TapMidMove_EndsClosed()
    {
        var (host, drawer) = OpenDrawer();
        using (host)
        {
            var robot = new GestureRobot(host);
            robot.PointerDown(150, 100);
            for (int i = 1; i <= 4; i++)
                robot.PointerMoveTo(150, 100 + i * 25); // a fast flick down
            robot.PointerUp();
            host.AdvanceFrames(2);
            Assert.InRange(drawer.TranslationY, 1, H - 1);

            robot.Tap(150, H - 10);

            AssertClosed(host, drawer);
        }
    }

    /// <summary>A pan that starts during the move still takes the drawer: dragged back up, it opens.</summary>
    [Fact]
    public void ClosedInCode_PanMidMove_FingerTakesIt()
    {
        var (host, drawer) = OpenDrawer();
        using (host)
        {
            drawer.IsOpen = false;
            IntoTheMove(host, drawer, 18);
            var caughtAt = drawer.TranslationY;

            var robot = new GestureRobot(host);
            float y = H - 10;
            robot.PointerDown(150, y);
            for (int i = 0; i < 20; i++)
            {
                y -= 10;
                robot.PointerMoveTo(150, y);
            }
            Assert.True(drawer.TranslationY < caughtAt - 100, "the drawer did not follow the finger");
            robot.PointerHold(19);
            robot.PointerUp();

            host.AdvanceFrames(60);
            Assert.True(drawer.IsOpen);
            Assert.Equal(0, drawer.TranslationY, 1);
        }
    }

    /// <summary>A touch on the panel ends with its Up: a later press outside the panel still closes it.</summary>
    [Fact]
    public void AutoClose_AfterTouchOnPanel_PressOutsideCloses()
    {
        var (host, drawer) = OpenDrawer(panelHeight: 150, autoClose: true);
        using (host)
        {
            var robot = new GestureRobot(host);
            robot.Tap(150, H - 50);
            host.AdvanceFrames(4);
            Assert.True(drawer.IsOpen);

            robot.Tap(150, 50);

            AssertClosed(host, drawer);
        }
    }
}
