using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaScroll.AdaptToKeyboardFor / AdaptToKeyboardSize (the on-screen keyboard over a form): the focused field's bottom
/// lands 8 points above the keyboard's top, a field already above it does not move the page, and the keyboard closing
/// leaves the page where it is.
/// </summary>
public class ScrollKeyboardAdaptTests
{
    const float Keyboard = 300;

    static (HeadlessCanvasHost Host, SkiaScroll Scroll, SkiaControl Field, SkiaControl Upper) Form()
    {
        var host = new HeadlessCanvasHost(400, 800, 1f, Colors.Black);
        SkiaScroll scroll;
        SkiaControl field, upper;
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaScroll
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Content = new SkiaStack
                    {
                        Spacing = 0,
                        Children =
                        {
                            new SkiaControl { HeightRequest = 600, HorizontalOptions = LayoutOptions.Fill },
                            new SkiaShape { HeightRequest = 40, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.Gray }.Assign(out upper),
                            new SkiaControl { HeightRequest = 260, HorizontalOptions = LayoutOptions.Fill },
                            new SkiaShape { HeightRequest = 40, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.White }.Assign(out field),
                            new SkiaControl { HeightRequest = 1000, HorizontalOptions = LayoutOptions.Fill },
                        }
                    }
                }.Assign(out scroll),
                // a bar drawn over the top of the scroll, as an app's header
                new SkiaShape { HeightRequest = 150, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.DarkBlue },
            }
        };
        for (var i = 0; i < 3; i++)
            host.RenderFrame(16.7);
        // the field at 700..740 on screen, the upper one at 400..440
        scroll.ScrollTo(0, -200, 0, true);
        for (var i = 0; i < 3; i++)
            host.RenderFrame(16.7);
        return (host, scroll, field, upper);
    }

    static float Bottom(SkiaControl c) => c.GetAccessibilityPixelRect().Bottom;

    /// <summary>Frames until the 150 ms delayed adaptation has run and the scroll rests.</summary>
    static void Settle(HeadlessCanvasHost host)
    {
        for (var i = 0; i < 40; i++)
        {
            Thread.Sleep(15);
            host.RenderFrame(16.7);
        }
    }

    [Fact]
    public void The_focused_field_lands_just_above_the_keyboard()
    {
        var (host, scroll, field, _) = Form();
        using var _ = host;
        Assert.Equal(740, Bottom(field), 1);

        scroll.AdaptToKeyboardFor = field;
        scroll.AdaptToKeyboardSize = Keyboard;
        Settle(host);

        Assert.Equal(800 - Keyboard - 8, Bottom(field), 2); // 8 points above the keyboard
    }

    [Fact]
    public void A_field_above_the_keyboard_does_not_move_the_page()
    {
        var (host, scroll, _, upper) = Form();
        using var _ = host;
        var offset = scroll.ViewportOffsetY;

        scroll.AdaptToKeyboardFor = upper;
        scroll.AdaptToKeyboardSize = Keyboard;
        Settle(host);

        Assert.Equal(offset, scroll.ViewportOffsetY, 1);
        Assert.Equal(440, Bottom(upper), 1);
    }

    /// <summary>
    /// The iPhone 14 report (844 pt screen, keyboard top at 536): Name, Email and Password at 365, 420 and 476, all
    /// above the keyboard. Focusing each one, and the keyboard closing, moves nothing.
    /// </summary>
    [Fact]
    public void A_form_already_above_the_keyboard_stays_put()
    {
        using var host = new HeadlessCanvasHost(390, 844, 1f, Colors.Black);
        SkiaScroll scroll;
        SkiaControl name, email, password;
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaScroll
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Content = new SkiaStack
                    {
                        Spacing = 0,
                        Children =
                        {
                            new SkiaControl { HeightRequest = 1365, HorizontalOptions = LayoutOptions.Fill },
                            new SkiaShape { HeightRequest = 44, HorizontalOptions = LayoutOptions.Fill }.Assign(out name),
                            new SkiaControl { HeightRequest = 11, HorizontalOptions = LayoutOptions.Fill },
                            new SkiaShape { HeightRequest = 44, HorizontalOptions = LayoutOptions.Fill }.Assign(out email),
                            new SkiaControl { HeightRequest = 12, HorizontalOptions = LayoutOptions.Fill },
                            new SkiaShape { HeightRequest = 44, HorizontalOptions = LayoutOptions.Fill }.Assign(out password),
                            new SkiaControl { HeightRequest = 2000, HorizontalOptions = LayoutOptions.Fill },
                        }
                    }
                }.Assign(out scroll),
                new SkiaShape { HeightRequest = 155, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.DarkBlue },
            }
        };
        for (var i = 0; i < 3; i++)
            host.RenderFrame(16.7);
        scroll.ScrollTo(0, -1000, 0, true);
        Settle(host);
        Assert.Equal(365, name.GetAccessibilityPixelRect().Top, 1);
        Assert.Equal(476, password.GetAccessibilityPixelRect().Top, 1);

        foreach (var field in new[] { name, email, password })
        {
            scroll.AdaptToKeyboardFor = field;
            scroll.AdaptToKeyboardSize = 844 - 536;
            Settle(host);
            Assert.Equal(-1000, scroll.ViewportOffsetY, 1);
        }
        scroll.AdaptToKeyboardFor = null;
        scroll.AdaptToKeyboardSize = 0;
        Settle(host);
        Assert.Equal(-1000, scroll.ViewportOffsetY, 1);
    }

    /// <summary>
    /// The iPhone 14 (844 pt, keyboard top at 536) with the form low on screen: Name, covered by the keyboard, ends
    /// AdaptToKeyboardGap points above it (8 by default, 0 = touching).
    /// </summary>
    [Theory]
    [InlineData(8)]
    [InlineData(0)]
    public void A_covered_field_ends_the_gap_above_the_keyboard(double gap)
    {
        using var host = new HeadlessCanvasHost(390, 844, 1f, Colors.Black);
        SkiaScroll scroll;
        SkiaControl name;
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaScroll
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Content = new SkiaStack
                    {
                        Spacing = 0,
                        Children =
                        {
                            new SkiaControl { HeightRequest = 1700, HorizontalOptions = LayoutOptions.Fill },
                            new SkiaShape { HeightRequest = 44, HorizontalOptions = LayoutOptions.Fill }.Assign(out name),
                            new SkiaControl { HeightRequest = 2000, HorizontalOptions = LayoutOptions.Fill },
                        }
                    }
                }.Assign(out scroll),
                new SkiaShape { HeightRequest = 155, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.DarkBlue },
            }
        };
        if (gap != 8)
            scroll.AdaptToKeyboardGap = gap;
        for (var i = 0; i < 3; i++)
            host.RenderFrame(16.7);
        scroll.ScrollTo(0, -1000, 0, true);
        Settle(host);
        Assert.Equal(700, name.GetAccessibilityPixelRect().Top, 1);

        scroll.AdaptToKeyboardFor = name;
        scroll.AdaptToKeyboardSize = 844 - 536;
        Settle(host);

        Assert.Equal(536 - gap, Bottom(name), 1);
    }

    [Fact]
    public void The_keyboard_closing_leaves_the_page_where_it_is()
    {
        var (host, scroll, field, _) = Form();
        using var _ = host;
        scroll.AdaptToKeyboardFor = field;
        scroll.AdaptToKeyboardSize = Keyboard;
        Settle(host);
        var offset = scroll.ViewportOffsetY;

        scroll.AdaptToKeyboardFor = null;
        scroll.AdaptToKeyboardSize = 0;
        Settle(host);

        Assert.Equal(offset, scroll.ViewportOffsetY, 1);
    }
}
