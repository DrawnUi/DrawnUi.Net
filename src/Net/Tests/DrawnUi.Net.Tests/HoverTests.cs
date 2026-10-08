using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Hover (drawnui-cross 6m): every control under the mouse that takes hover is hovered, a card and the button
/// inside it alike; only opted-in controls hover; nothing changes while content animates under the pointer, one check
/// follows when it stops; leaving the canvas ends hover at once.
/// </summary>
public class HoverTests
{
    const int W = 400, H = 400;

    static void MoveTo(HeadlessCanvasHost host, float x, float y)
    {
        host.Canvas.HandleDesktopPointerMove(x, y, false, W, H);
        host.RenderFrame();
    }

    [Fact]
    public void CardAndButtonInside_BothHovered()
    {
        using var host = new HeadlessCanvasHost(W, H);
        var cardEvents = new List<bool>();
        SkiaShape card = null;
        SkiaButton button = null;
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl>
            {
                new SkiaShape
                {
                    WidthRequest = 300, HeightRequest = 200, BackgroundColor = Colors.Gray,
                    Children = new List<SkiaControl>
                    {
                        new SkiaButton("×") { WidthRequest = 60, HeightRequest = 40, Margin = new Thickness(220, 10, 0, 0) }
                            .Assign(out button),
                    },
                }.Assign(out card).OnHovered((me, on) => cardEvents.Add(on)),
            },
        };
        host.AdvanceFrames(3);

        MoveTo(host, 50, 150); // card only
        Assert.True(card.IsHovered);
        Assert.False(button.IsHovered);

        MoveTo(host, 250, 30); // the button inside the card
        Assert.True(card.IsHovered);
        Assert.True(button.IsHovered);
        Assert.Equal(new[] { true }, cardEvents); // the card did not flicker

        MoveTo(host, 380, 380); // outside
        Assert.False(card.IsHovered);
        Assert.False(button.IsHovered);
        Assert.Equal(new[] { true, false }, cardEvents);
    }

    [Fact]
    public void OnlyOptedInControlsHover()
    {
        using var host = new HeadlessCanvasHost(W, H);
        SkiaShape tappable = null;
        SkiaButton button = null;
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl>
            {
                new SkiaShape { WidthRequest = 150, HeightRequest = 150, BackgroundColor = Colors.Gray }
                    .Assign(out tappable).OnTapped(me => { }),
                new SkiaButton("Off") { ReceivesHover = false, WidthRequest = 150, HeightRequest = 60, Margin = new Thickness(200, 0, 0, 0) }
                    .Assign(out button),
            },
        };
        host.AdvanceFrames(3);

        MoveTo(host, 50, 50);
        Assert.False(tappable.IsHovered); // a tap handler does not make a control hover
        MoveTo(host, 250, 20);
        Assert.False(button.IsHovered); // hover turned off on a control that has it by default
        Assert.True(new SkiaButton().ReceivesHover);
        Assert.False(new SkiaShape().ReceivesHover);
    }

    [Fact]
    public void ScrollAnimation_HoverWaitsForTheEnd()
    {
        using var host = new HeadlessCanvasHost(W, H);
        var cards = new List<SkiaShape>();
        var changes = 0;
        var scroll = new SkiaScroll
        {
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Spacing = 0,
                Children = Enumerable.Range(0, 30).Select(i => (SkiaControl)new SkiaShape
                {
                    HeightRequest = 50,
                    HorizontalOptions = LayoutOptions.Fill,
                    BackgroundColor = i % 2 == 0 ? Colors.Gray : Colors.DarkGray,
                }.Adapt(me => cards.Add(me)).OnHovered((me, on) => changes++)).ToList(),
            },
        };
        host.Canvas.Content = scroll;
        host.AdvanceFrames(3);

        MoveTo(host, 100, 75); // card 1
        Assert.True(cards[1].IsHovered);
        changes = 0;

        scroll.ScrollTo(0, -500, 0.5f, true); // animated, 10 cards down
        for (var i = 0; i < 6 && scroll.IsScrolling == false; i++)
            host.RenderFrame();
        Assert.True(scroll.IsScrolling);

        // the mouse moves while the content glides: hover is not tracked
        for (var i = 0; i < 5; i++)
            MoveTo(host, 100, 75 + i);
        Assert.True(cards[1].IsHovered);
        Assert.Equal(0, changes);

        for (var i = 0; i < 120 && scroll.IsScrolling; i++)
            host.RenderFrame();
        Assert.False(scroll.IsScrolling);
        host.AdvanceFrames(3); // the check at the last pointer position

        Assert.False(cards[1].IsHovered);
        Assert.Single(cards, c => c.IsHovered);
        Assert.True(cards[11].IsHovered); // y 79 + 500 scrolled = card 11
    }

    [Fact]
    public void Leave_ClearsHover_EvenWhileContentAnimates()
    {
        using var host = new HeadlessCanvasHost(W, H);
        SkiaShape card = null;
        var scroll = new SkiaScroll
        {
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Children = Enumerable.Range(0, 30).Select(i => (SkiaControl)new SkiaShape
                {
                    HeightRequest = 50,
                    HorizontalOptions = LayoutOptions.Fill,
                    BackgroundColor = Colors.Gray,
                    ReceivesHover = true,
                }).ToList(),
            },
        };
        host.Canvas.Content = scroll;
        host.AdvanceFrames(3);

        MoveTo(host, 100, 25);
        card = (SkiaShape)host.Canvas.HoveredControls.Single();

        scroll.ScrollTo(0, -500, 0.5f, true);
        host.AdvanceFrames(3);
        Assert.True(scroll.IsScrolling);

        host.Canvas.HandleDesktopPointerLeave();
        host.RenderFrame();
        Assert.False(card.IsHovered);
        Assert.Empty(host.Canvas.HoveredControls);

        for (var i = 0; i < 120 && scroll.IsScrolling; i++)
            host.RenderFrame();
        host.AdvanceFrames(3);
        Assert.Empty(host.Canvas.HoveredControls); // no check after the end: the mouse is gone
    }
}
