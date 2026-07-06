using System.Drawing;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

// Tests in one class run sequentially (shared DrawnUI global state via Super.Init).
public class GestureRobotTests
{
    private static (HeadlessCanvasHost host, SkiaScroll scroll) MakeScrollScene(
        int width = 400, int height = 600, float contentHeight = 3000)
    {
        var host = new HeadlessCanvasHost(width, height, scale: 1f, background: Colors.White);

        var content = new SkiaLayout
        {
            Type = LayoutType.Column,
            HorizontalOptions = LayoutOptions.Fill,
            WidthRequest = width,
            HeightRequest = contentHeight,
            BackgroundColor = Colors.LightYellow
        };

        var scroll = new SkiaScroll
        {
            Tag = "scroll",
            Orientation = ScrollOrientation.Vertical,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = content
        };

        host.Canvas.Content = scroll;
        host.RenderFrame(); // layout + gesture-listener registration
        return (host, scroll);
    }

    [Fact]
    public void Pan_ScrollsViewport()
    {
        var (host, scroll) = MakeScrollScene();
        using var _ = host;
        var robot = new GestureRobot(host);

        Assert.Equal(0f, scroll.ViewportOffsetY, 1f);

        robot.Pan(200, 500, 200, 200, durationMs: 250, steps: 16);

        // dragging the finger up scrolls content up → offset becomes negative
        Assert.True(scroll.ViewportOffsetY < -100f,
            $"expected scroll to move past -100, got {scroll.ViewportOffsetY}");
    }

    [Fact]
    public void Pan_TracksFingerOneToOne_WithoutFling()
    {
        var (host, scroll) = MakeScrollScene();
        using var _ = host;
        var robot = new GestureRobot(host);

        // Slow drag of exactly 200px over a long time → negligible release velocity.
        robot.Pan(200, 500, 200, 300, durationMs: 1200, steps: 40);

        // before any fling settling, offset should match the drag distance closely
        Assert.InRange(scroll.ViewportOffsetY, -210f, -190f);
    }

    [Fact]
    public void Fling_TravelsFurtherThanSlowPan_AndSettles()
    {
        // slow pan reference
        var (hostA, scrollA) = MakeScrollScene();
        using (hostA)
        {
            var robotA = new GestureRobot(hostA);
            robotA.Pan(200, 500, 200, 300, durationMs: 1200, steps: 40);
            robotA.SettleFling(scrollA);
            var slow = Math.Abs(scrollA.ViewportOffsetY);

            // fast flick of the same 200px distance
            var (hostB, scrollB) = MakeScrollScene();
            using (hostB)
            {
                var robotB = new GestureRobot(hostB);
                var frames = robotB.Fling(new PointF(200, 500), new PointF(200, 300), scrollB,
                    durationMs: 60, steps: 6);

                var fast = Math.Abs(scrollB.ViewportOffsetY);

                Assert.True(fast > slow + 100f, $"fling ({fast}) should exceed slow pan ({slow}) by margin");
                Assert.True(frames > 1, "fling should require multiple settle frames");

                // settled = a couple more frames must not move it
                var resting = scrollB.ViewportOffsetY;
                hostB.AdvanceFrames(5);
                Assert.Equal(resting, scrollB.ViewportOffsetY, 0.5f);
            }
        }
    }

    [Fact]
    public void Tap_DoesNotScroll()
    {
        var (host, scroll) = MakeScrollScene();
        using var _ = host;
        var robot = new GestureRobot(host);

        robot.Tap(200, 300);
        robot.SettleFling(scroll, maxFrames: 30);

        Assert.Equal(0f, scroll.ViewportOffsetY, 1f);
    }

    [Fact]
    public void Pan_IsDeterministic()
    {
        float Run()
        {
            var (host, scroll) = MakeScrollScene();
            using var _ = host;
            var robot = new GestureRobot(host);
            robot.Pan(200, 520, 200, 120, durationMs: 200, steps: 12);
            robot.SettleFling(scroll);
            return scroll.ViewportOffsetY;
        }

        var first = Run();
        var second = Run();
        Assert.Equal(first, second, 0.001f);
    }

    [Fact]
    public void WheelScroll_MovesViewport()
    {
        var (host, scroll) = MakeScrollScene();
        using var _ = host;
        // SkiaScroll routes wheel to zoom unless zoom is locked; lock it so wheel scrolls.
        scroll.ZoomLocked = true;
        host.RenderFrame();
        var robot = new GestureRobot(host);

        var before = scroll.ViewportOffsetY;
        // negative delta scrolls down (offset goes negative); magnitude is per-notch (WheelLineSize)
        robot.WheelScroll(200, 300, delta: -600f);
        robot.SettleFling(scroll, maxFrames: 120);

        Assert.True(scroll.ViewportOffsetY < before - 1f,
            $"wheel should scroll down; before={before} after={scroll.ViewportOffsetY}");
    }
}
