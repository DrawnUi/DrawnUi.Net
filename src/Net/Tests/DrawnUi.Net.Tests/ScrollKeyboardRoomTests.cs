using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaScroll.AdaptToKeyboardAuto: the keyboard's cover is room at the content end, so the LAST field rises above the
/// keyboard and stays there (a touch does not bounce it back), and the keyboard closing settles the page at the end.
/// </summary>
public class ScrollKeyboardRoomTests
{
    const float Keyboard = 300;

    static (HeadlessCanvasHost Host, SkiaScroll Scroll, SkiaControl Field) Form(bool auto)
    {
        var host = new HeadlessCanvasHost(400, 800, 1f, Colors.Black);
        SkiaScroll scroll;
        SkiaControl field;
        host.Canvas.Content = new SkiaScroll
        {
            AdaptToKeyboardAuto = auto,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Spacing = 0,
                Children =
                {
                    new SkiaControl { HeightRequest = 860, HorizontalOptions = LayoutOptions.Fill },
                    // the last field: content ends 900, it is at 860..900, the end of the scroll range is -100
                    new SkiaShape { HeightRequest = 40, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.White }.Assign(out field),
                }
            }
        }.Assign(out scroll);
        Settle(host);
        scroll.ScrollTo(0, -100, 0, true);
        Settle(host);
        return (host, scroll, field);
    }

    static float Bottom(SkiaControl c) => c.GetAccessibilityPixelRect().Bottom;

    static void Settle(HeadlessCanvasHost host)
    {
        for (var i = 0; i < 40; i++)
        {
            Thread.Sleep(15);
            host.RenderFrame(16.7);
        }
    }

    [Fact]
    public void The_last_field_rises_above_the_keyboard_and_stays_there()
    {
        var (host, scroll, field) = Form(true);
        using var _ = host;
        Assert.Equal(800, Bottom(field), 1);

        scroll.AdaptToKeyboardFor = field;
        scroll.AdaptToKeyboardSize = Keyboard;
        Settle(host);
        Assert.Equal(800 - Keyboard - 8, Bottom(field), 2);

        // a touch on the scroll (the next field's tap) must not bounce the page back past its end
        var robot = new GestureRobot(host);
        robot.Tap(200, 300);
        Settle(host);
        Assert.Equal(800 - Keyboard - 8, Bottom(field), 2);
    }

    [Fact]
    public void Keyboard_closing_settles_the_page_at_the_content_end()
    {
        var (host, scroll, field) = Form(true);
        using var _ = host;
        scroll.AdaptToKeyboardFor = field;
        scroll.AdaptToKeyboardSize = Keyboard;
        Settle(host);

        scroll.AdaptToKeyboardFor = null;
        scroll.AdaptToKeyboardSize = 0;
        Settle(host);
        Settle(host);
        Assert.Equal(-100, scroll.ViewportOffsetY, 1);
        Assert.Equal(800, Bottom(field), 1);
    }
}
