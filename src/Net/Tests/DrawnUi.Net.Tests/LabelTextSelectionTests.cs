using System.Drawing;
using AppoMobi.Gestures;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaLabel.AccessibilityTextSelectable: mouse drag and double click select, touch long press selects a word, the
/// selection copies through Super.SetClipboardText (Ctrl+C or CopySelection), a click elsewhere drops it, and a label
/// without the property ignores all of it.
/// </summary>
[Collection("Clipboard")]
public class LabelTextSelectionTests
{
    private const string Text = "Hello wonderful world, this text wraps onto a second line here";

    private static (HeadlessCanvasHost Host, SkiaLabel Label, GestureRobot Robot) Create(bool selectable, PointerDeviceType device)
    {
        var host = new HeadlessCanvasHost(420, 300, scale: 1f, background: Colors.Black);
        var label = new SkiaLabel
        {
            Text = Text,
            FontSize = 20,
            WidthRequest = 300,
            Margin = new Thickness(20),
            AccessibilityTextSelectable = selectable,
        };
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children = { label },
        };
        host.AdvanceFrames(3);
        return (host, label, new GestureRobot(host) { Device = device });
    }

    /// <summary>Canvas point (pixels at scale 1) at the left edge of a character, mid-height of its line.</summary>
    private static PointF At(SkiaLabel label, int index)
    {
        var lines = label.Lines;
        var start = 0;
        foreach (var line in lines)
        {
            var at = Text.IndexOf(line.Value, start, StringComparison.Ordinal);
            if (index <= at + line.Value.Length)
            {
                var glyphs = SkiaLabel.GetLineGlyphs(line);
                var slot = index - at;
                var x = slot < glyphs.Length ? glyphs[slot].Position : line.Width;
                return new PointF(line.Bounds.Left + x + 1, line.Bounds.MidY);
            }
            start = at + line.Value.Length;
        }
        throw new ArgumentOutOfRangeException(nameof(index));
    }

    [Fact]
    public void MouseDrag_SelectsTheDraggedRange()
    {
        var (host, label, robot) = Create(true, PointerDeviceType.Mouse);
        using var _ = host;
        var from = At(label, 6);
        var to = At(label, 15);

        robot.PointerDown(from.X, from.Y);
        robot.PointerMoveTo((from.X + to.X) / 2, to.Y);
        robot.PointerMoveTo(to.X, to.Y);
        robot.PointerUp();

        Assert.Equal("wonderful", label.SelectedText);
    }

    [Fact]
    public void MouseDrag_AcrossTheWrap_CopiesTheSpaceItDropped()
    {
        var (host, label, robot) = Create(true, PointerDeviceType.Mouse);
        using var _ = host;
        Assert.True(label.Lines.Length >= 2);
        var secondLineStart = Text.IndexOf(label.Lines[1].Value, StringComparison.Ordinal);
        var from = At(label, secondLineStart - 6);
        var to = At(label, secondLineStart + 4);

        robot.PointerDown(from.X, from.Y);
        robot.PointerMoveTo(to.X, to.Y);
        robot.PointerUp();

        Assert.Equal(Text.Substring(secondLineStart - 6, 10), label.SelectedText);
    }

    [Fact]
    public void DoubleClick_SelectsTheWord()
    {
        var (host, label, robot) = Create(true, PointerDeviceType.Mouse);
        using var _ = host;
        var inside = At(label, 9);

        robot.Tap(inside.X, inside.Y);
        robot.Tap(inside.X, inside.Y);

        Assert.Equal("wonderful", label.SelectedText);
    }

    [Fact]
    public void CtrlC_CopiesThroughSuperSetClipboardText()
    {
        var previous = Super.SetClipboardText;
        string copied = null;
        Super.SetClipboardText = text => copied = text;
        try
        {
            var (host, label, robot) = Create(true, PointerDeviceType.Mouse);
            using var _ = host;
            var inside = At(label, 2);
            robot.Tap(inside.X, inside.Y);
            robot.Tap(inside.X, inside.Y);

            KeyboardManager.KeyboardPressed(InputKey.ControlLeft);
            KeyboardManager.KeyboardPressed(InputKey.KeyC);
            KeyboardManager.KeyboardReleased(InputKey.KeyC);
            KeyboardManager.KeyboardReleased(InputKey.ControlLeft);

            Assert.Equal("Hello", copied);
        }
        finally
        {
            Super.SetClipboardText = previous;
        }
    }

    /// <summary>
    /// Ctrl+C typed into a field outside DrawnUI (a page input, a native text box) is that field's copy: the drawn
    /// selection must not overwrite the clipboard (GitHub #231 class). The key is still reported, flagged.
    /// </summary>
    [Fact]
    public void CtrlC_ForAnotherElement_DoesNotCopy()
    {
        var previous = Super.SetClipboardText;
        string copied = null;
        Super.SetClipboardText = text => copied = text;
        var flags = new List<bool>();
        EventHandler<InputKey> watch = (s, k) => flags.Add(KeyboardManager.IsKeyForOtherElement);
        KeyboardManager.KeyDown += watch;
        try
        {
            var (host, label, robot) = Create(true, PointerDeviceType.Mouse);
            using var _ = host;
            var inside = At(label, 2);
            robot.Tap(inside.X, inside.Y);
            robot.Tap(inside.X, inside.Y);

            KeyboardManager.KeyboardPressed(InputKey.ControlLeft, true);
            KeyboardManager.KeyboardPressed(InputKey.KeyC, true);
            KeyboardManager.KeyboardReleased(InputKey.KeyC, true);
            KeyboardManager.KeyboardReleased(InputKey.ControlLeft, true);

            Assert.Null(copied);
            Assert.Equal(new[] { true, true }, flags);
            Assert.False(KeyboardManager.IsKeyForOtherElement);
        }
        finally
        {
            KeyboardManager.KeyDown -= watch;
            Super.SetClipboardText = previous;
        }
    }

    [Fact]
    public void ClickOnEmptySpace_DropsTheSelection()
    {
        var (host, label, robot) = Create(true, PointerDeviceType.Mouse);
        using var _ = host;
        var inside = At(label, 9);
        robot.Tap(inside.X, inside.Y);
        robot.Tap(inside.X, inside.Y);
        Assert.Equal("wonderful", label.SelectedText);

        robot.Tap(400, 280);

        Assert.Equal(string.Empty, label.SelectedText);
    }

    [Fact]
    public void TouchLongPress_SelectsTheWord_TouchDragDoesNot()
    {
        var (host, label, robot) = Create(true, PointerDeviceType.Touch);
        using var _ = host;
        var from = At(label, 6);
        var to = At(label, 15);

        robot.PointerDown(from.X, from.Y);
        robot.PointerMoveTo(to.X, to.Y);
        robot.PointerUp();
        Assert.Equal(string.Empty, label.SelectedText);

        var press = At(label, 9);
        var args = new TouchActionEventArgs(1, TouchActionType.Pressed, press, null, 1f)
        {
            StartingLocation = press,
            NumberOfTouches = 1,
            IsInsideView = true,
            Pointer = new PointerData { DeviceType = PointerDeviceType.Touch },
        };
        host.Canvas.OnGestureEvent(TouchActionType.Pressed, args, TouchActionResult.LongPressing);
        host.RenderFrame();

        Assert.Equal("wonderful", label.SelectedText);
    }

    [Fact]
    public void NotSelectable_IgnoresPointer()
    {
        var (host, label, robot) = Create(false, PointerDeviceType.Mouse);
        using var _ = host;
        var from = At(label, 6);
        var to = At(label, 15);

        robot.PointerDown(from.X, from.Y);
        robot.PointerMoveTo(to.X, to.Y);
        robot.PointerUp();

        Assert.Equal(string.Empty, label.SelectedText);
    }
}
