using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Where a side drawer goes when released. A finger held still before lifting is not a flick: the drawer goes
/// to the nearest anchor by distance. It used to close on ANY velocity toward closing, however small, and a
/// finger held "still" on a phone still trembles a little (FiltersCamera: 70 pt drag, hold, release: closed).
/// </summary>
public class DrawerReleaseSnapTests
{
    const int W = 400, H = 700;

    static (HeadlessCanvasHost host, SkiaDrawer drawer) Scene()
    {
        var host = new HeadlessCanvasHost(W, H, scale: 1f, background: Colors.Black);
        SkiaDrawer drawer = null;
        host.Canvas.Content = new SkiaDrawer
        {
            Direction = DrawerDirection.FromRight,
            RespondsToGestures = true,
            IgnoreWrongDirection = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                BackgroundColor = Colors.Gray,
            },
        }.Assign(out drawer);
        host.AdvanceFrames(4);
        drawer.IsOpen = true;
        host.AdvanceFrames(60);
        return (host, drawer);
    }

    /// <summary>Drags right by <paramref name="distance"/> pt in <paramref name="moves"/> 16 ms moves, then holds
    /// for <paramref name="holdMoves"/> moves of <paramref name="jitter"/> pt each (a resting finger), then lifts.</summary>
    static void Drag(GestureRobot robot, float distance, int moves, int holdMoves = 0, float jitter = 0)
    {
        float x = 50;
        robot.PointerDown(x, 300);
        for (int i = 0; i < moves; i++)
        {
            x += distance / moves;
            robot.PointerMoveTo(x, 300);
        }
        for (int i = 0; i < holdMoves; i++)
        {
            if (jitter == 0)
                robot.PointerHold(1);
            else
            {
                x += (i % 3 == 2) ? -jitter : jitter; // mostly drifting right, as a resting fingertip does
                robot.PointerMoveTo(x, 300);
            }
        }
        robot.PointerUp();
    }

    [Theory]
    [InlineData(0f)]   // perfectly still
    [InlineData(0.5f)] // a resting fingertip
    public void SlowDrag_Hold_Release_SnapsBack(float jitter)
    {
        var (host, drawer) = Scene();
        using (host)
        {
            Drag(new GestureRobot(host), 70, 30, 19, jitter); // 70 pt over ~0.5 s, held ~0.3 s
            host.AdvanceFrames(90);
            Assert.True(drawer.IsOpen, "a held, released 70 pt drag closed the drawer");
            Assert.Equal(0, drawer.TranslationX, 1);
        }
    }

    [Fact]
    public void FastShortFlick_Closes()
    {
        var (host, drawer) = Scene();
        using (host)
        {
            Drag(new GestureRobot(host), 60, 4); // 60 pt in ~64 ms
            host.AdvanceFrames(90);
            Assert.False(drawer.IsOpen, "a fast flick did not close the drawer");
        }
    }

    [Fact]
    public void SlowDragPastHalf_Hold_Release_Closes()
    {
        var (host, drawer) = Scene();
        using (host)
        {
            Drag(new GestureRobot(host), 260, 90, 19); // past half of 400, slowly, then held still
            host.AdvanceFrames(90);
            Assert.False(drawer.IsOpen, "a drag past half did not close the drawer");
        }
    }
}
