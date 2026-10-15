using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// ArtOfFoto on an iPhone (2026-10-13): a typed character was drawn first and the caret moved after it only a
/// visible moment later. The caret index came through a 50 ms timer (32 ms for the desktop stubs) and its pixels
/// were taken from label lines not laid out for the new text yet. The caret must sit after the new character in
/// the very frame that draws it, whatever order the platform reports text and selection in.
/// </summary>
public class EditorCaretFollowsTextTests
{
    /// <summary>Exposes the native-input entry point a platform head calls.</summary>
    class NativeEditor : SkiaEditor
    {
        public void FromNative(int start, int length) => SetCursorFromNative(start, length);
    }

    static (HeadlessCanvasHost host, T editor) Host<T>(T editor) where T : SkiaEditor
    {
        var host = new HeadlessCanvasHost(400, 200, scale: 2f, background: Colors.Black);
        editor.WidthRequest = 300;
        editor.HeightRequest = 44;
        editor.MaxLines = 1;
        editor.FontSize = 20;
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { editor }
        };
        host.AdvanceFrames(4);
        editor.IsFocused = true;
        host.AdvanceFrames(4);
        return (host, editor);
    }

    /// <summary>Caret x the label's lines put at <paramref name="index"/>, in points from the label's left.</summary>
    static double CaretXFromLines(SkiaEditor editor, int index)
    {
        var label = editor.Label;
        var line = label.Lines[0];
        var x = index >= line.Value.Length
            ? line.Bounds.Right
            : line.Bounds.Left + SkiaLabel.GetLineGlyphs(line)[index].Position;
        return (x - label.DrawingRect.Left) / label.RenderingScale;
    }

    [Fact]
    public void TypedCharacters_CaretAfterEachInTheSameFrame()
    {
        var (host, editor) = Host(new SkiaEditor());

        var lastX = editor.Cursor.Left;
        foreach (var expected in new[] { "5", "55", "555" })
        {
            editor.StubTypeText("5");
            host.RenderFrame(16);

            Assert.Equal(expected, editor.Label.Lines[0].Value);
            Assert.Equal(expected.Length, editor.CursorPosition);
            Assert.Equal(CaretXFromLines(editor, expected.Length), editor.Cursor.Left, 1);
            Assert.True(editor.Cursor.Left > lastX, $"caret did not move after '{expected}'");
            lastX = editor.Cursor.Left;
        }
    }

    /// <summary>iOS and Android report the new selection before the new text: typing "7" between "5" and "5".</summary>
    [Fact]
    public void NativeSelectionBeforeText_MidText_CaretAfterTypedCharacter()
    {
        var (host, editor) = Host(new NativeEditor());
        editor.Text = "55";
        editor.FromNative(1, 0);
        host.AdvanceFrames(2);
        Assert.Equal(CaretXFromLines(editor, 1), editor.Cursor.Left, 1);

        editor.FromNative(2, 0); // textViewDidChangeSelection / Android onSelectionChanged
        editor.Text = "575"; // textViewDidChange / TextWatcher.onTextChanged
        host.RenderFrame(16);

        Assert.Equal("575", editor.Label.Lines[0].Value);
        Assert.Equal(2, editor.CursorPosition);
        Assert.Equal(CaretXFromLines(editor, 2), editor.Cursor.Left, 1);
    }

    /// <summary>The IME moves the native caret without changing the text (Gboard space-bar slide, a CJK candidate).</summary>
    [Fact]
    public void NativeSelectionOnly_CaretMovesInTheNextFrame()
    {
        var (host, editor) = Host(new NativeEditor());
        editor.Text = "hello";
        editor.FromNative(5, 0);
        host.AdvanceFrames(2);

        editor.FromNative(2, 0);
        host.RenderFrame(16);

        Assert.Equal(CaretXFromLines(editor, 2), editor.Cursor.Left, 1);
    }

    [Fact]
    public void Backspace_CaretFollowsInTheSameFrame()
    {
        var (host, editor) = Host(new SkiaEditor());
        editor.StubTypeText("abc");
        host.AdvanceFrames(2);

        editor.StubBackspace();
        host.RenderFrame(16);

        Assert.Equal("ab", editor.Label.Lines[0].Value);
        Assert.Equal(CaretXFromLines(editor, 2), editor.Cursor.Left, 1);
    }

    /// <summary>Every change shows the caret and restarts its blink; idle, it still blinks.</summary>
    [Fact]
    public void Change_ShowsCaret_IdleCaretBlinks()
    {
        var (host, editor) = Host(new SkiaEditor());
        editor.StubTypeText("a");
        host.AdvanceFrames(2);

        var hidden = false;
        for (var i = 0; i < 90 && !hidden; i++) // 1.5 s
        {
            host.RenderFrame(16);
            hidden = editor.Cursor.Opacity < 0.5;
        }
        Assert.True(hidden, "idle caret never blinked off");

        editor.StubTypeText("b");
        host.RenderFrame(16);
        Assert.Equal(1.0, editor.Cursor.Opacity);
        Assert.True(editor.Cursor.IsVisible);

        for (var i = 0; i < 25; i++) // 400 ms: still the visible half of the restarted blink
        {
            host.RenderFrame(16);
            Assert.Equal(1.0, editor.Cursor.Opacity);
        }
    }
}
