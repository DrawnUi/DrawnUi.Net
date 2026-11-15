using AppoMobi.Gestures;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Page text selection with the mouse, as in browsers: a main-button drag that starts on no interactive control selects
/// the selectable labels it crosses (AccessibilityTextSelectable), in reading order, the first and last partly; the
/// copy joins them with new lines. Inside a scroll that holds selectable text a mouse drag selects instead of
/// scrolling, and the scroll follows the pointer past its edge; other scrolls keep mouse-drag scrolling. Touch is
/// unchanged.
/// </summary>
[Collection("Clipboard")]
public class PageTextSelectionTests
{
    private const string First = "Alpha bravo charlie";
    private const string Second = "Delta echo foxtrot";
    private const string Third = "Golf hotel india";

    private static SkiaLabel Text(string text) => new(text)
    {
        FontSize = 20,
        WidthRequest = 300,
        Margin = new Thickness(20, 10),
        AccessibilityTextSelectable = true,
    };

    private static (HeadlessCanvasHost Host, GestureRobot Robot) Host(SkiaControl content)
    {
        var host = new HeadlessCanvasHost(420, 400, scale: 1f, background: Colors.Black);
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;
        host.Canvas.Content = content;
        host.AdvanceFrames(4);
        return (host, new GestureRobot(host) { Device = PointerDeviceType.Mouse });
    }

    private static SKRect Rect(SkiaControl control) => control.GetAccessibilityPixelRect();

    /// <summary>Canvas point at the right edge of the first word of a single-line label.</summary>
    private static SKPoint AfterFirstWord(SkiaLabel label, string text)
    {
        var line = label.Lines[0];
        var glyphs = SkiaLabel.GetLineGlyphs(line);
        var space = text.IndexOf(' ');
        // line bounds are canvas pixels already (the label is not scrolled here)
        return new SKPoint(line.Bounds.Left + glyphs[space].Position, Rect(label).MidY);
    }

    [Fact]
    public void DragFromBesideALabel_SelectsFromItsStart()
    {
        SkiaLabel label = null;
        var (host, robot) = Host(new SkiaStack { Children = { Text(First).Assign(out label) } });
        using var _ = host;

        var r = Rect(label);
        var end = AfterFirstWord(label, First);
        robot.PointerDown(r.Left - 3, r.MidY);
        robot.PointerMoveTo((r.Left + end.X) / 2, r.MidY);
        robot.PointerMoveTo(end.X, end.Y);
        robot.PointerUp();

        Assert.Equal("Alpha", label.SelectedText);
    }

    [Fact]
    public void DragAcrossLabels_SelectsThemInReadingOrder_CopyJoinsWithNewLines()
    {
        SkiaLabel a = null, b = null, c = null;
        var (host, robot) = Host(new SkiaStack { Children = { Text(First).Assign(out a), Text(Second).Assign(out b), Text(Third).Assign(out c) } });
        using var _ = host;

        var top = Rect(a);
        var end = AfterFirstWord(c, Third);
        robot.PointerDown(top.Left + 10, top.Top - 6);
        robot.PointerMoveTo(top.MidX, Rect(b).MidY);
        robot.PointerMoveTo(end.X, end.Y);
        robot.PointerUp();

        Assert.Equal(First, a.SelectedText);
        Assert.Equal(Second, b.SelectedText);
        Assert.Equal("Golf", c.SelectedText);
        Assert.Equal($"{First}\n{Second}\nGolf", host.Canvas.SelectedText);

        string copied = null;
        var previous = Super.SetClipboardText;
        Super.SetClipboardText = text => copied = text;
        try
        {
            Assert.True(host.Canvas.CopyTextSelection());
        }
        finally
        {
            Super.SetClipboardText = previous;
        }
        Assert.Equal($"{First}\n{Second}\nGolf", copied);

        // a click elsewhere clears all of it
        robot.Tap(400, 390);
        host.AdvanceFrames(2);
        Assert.Equal(string.Empty, a.SelectedText);
        Assert.Equal(string.Empty, b.SelectedText);
        Assert.Equal(string.Empty, c.SelectedText);
        Assert.Equal(string.Empty, host.Canvas.SelectedText);
    }

    [Fact]
    public void TouchDrag_StillScrolls_AndSelectsNothing()
    {
        SkiaScroll scroll = null;
        var lines = Enumerable.Range(0, 30).Select(i => Text($"Line {i} of selectable text")).ToArray();
        var (host, _) = Host(new SkiaScroll { HeightRequest = 300, HorizontalOptions = LayoutOptions.Fill, Content = new SkiaStack { Children = lines } }.Assign(out scroll));
        using var __ = host;
        var robot = new GestureRobot(host) { Device = PointerDeviceType.Touch };

        robot.Pan(200, 250, 200, 100, 200, 12);
        host.AdvanceFrames(30);

        Assert.True(scroll.ViewportOffsetY < -50, $"touch drag scrolled: {scroll.ViewportOffsetY}");
        Assert.All(lines, l => Assert.Equal(string.Empty, l.SelectedText));
    }

    [Fact]
    public void ScrollWithoutSelectableText_KeepsMouseDragScrolling()
    {
        SkiaScroll scroll = null;
        var lines = Enumerable.Range(0, 30).Select(i => new SkiaLabel($"Line {i}") { FontSize = 20, Margin = new Thickness(20, 10) }).ToArray();
        var (host, robot) = Host(new SkiaScroll { HeightRequest = 300, HorizontalOptions = LayoutOptions.Fill, Content = new SkiaStack { Children = lines } }.Assign(out scroll));
        using var _ = host;

        robot.PointerDown(200, 250);
        for (var y = 240; y >= 100; y -= 10)
            robot.PointerMoveTo(200, y);
        robot.PointerUp();
        host.AdvanceFrames(30);

        Assert.True(scroll.ViewportOffsetY < -50, $"mouse drag scrolled: {scroll.ViewportOffsetY}");
    }

    [Fact]
    public void ScrollWithSelectableText_MouseDragSelects_AndFollowsPastTheEdge()
    {
        SkiaScroll scroll = null;
        var lines = Enumerable.Range(0, 30).Select(i => Text($"Line {i} of selectable text")).ToArray();
        var (host, robot) = Host(new SkiaStack
        {
            Children =
            {
                new SkiaScroll { HeightRequest = 300, HorizontalOptions = LayoutOptions.Fill, Content = new SkiaStack { Children = lines } }.Assign(out scroll),
            }
        });
        using var _ = host;

        // a drag in the gap left of the lines: selects, the scroll stays
        robot.PointerDown(8, 20);
        for (var y = 30; y <= 200; y += 10)
            robot.PointerMoveTo(200, y);
        Assert.Equal(0, scroll.ViewportOffsetY, 1);
        Assert.Equal(lines[0].Text, lines[0].SelectedText);
        Assert.NotEqual(string.Empty, lines[2].SelectedText);

        // held below the bottom edge: the scroll moves on and the selection grows with it
        robot.PointerMoveTo(200, 340);
        robot.PointerHold(60);
        Assert.True(scroll.ViewportOffsetY < -50, $"auto-scroll while held past the edge: {scroll.ViewportOffsetY}");
        Assert.NotEqual(string.Empty, lines[10].SelectedText);
        robot.PointerUp();
        host.AdvanceFrames(5);

        // and it stops once the button is up
        var offset = scroll.ViewportOffsetY;
        host.AdvanceFrames(30);
        Assert.Equal(offset, scroll.ViewportOffsetY, 1);
    }
}
