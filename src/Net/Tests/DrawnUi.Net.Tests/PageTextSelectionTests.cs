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

    private static SkiaLabel Narrow(string text) => new(text)
    {
        FontSize = 20,
        WidthRequest = 160,
        Margin = new Thickness(10, 10),
        AccessibilityTextSelectable = true,
    };

    /// <summary>
    /// Two columns, the left one longer: a drag that starts beside a label of the right column selects the right column,
    /// never a left-column label that sits lower on the page.
    /// </summary>
    [Fact]
    public void TwoColumns_DragBesideTheRightColumn_SelectsIt()
    {
        SkiaLabel b1 = null, b2 = null;
        var left = new[] { Narrow("Left one"), Narrow("Left two"), Narrow("Left three"), Narrow("Left four") };
        var (host, robot) = Host(new SkiaRow
        {
            Spacing = 40,
            Children =
            {
                new SkiaStack { WidthRequest = 180, Children = left },
                new SkiaStack { WidthRequest = 180, Children = { Narrow("Right one").Assign(out b1), Narrow("Right two").Assign(out b2) } },
            }
        });
        using var _ = host;

        var start = Rect(b1);
        var end = Rect(b2);
        robot.PointerDown(start.Left - 4, start.MidY);
        robot.PointerMoveTo(start.MidX, end.Top);
        robot.PointerMoveTo(end.Right - 2, end.MidY);
        robot.PointerUp();

        Assert.Equal("Right one", b1.SelectedText);
        Assert.NotEqual(string.Empty, b2.SelectedText);
        Assert.All(left, l => Assert.Equal(string.Empty, l.SelectedText));
    }

    /// <summary>A page drag over one label: Ctrl+C copies it, with no label in focus.</summary>
    [Fact]
    public void OneLabel_PageDrag_CtrlCCopies()
    {
        SkiaLabel label = null;
        var (host, robot) = Host(new SkiaStack { Children = { Text(First).Assign(out label) } });
        using var _ = host;
        var r = Rect(label);
        var end = AfterFirstWord(label, First);
        robot.PointerDown(r.Left - 3, r.MidY);
        robot.PointerMoveTo(end.X, end.Y);
        robot.PointerUp();
        Assert.Equal("Alpha", label.SelectedText);

        string copied = null;
        var previous = Super.SetClipboardText;
        Super.SetClipboardText = text => copied = text;
        try
        {
            KeyboardManager.KeyboardPressed(InputKey.ControlLeft);
            KeyboardManager.KeyboardPressed(InputKey.KeyC);
            KeyboardManager.KeyboardReleased(InputKey.KeyC);
            KeyboardManager.KeyboardReleased(InputKey.ControlLeft);
        }
        finally
        {
            Super.SetClipboardText = previous;
        }
        Assert.Equal("Alpha", copied);
    }

    /// <summary>The app's Copy menu drawn on the canvas: the press on its item keeps the page selection for the copy.</summary>
    [Fact]
    public void PressInAnOpenPopup_KeepsTheSelection_ForItsCopy()
    {
        SkiaLabel a = null, b = null;
        var (host, robot) = Host(new SkiaStack { Children = { Text(First).Assign(out a), Text(Second).Assign(out b) } });
        using var _ = host;
        var top = Rect(a);
        var bottom = Rect(b);
        robot.PointerDown(top.Left - 3, top.MidY);
        robot.PointerMoveTo(bottom.Right - 2, bottom.MidY);
        robot.PointerUp();
        Assert.Equal($"{First}\n{Second}", host.Canvas.SelectedText);

        string copied = null;
        host.Canvas.OpenPopupWindow(new SkiaLayer
        {
            WidthRequest = 120,
            HeightRequest = 40,
            BackgroundColor = Colors.DarkGray,
            Children = { new SkiaLabel("Copy") },
        }.OnTapped(_ => copied = host.Canvas.SelectedText), new SKPoint(200, 250));
        host.AdvanceFrames(3);

        robot.Tap(230, 265);
        host.AdvanceFrames(2);
        Assert.Equal($"{First}\n{Second}", copied);
    }

    /// <summary>The drawn Copy button of a touch selection copies whether the tap or the release comes first (Blazor: the tap).</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CopyButton_Copies_WhicheverOfTapAndUpComesFirst(bool tapFirst)
    {
        SkiaLabel label = null;
        var (host, _) = Host(new SkiaStack { Children = { Text(First).Assign(out label) } });
        using var __ = host;
        var word = AfterFirstWord(label, First);
        var press = new System.Drawing.PointF(word.X - 10, word.Y);
        TouchActionEventArgs Touch(long id, System.Drawing.PointF at) => new(id, TouchActionType.Pressed, at, null, 1f)
        {
            StartingLocation = at,
            NumberOfTouches = 1,
            IsInsideView = true,
            Pointer = new PointerData { DeviceType = PointerDeviceType.Touch },
        };
        host.Canvas.OnGestureEvent(TouchActionType.Pressed, Touch(1, press), TouchActionResult.LongPressing);
        host.AdvanceFrames(2);
        Assert.Equal("Alpha", label.SelectedText);

        var button = (SKRect)typeof(SkiaLabel).GetField("_copyButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(label)!;
        Assert.False(button.IsEmpty, "the Copy button shows");
        var at = new System.Drawing.PointF(button.MidX, button.MidY);

        string copied = null;
        var previous = Super.SetClipboardText;
        Super.SetClipboardText = text => copied = text;
        try
        {
            host.Canvas.OnGestureEvent(TouchActionType.Pressed, Touch(2, at), TouchActionResult.Down);
            if (tapFirst)
                host.Canvas.OnGestureEvent(TouchActionType.Released, Touch(2, at), TouchActionResult.Tapped);
            host.Canvas.OnGestureEvent(TouchActionType.Released, Touch(2, at), TouchActionResult.Up);
            if (!tapFirst)
                host.Canvas.OnGestureEvent(TouchActionType.Released, Touch(2, at), TouchActionResult.Tapped);
            host.AdvanceFrames(2);
        }
        finally
        {
            Super.SetClipboardText = previous;
        }
        Assert.Equal("Alpha", copied);
    }
}
