using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A markdown editor on a head with a native text control (MAUI Android, iOS, Windows): a caret the native control moves
/// into hidden markup goes on to the next drawn character in the move's direction, and the native caret follows.
/// </summary>
public class EditorNativeCaretMarkdownTests
{
    class NativeEditor : SkiaEditor
    {
        public void FromNative(int start, int length) => SetCursorFromNative(start, length);
    }

    [Fact]
    public void NativeCaretInsideMarkup_GoesToTheNextDrawnCharacter()
    {
        using var host = new HeadlessCanvasHost(400, 300, scale: 1f, background: Colors.Black);
        var editor = new NativeEditor { WidthRequest = 300, UseMarkdown = true, MaxLines = 4, Text = "**bold** text" };
        host.Canvas.Content = new SkiaStack { Children = { editor } };
        host.AdvanceFrames(4);
        editor.IsFocused = true;
        host.AdvanceFrames(2);

        void Native(int position, int expected)
        {
            editor.FromNative(position, 0);
            host.AdvanceFrames(1);
            Assert.Equal(expected, editor.CursorPosition);
        }

        editor.FromNative(6, 0); // after "bold": a drawn position, kept
        Assert.Equal(6, editor.CursorPosition);
        Native(7, 9);  // Right into the closing "**": on past the space
        Native(8, 6);  // Left from after the space: before the space (after "bold")
        Native(5, 5);  // Left: after "bol", kept
        Native(3, 3);
        Native(2, 2);  // before "b"
        Native(1, 2);  // Left into the opening "**": nothing drawn before, stays before "b"
        Native(0, 0);  // the text's start is always kept

        // typed text moves the caret with it, never snapped
        editor.Text = "**bold** text!";
        editor.FromNative(7, 0);
        Assert.Equal(7, editor.CursorPosition);
    }
}
