using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The reading (and Tab) order of a page stays the same while it scrolls: a fixed header comes first, the scrolled
/// content next in its own order, a footer last, whatever the scroll position (drawnui-cross, as DrawnUi.React).
/// Items scrolled out of view are listed too (Tab reaches them, the scroll brings them into view).
/// </summary>
public class AccessibilityReadingOrderTests
{
    private static SkiaButton Button(string text) => new()
    {
        Text = text,
        WidthRequest = 120,
        HeightRequest = 40,
        AccessibilityRole = Aria.RoleButton,
        AccessibilityLabel = text,
        AccessibilityCanInteract = true,
    };

    [Theory]
    [InlineData(0)]
    [InlineData(-160)]
    public void Order_IsHeader_Items_Footer_AtAnyScrollPosition(double offsetY)
    {
        using var host = new HeadlessCanvasHost(400, 400);
        var manager = host.Canvas.AccessibilityManager;
        manager.MinUpdateIntervalMs = 0;

        var items = new SkiaLayout { Type = LayoutType.Column, HorizontalOptions = LayoutOptions.Fill, Spacing = 0 };
        for (var i = 0; i < 12; i++)
            items.AddSubView(Button($"Item {i}"));

        var header = new SkiaLayout { Type = LayoutType.Row, HeightRequest = 40 };
        header.AddSubView(Button("Back"));
        header.AddSubView(Button("Home"));

        var scroll = new SkiaScroll { HeightRequest = 280, HorizontalOptions = LayoutOptions.Fill, Content = items };

        var root = new SkiaLayout { Type = LayoutType.Column, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, Spacing = 0 };
        root.AddSubView(header);
        root.AddSubView(scroll);
        root.AddSubView(Button("Footer"));
        host.Canvas.Content = root;
        host.AdvanceFrames(3);

        scroll.ScrollTo(0, (float)offsetY, 0, true);
        host.AdvanceFrames(3);

        // every item is listed, also the ones scrolled out of view, in page order: header, all items, footer
        var actual = manager.Snapshot.Select(n => n.Label).ToArray();
        var expected = new[] { "Back", "Home" }.Concat(Enumerable.Range(0, 12).Select(i => $"Item {i}")).Append("Footer");
        Assert.Equal(expected, actual);
    }
}
