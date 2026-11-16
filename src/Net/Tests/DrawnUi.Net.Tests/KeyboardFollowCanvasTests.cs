using System.Reflection;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The canvas follows the app-wide keyboard (Super.KeyboardTop) by itself: the focused editor inside a scroll with
/// SkiaScroll.AdaptToKeyboardAuto rises above the keyboard (its gap clear), a scroll without the setting is left alone,
/// and the keyboard closing lets the scroll go. Shared by every head; a head only reports the keyboard top and where
/// its canvas ends (here a canvas spanning 0..800 page points).
/// </summary>
[Collection("Keyboard")]
public class KeyboardFollowCanvasTests
{
    private readonly ITestOutputHelper _out;
    public KeyboardFollowCanvasTests(ITestOutputHelper o) { _out = o; }

    sealed class PageCanvas : Canvas
    {
        protected override double GetBottomOnScreen() => 800;
    }

    static readonly MethodInfo SetKeyboardTop =
        typeof(Super).GetMethod("SetKeyboardTop", BindingFlags.NonPublic | BindingFlags.Static)!;

    static void Keyboard(double top) => SetKeyboardTop.Invoke(null, new object[] { top });

    static (HeadlessCanvasHost host, SkiaScroll scroll, SkiaEditor first, SkiaEditor second) Form(bool auto)
    {
        var host = new HeadlessCanvasHost(new PageCanvas(), 400, 800, 1f, Colors.Black);
        SkiaScroll scroll;
        SkiaEditor first, second;
        host.Canvas.Content = new SkiaScroll
        {
            AdaptToKeyboardAuto = auto,
            AdaptToKeyboardGap = 16,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Spacing = 0,
                Children =
                {
                    new SkiaShape { HeightRequest = 560, HorizontalOptions = LayoutOptions.Fill },
                    new SkiaEditor { MaxLines = 1, HeightRequest = 44, FontSize = 16, HorizontalOptions = LayoutOptions.Fill }.Assign(out first),
                    new SkiaShape { HeightRequest = 60, HorizontalOptions = LayoutOptions.Fill },
                    new SkiaEditor { MaxLines = 1, HeightRequest = 44, FontSize = 16, HorizontalOptions = LayoutOptions.Fill }.Assign(out second),
                    new SkiaShape { HeightRequest = 600, HorizontalOptions = LayoutOptions.Fill },
                }
            }
        }.Assign(out scroll);
        Settle(host);
        return (host, scroll, first, second);
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
    public void FocusedField_RisesAboveTheKeyboard_AndTheScrollLetsGoWhenItCloses()
    {
        var (host, scroll, first, second) = Form(auto: true);
        using var _ = host;
        try
        {
            first.IsFocused = true;
            Settle(host);
            Assert.Equal(604, Bottom(first), 1);

            Keyboard(500); // covers the canvas from 500 down
            Settle(host);
            _out.WriteLine($"first bottom {Bottom(first)}, offset {scroll.ViewportOffsetY}");
            Assert.InRange(Bottom(first), 482, 486); // 16 clear of 500

            // the focus moves to a field the open keyboard covers: it rises too
            second.IsFocused = true;
            Settle(host);
            _out.WriteLine($"second bottom {Bottom(second)}");
            Assert.InRange(Bottom(second), 482, 486);

            Keyboard(double.NaN);
            Settle(host);
            Assert.Null(scroll.AdaptToKeyboardFor);
            Assert.Equal(0, scroll.AdaptToKeyboardSize);
        }
        finally
        {
            Keyboard(double.NaN);
        }
    }

    [Fact]
    public void KeyboardBlink_FullHeightForAMoment_DoesNotDropTheField()
    {
        // Android Chrome reports the full height for a moment when the focus moves to the next field (the keyboard
        // restarts): the lifted field stays where it is
        var (host, scroll, first, _) = Form(auto: true);
        using var _ = host;
        try
        {
            first.IsFocused = true;
            Settle(host);
            Keyboard(500);
            Settle(host);
            var lifted = scroll.ViewportOffsetY;
            var bottom = Bottom(first);

            Keyboard(double.NaN);
            for (var i = 0; i < 6; i++) // ~100 ms
            {
                Thread.Sleep(15);
                host.RenderFrame(16.7);
            }
            Keyboard(500);
            Settle(host);

            _out.WriteLine($"offset {lifted} -> {scroll.ViewportOffsetY}, bottom {bottom} -> {Bottom(first)}");
            Assert.Equal(lifted, scroll.ViewportOffsetY, 1);
            Assert.Equal(bottom, Bottom(first), 1);
        }
        finally
        {
            Keyboard(double.NaN);
        }
    }

    [Fact]
    public void ScrollWithoutTheSetting_IsLeftAlone()
    {
        var (host, scroll, first, _) = Form(auto: false);
        using var __ = host;
        try
        {
            first.IsFocused = true;
            Settle(host);
            Keyboard(500);
            Settle(host);
            Assert.Equal(604, Bottom(first), 1);
            Assert.Null(scroll.AdaptToKeyboardFor);
        }
        finally
        {
            Keyboard(double.NaN);
        }
    }
}
