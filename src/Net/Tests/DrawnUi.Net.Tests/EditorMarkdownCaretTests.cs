using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A markdown editor (UseMarkdown) draws its text without the markup ("# ", "**", "[...](...)"), so a text index and a
/// drawn character differ. Caret placement, taps, arrows and the selection map between them: the caret sits at the
/// drawn character of its text index and never stops inside hidden markup.
/// </summary>
public class EditorMarkdownCaretTests
{
    class Editor : SkiaEditor
    {
        public int TapIndex(float x, float y) => GetCursorPosition(x, y);
        public void Vertical(bool up) => HandleVerticalArrow(up);
        public SkiaEditorSelection Selection => _selectionControl;
    }

    static (HeadlessCanvasHost Host, Editor Editor) Create(string text)
    {
        var host = new HeadlessCanvasHost(500, 300, scale: 2f, background: Colors.Black);
        var editor = new Editor { WidthRequest = 220, FontSize = 14, MaxLines = 6, UseMarkdown = true, Text = text };
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
            Children = { editor }
        };
        host.AdvanceFrames(4);
        editor.IsFocused = true;
        host.AdvanceFrames(4);
        return (host, editor);
    }

    static void Place(HeadlessCanvasHost host, Editor editor, int position)
    {
        editor.SelectionLength = 0;
        editor.CursorPosition = position;
        host.AdvanceFrames(3);
    }

    /// <summary>Caret left and top (points) for the drawn character <paramref name="drawn"/> of line <paramref name="line"/>.</summary>
    static (double X, double Y) DrawnCaret(SkiaEditor editor, int line, int drawn)
    {
        var label = editor.Label;
        var l = label.Lines[line];
        var glyphs = SkiaLabel.GetLineGlyphs(l);
        var x = drawn >= glyphs.Length ? l.Bounds.Right : l.Bounds.Left + glyphs[drawn].Position;
        return ((x - label.DrawingRect.Left) / label.RenderingScale, (l.Bounds.Top - label.DrawingRect.Top) / label.RenderingScale);
    }

    static void AssertCaretAt(Editor editor, int line, int drawn)
    {
        var (x, y) = DrawnCaret(editor, line, drawn);
        Assert.Equal(y, editor.Cursor.Top, 1);
        Assert.Equal(x, editor.Cursor.Left, 1);
    }

    [Fact]
    public void Heading_CaretTapAndVerticalArrow()
    {
        var (host, editor) = Create("# Big\nsmall");
        using var _ = host;
        Assert.Equal(new[] { "Big", "small" }, editor.Label.Lines.Select(l => l.Value.TrimEnd()).ToArray());

        Place(host, editor, editor.Text.Length);
        AssertCaretAt(editor, 1, 5);
        Assert.Equal(1, editor.GetCursorLine());

        Place(host, editor, 2); // after "# ": before "B"
        AssertCaretAt(editor, 0, 0);
        Place(host, editor, 5); // after "Big"
        AssertCaretAt(editor, 0, 3);
        Place(host, editor, 6); // before "small"
        AssertCaretAt(editor, 1, 0);

        // taps: past the end of "small", at the start of "small", on "B"
        var second = editor.Label.Lines[1].Bounds;
        Assert.Equal(editor.Text.Length, editor.TapIndex(second.Right + 4, second.MidY));
        Assert.Equal(6, editor.TapIndex(second.Left, second.MidY));
        var first = editor.Label.Lines[0].Bounds;
        Assert.Equal(2, editor.TapIndex(first.Left, first.MidY));

        // up from the end of "small" lands inside "Big"
        Place(host, editor, editor.Text.Length);
        editor.Vertical(true);
        host.AdvanceFrames(3);
        Assert.InRange(editor.CursorPosition, 2, 5);
        Assert.Equal(0, editor.GetCursorLine());
    }

    [Fact]
    public void Bold_ArrowsSkipMarkupAndSelectionIsDrawn()
    {
        var (host, editor) = Create("**bold** text");
        using var _ = host;
        Assert.Equal("bold text", editor.Label.Lines[0].Value.TrimEnd());

        Place(host, editor, editor.Text.Length);
        AssertCaretAt(editor, 0, 9);

        // every Right press moves the caret by one drawn character
        Place(host, editor, 0);
        var stops = new List<double> { editor.Cursor.Left };
        for (var i = 0; i < 9; i++)
        {
            editor.StubArrow(true);
            host.AdvanceFrames(2);
            Assert.True(editor.Cursor.Left > stops[^1], $"press {i + 1}: caret did not move ({editor.CursorPosition})");
            stops.Add(editor.Cursor.Left);
        }
        Assert.Equal(editor.Text.Length, editor.CursorPosition);
        for (var i = 0; i < 9; i++)
        {
            editor.StubArrow(false);
            host.AdvanceFrames(2);
            Assert.Equal(stops[8 - i], editor.Cursor.Left, 1);
        }

        // selecting "bold" in the text selects the 4 drawn letters
        editor.StubSelectRange(2, 4);
        host.AdvanceFrames(3);
        Assert.Equal(0, editor.Selection.SelectionStart);
        Assert.Equal(4, editor.Selection.SelectionLength);
    }

    [Fact]
    public void Link_CaretAndTap()
    {
        var (host, editor) = Create("[link](http://x.com) tail");
        using var _ = host;
        Assert.Equal("link tail", editor.Label.Lines[0].Value.TrimEnd());

        Place(host, editor, editor.Text.Length);
        AssertCaretAt(editor, 0, 9);
        Place(host, editor, 5); // after "link"
        AssertCaretAt(editor, 0, 4);
        Place(host, editor, 21); // before "tail"
        AssertCaretAt(editor, 0, 5);

        var line = editor.Label.Lines[0].Bounds;
        Assert.Equal(editor.Text.Length, editor.TapIndex(line.Right + 4, line.MidY));
    }
}
