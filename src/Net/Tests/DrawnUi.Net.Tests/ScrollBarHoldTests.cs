using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// An auto-hiding scroll bar stays visible while the mouse is over the scroll (also over a button inside it, which
/// takes the exclusive hover), while keyboard focus is inside it and while KeepScrollBarsVisible is set; it hides
/// as usual once none of them holds it.
/// </summary>
public class ScrollBarHoldTests
{
    private readonly ITestOutputHelper _out;
    public ScrollBarHoldTests(ITestOutputHelper o) { _out = o; }

    private static (HeadlessCanvasHost host, SkiaScroll scroll, SkiaScrollBar bar, SkiaButton button) Scene()
    {
        var host = new HeadlessCanvasHost(300, 300, background: Colors.Black);
        SkiaScroll scroll = null;
        SkiaButton button = null;
        host.Canvas.Content = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaScroll
                {
                    HeightRequest = 200,
                    HorizontalOptions = LayoutOptions.Fill,
                    ScrollBarsVisibility = ScrollBarVisibility.Vertical,
                    Content = new SkiaStack
                    {
                        Spacing = 0,
                        Children =
                        {
                            new SkiaButton("Go") { WidthRequest = 100, HeightRequest = 40, AccessibilityRole = Aria.RoleButton }.Assign(out button),
                            new SkiaControl { HeightRequest = 800 },
                        }
                    }
                }.Assign(out scroll),
            }
        };
        host.AdvanceFrames(4);

        var bar = Assert.IsType<SkiaScrollBar>(scroll.ScrollBar);
        bar.HideDelaySecs = 0.05;
        bar.HideDurationSecs = 0;
        return (host, scroll, bar, button);
    }

    // the hide countdown runs on real time, the fades on frames
    private static void Settle(HeadlessCanvasHost host)
    {
        Thread.Sleep(200);
        host.AdvanceFrames(20);
    }

    [Fact]
    public void Hover_KeepsBarVisible_UntilPointerLeaves()
    {
        var (host, scroll, bar, button) = Scene();
        using var _ = host;

        Settle(host);
        Assert.Equal(0, bar.Opacity, 2); // auto-hidden after the first push

        host.Canvas.HandleDesktopPointerMove(150, 150, false, 300, 300); // over the scroll content
        Settle(host);
        Assert.True(scroll.IsPointerOver);
        Assert.Equal(1, bar.Opacity, 2);

        host.Canvas.HandleDesktopPointerMove(50, 20, false, 300, 300); // over the button: it takes the hover
        Settle(host);
        Assert.True(scroll.IsPointerOver, "the scroll lost pointer-over to the button inside it");
        Assert.Equal(1, bar.Opacity, 2);

        host.Canvas.HandleDesktopPointerMove(150, 280, false, 300, 300); // below the scroll, still on the canvas
        Settle(host);
        Assert.False(scroll.IsPointerOver);
        Assert.Equal(0, bar.Opacity, 2);

        host.Canvas.HandleDesktopPointerMove(150, 150, false, 300, 300);
        Settle(host);
        Assert.Equal(1, bar.Opacity, 2);

        host.Canvas.HandleDesktopPointerLeave(); // left the window
        Settle(host);
        Assert.False(scroll.IsPointerOver);
        Assert.Equal(0, bar.Opacity, 2);
    }

    [Fact]
    public void KeyboardFocusInside_And_KeepScrollBarsVisible_HoldTheBar()
    {
        var (host, scroll, bar, button) = Scene();
        using var _ = host;
        Settle(host);
        Assert.Equal(0, bar.Opacity, 2);

        host.Canvas.KeyboardFocusNode = button;
        Settle(host);
        Assert.Equal(1, bar.Opacity, 2);

        host.Canvas.KeyboardFocusNode = null;
        Settle(host);
        Assert.Equal(0, bar.Opacity, 2);

        scroll.KeepScrollBarsVisible = true;
        Settle(host);
        Assert.Equal(1, bar.Opacity, 2);

        scroll.KeepScrollBarsVisible = false;
        Settle(host);
        Assert.Equal(0, bar.Opacity, 2);
    }

    [Fact]
    public void ShowScrollBarsOnHover_False_IgnoresHover()
    {
        var (host, scroll, bar, button) = Scene();
        using var _ = host;
        scroll.ShowScrollBarsOnHover = false;
        Settle(host);

        host.Canvas.HandleDesktopPointerMove(150, 150, false, 300, 300);
        Settle(host);
        Assert.Equal(0, bar.Opacity, 2);
    }
}
