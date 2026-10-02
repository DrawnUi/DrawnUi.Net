using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Mouse wheel on the desktop heads (WPF, OpenTK: Windows units, 120 a notch): a scroll moves by the event's share of
/// a notch. A precision touchpad sends many small events and must scroll as far as the fingers moved, not a full line
/// per event; mouse notches keep one line each and a fast spin still adds up. An event under half a notch moves at once
/// (easing each one kept the content behind the fingers and SpringOut overshot the end of a swipe); notches glide.
/// </summary>
public class WheelTouchpadTests
{
    private readonly ITestOutputHelper _out;
    public WheelTouchpadTests(ITestOutputHelper o) { _out = o; }

    private static (HeadlessCanvasHost host, SkiaScroll scroll) Scene()
    {
        var host = new HeadlessCanvasHost(300, 400);
        SkiaScroll scroll = null;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaControl { HeightRequest = 20000, HorizontalOptions = LayoutOptions.Fill },
        }.Assign(out scroll);
        host.AdvanceFrames(4);
        return (host, scroll);
    }

    private static float Travel(HeadlessCanvasHost host, SkiaScroll scroll, int delta, int events, double gapMs)
    {
        var start = scroll.ViewportOffsetY;
        for (var i = 0; i < events; i++)
        {
            host.Canvas.HandleDesktopWheel(150, 200, delta, 300, 400);
            host.RenderFrame(gapMs);
        }
        host.AdvanceFrames(120);
        return start - scroll.ViewportOffsetY;
    }

    [Fact]
    public void TouchpadBurst_ScrollsItsShareOfANotch()
    {
        var (host, scroll) = Scene();
        using var _ = host;

        // 20 events of 6 = one notch in total, the way a precision touchpad reports a short swipe
        var travel = Travel(host, scroll, -6, 20, 8);
        _out.WriteLine($"touchpad travel {travel}");
        Assert.Equal(SkiaScroll.WheelLineSize, travel, 1);
    }

    [Fact]
    public void MouseNotches_OneLineEach_FastSpinAddsUp()
    {
        var (host, scroll) = Scene();
        using var _ = host;

        Assert.Equal(SkiaScroll.WheelLineSize, Travel(host, scroll, -120, 1, 16), 1);

        var spin = Travel(host, scroll, -120, 8, 40);
        _out.WriteLine($"spin travel {spin}");
        Assert.Equal(8 * SkiaScroll.WheelLineSize, spin, 1);
    }

    [Fact]
    public void TouchpadEvents_MoveAtOnce_NoOvershoot()
    {
        var (host, scroll) = Scene();
        using var _ = host;
        var start = scroll.ViewportOffsetY;
        var share = SkiaScroll.WheelLineSize * 10 / 120f; // an event of 10 Windows units

        for (var i = 1; i <= 15; i++)
        {
            host.Canvas.HandleDesktopWheel(150, 200, -10, 300, 400);
            host.RenderFrame(16);
            var moved = start - scroll.ViewportOffsetY;
            _out.WriteLine($"event {i}: moved {moved:0.0}");
            Assert.Equal(share * i, moved, 0.5);
        }

        var furthest = 0f;
        for (var i = 0; i < 60; i++)
        {
            host.RenderFrame(16);
            furthest = Math.Max(furthest, start - scroll.ViewportOffsetY);
        }
        Assert.Equal(share * 15, furthest, 0.5);
    }

    private static (HeadlessCanvasHost host, SkiaScroll outer, SkiaScroll inner) NestedScene()
    {
        var host = new HeadlessCanvasHost(300, 400);
        SkiaScroll outer = null, inner = null;
        host.Canvas.Content = new SkiaScroll
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaRow
            {
                Children =
                {
                    new SkiaScroll
                    {
                        WidthRequest = 300,
                        HeightRequest = 400,
                        Content = new SkiaControl { HeightRequest = 20000, WidthRequest = 300 },
                    }.Assign(out inner),
                    new SkiaControl { WidthRequest = 3000, HeightRequest = 400 },
                }
            },
        }.Assign(out outer);
        host.AdvanceFrames(4);
        return (host, outer, inner);
    }

    [Fact]
    public void HorizontalEvents_LeaveAVerticalList_ReachTheHorizontalScroll()
    {
        var (host, outer, inner) = NestedScene();
        using var _ = host;

        // toward the end (right), as Windows reports a swipe to the right after the sign flip
        for (var i = 0; i < 10; i++)
        {
            host.Canvas.HandleDesktopWheel(150, 200, -12, 300, 400, horizontal: true);
            host.RenderFrame(16);
        }
        host.AdvanceFrames(30);

        _out.WriteLine($"inner y {inner.ViewportOffsetY} outer x {outer.ViewportOffsetX}");
        Assert.Equal(0, inner.ViewportOffsetY, 0.5);
        Assert.Equal(-SkiaScroll.WheelLineSize, outer.ViewportOffsetX, 1);
    }

    [Fact]
    public void VerticalWheel_StillScrollsAHorizontalScroll()
    {
        var host = new HeadlessCanvasHost(300, 400);
        using var _ = host;
        SkiaScroll scroll = null;
        host.Canvas.Content = new SkiaScroll
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaControl { WidthRequest = 20000, HeightRequest = 400 },
        }.Assign(out scroll);
        host.AdvanceFrames(4);

        host.Canvas.HandleDesktopWheel(150, 200, -120, 300, 400);
        host.AdvanceFrames(60);

        Assert.Equal(-SkiaScroll.WheelLineSize, scroll.ViewportOffsetX, 1);
    }

    [Fact]
    public void FastSwipe_EventBeforeEveryFrame_MovesEveryFrame()
    {
        var (host, scroll) = Scene();
        using var _ = host;
        var start = scroll.ViewportOffsetY;
        var previous = 0f;
        var stalls = 0;

        // a fast trackpad swipe: an event over half a notch (glide path) before every frame
        for (var i = 1; i <= 20; i++)
        {
            host.Canvas.HandleDesktopWheel(150, 200, -72, 300, 400);
            host.RenderFrame(16);
            var moved = start - scroll.ViewportOffsetY;
            _out.WriteLine($"frame {i}: moved {moved:0.0} of {SkiaScroll.WheelLineSize * 72 / 120f * i:0.0} arrived");
            if (i > 1 && moved <= previous + 0.5f) // the first glide starts at its first frame; restarted ones must move
                stalls++;
            previous = moved;
        }

        Assert.Equal(0, stalls);
    }
}
