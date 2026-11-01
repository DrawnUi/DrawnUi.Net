using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Popup windows (OpenPopupWindow, the API of DrawnUI for Rust): a menu opens at a point or below a control, flips
/// where the canvas ends, takes the input over it, and closes with a press outside (that press goes nowhere), Escape
/// or from code.
/// </summary>
[Collection("Tooltips")] // Escape goes through the static KeyboardManager
public class PopupWindowTests
{
    const int W = 400, H = 400;

    static SkiaControl Menu(List<string> picked, params string[] items) => new SkiaStack
    {
        WidthRequest = 120,
        Spacing = 0,
        HorizontalOptions = LayoutOptions.Start,
        BackgroundColor = Colors.DarkGray,
        Children = items.Select(text => (SkiaControl)new SkiaLayer
        {
            HeightRequest = 30,
            HorizontalOptions = LayoutOptions.Fill,
            Children = { new SkiaLabel(text) },
        }.OnTapped(me => picked.Add(text))).ToList(),
    };

    static HeadlessCanvasHost Scene(out SkiaShape button, out List<string> tapped)
    {
        var list = new List<string>();
        tapped = list;
        var host = new HeadlessCanvasHost(W, H);
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl>
            {
                new SkiaShape { BackgroundColor = Colors.Gray, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill }
                    .OnTapped(me => list.Add("background")),
                new SkiaShape { WidthRequest = 100, HeightRequest = 40, Margin = new Thickness(20, 20, 0, 0), BackgroundColor = Colors.Blue }
                    .Assign(out button),
            },
        };
        host.AdvanceFrames(3);
        return host;
    }

    static void Tap(HeadlessCanvasHost host, float x, float y)
    {
        host.Canvas.HandleDesktopPointerDown(x, y, W, H);
        host.Canvas.HandleDesktopPointerUp(x, y, W, H);
        host.AdvanceFrames(2);
    }

    [Fact]
    public void OpensAtThePoint_ItemTapPicks_NothingBelowGetsIt()
    {
        using var host = Scene(out _, out var tapped);
        var picked = new List<string>();

        var id = host.Canvas.OpenPopupWindow(Menu(picked, "Cut", "Copy", "Paste"), new SKPoint(150, 150));
        host.AdvanceFrames(3);

        Assert.True(host.Canvas.IsPopupWindowOpen(id));
        var root = host.Canvas.TopPopupWindow;
        var box = root.HitBoxAuto;
        Assert.Equal(150, box.Left, 1.5);
        Assert.Equal(150, box.Top, 1.5);

        Tap(host, 160, 150 + 45); // the second item
        Assert.Equal(new[] { "Copy" }, picked);
        Assert.Empty(tapped); // the background under the menu got nothing
    }

    [Fact]
    public void FlipsWhereTheCanvasEnds()
    {
        using var host = Scene(out _, out _);
        host.Canvas.OpenPopupWindow(Menu(new List<string>(), "A", "B", "C"), new SKPoint(380, 380));
        host.AdvanceFrames(3);

        var box = host.Canvas.TopPopupWindow.HitBoxAuto;
        Assert.Equal(380 - 120, box.Left, 1.5); // to the left of the point
        Assert.Equal(380 - 90, box.Top, 1.5); // above it
    }

    [Fact]
    public void PressOutside_Closes_AndThatPressGoesNowhere()
    {
        using var host = Scene(out _, out var tapped);
        var id = host.Canvas.OpenPopupWindow(Menu(new List<string>(), "A", "B"), new SKPoint(150, 150));
        host.AdvanceFrames(3);

        Tap(host, 50, 300);
        Assert.False(host.Canvas.IsPopupWindowOpen(id));
        Assert.Empty(tapped);

        Tap(host, 50, 300); // the next press works again
        Assert.Equal(new[] { "background" }, tapped);
    }

    [Fact]
    public void EscapeCloses()
    {
        using var host = Scene(out _, out _);
        var id = host.Canvas.OpenPopupWindow(Menu(new List<string>(), "A"), new SKPoint(150, 150));
        host.AdvanceFrames(2);

        KeyboardManager.KeyboardPressed(InputKey.Escape);
        KeyboardManager.KeyboardReleased(InputKey.Escape);
        host.AdvanceFrames(2);

        Assert.False(host.Canvas.IsPopupWindowOpen(id));
    }

    [Fact]
    public void Dropdown_OpensBelowItsControl()
    {
        using var host = Scene(out var button, out _);
        host.Canvas.OpenPopupWindow(Menu(new List<string>(), "A", "B"), button);
        host.AdvanceFrames(3);

        var box = host.Canvas.TopPopupWindow.HitBoxAuto;
        Assert.Equal(button.HitBoxAuto.Left, box.Left, 1.5);
        Assert.Equal(button.HitBoxAuto.Bottom, box.Top, 1.5);
    }

    [Fact]
    public void ClosedFromCode()
    {
        using var host = Scene(out _, out _);
        var id = host.Canvas.OpenPopupWindow(Menu(new List<string>(), "A"), new SKPoint(10, 10));
        host.AdvanceFrames(2);

        host.Canvas.ClosePopupWindow(id);
        host.AdvanceFrames(2);
        Assert.False(host.Canvas.IsPopupWindowOpen(id));
        Assert.Null(host.Canvas.TopPopupWindow);
    }
}
