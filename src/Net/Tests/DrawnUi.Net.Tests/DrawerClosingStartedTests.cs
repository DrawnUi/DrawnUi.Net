using System.Reflection;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaDrawer.ClosingStarted (internal): raised once when the drawer starts moving from open to closed, at the
/// swipe's release, while IsOpen still says open until the slide ends. The MAUI SkiaShell takes a modal out of
/// NavigationStackModals on it, so a swiped-away modal leaves the stack at release, as with PopModalAsync.
/// </summary>
public class DrawerClosingStartedTests
{
    const int W = 400, H = 700;

    // the drawer SkiaShell wraps a modal in
    static (HeadlessCanvasHost host, SkiaDrawer drawer, List<bool> fired) Scene(bool open = true)
    {
        var host = new HeadlessCanvasHost(W, H, scale: 1f, background: Colors.Black);
        SkiaDrawer drawer = null;
        host.Canvas.Content = new SkiaDrawer
        {
            Direction = DrawerDirection.FromBottom,
            HeaderSize = 0,
            Bounces = false,
            RespondsToGestures = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                BackgroundColor = Colors.Gray,
            },
        }.Assign(out drawer);

        var fired = new List<bool>(); // IsOpen at each ClosingStarted
        typeof(SkiaDrawer).GetEvent("ClosingStarted", BindingFlags.Instance | BindingFlags.NonPublic)!
            .AddMethod!.Invoke(drawer, new object[] { new EventHandler((_, _) => fired.Add(drawer.IsOpen)) });

        host.AdvanceFrames(4);
        if (open)
        {
            drawer.IsOpen = true;
            host.AdvanceFrames(60);
        }
        return (host, drawer, fired);
    }

    static void DragDown(GestureRobot robot, float distance, int moves)
    {
        float y = 200;
        robot.PointerDown(200, y);
        for (int i = 0; i < moves; i++)
        {
            y += distance / moves;
            robot.PointerMoveTo(200, y);
        }
        robot.PointerUp();
    }

    [Fact]
    public void Opening_DoesNotRaise()
    {
        var (host, drawer, fired) = Scene();
        using (host)
        {
            Assert.True(drawer.IsOpen);
            Assert.Empty(fired);
        }
    }

    [Fact]
    public void SwipeClose_RaisesOnceAtRelease_WhileIsOpenStillTrue()
    {
        var (host, drawer, fired) = Scene();
        using (host)
        {
            DragDown(new GestureRobot(host), 300, 6);
            host.AdvanceFrames(1);
            Assert.Equal(new[] { true }, fired); // raised at release, the slide-out still running
            host.AdvanceFrames(90);
            Assert.False(drawer.IsOpen);
            Assert.Single(fired);
        }
    }

    [Fact]
    public void SwipeSnapsBackOpen_DoesNotRaise()
    {
        var (host, drawer, fired) = Scene();
        using (host)
        {
            var robot = new GestureRobot(host);
            robot.PointerDown(200, 200);
            for (int i = 0; i < 30; i++)
                robot.PointerMoveTo(200, 200 + (i + 1) * 2); // 60 pt, slowly
            robot.PointerHold(19);
            robot.PointerUp();
            host.AdvanceFrames(90);
            Assert.True(drawer.IsOpen);
            Assert.Empty(fired);
        }
    }

    [Fact]
    public void IsOpenFalse_RaisesOnce()
    {
        var (host, drawer, fired) = Scene();
        using (host)
        {
            drawer.IsOpen = false;
            host.AdvanceFrames(90);
            Assert.Single(fired);
            Assert.False(drawer.IsOpen);
        }
    }
}
