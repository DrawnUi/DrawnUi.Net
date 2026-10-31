using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The editor caret in right-to-left text, the rules DrawnUi.Rust and DrawnUi.React follow: the caret before character
/// i sits at the trailing edge of character i-1 (at a line start at the leading edge of the first character); a tap
/// goes to the grapheme boundary whose caret is nearest; Left / Right move on screen; a selection covers the boxes of
/// its characters. Lines: pure Arabic, pure Hebrew, and "Hi مرحبا 123" (left to right, its digits with the Arabic).
/// </summary>
[Collection("FontRegistration")] // registers fonts: never in parallel with another class that does
public class EditorRtlCaretTests
{
    const string Arabic = "مرحبا";
    const string Hebrew = "שלום";
    const string Mixed = "Hi مرحبا 123";

    /// <summary>Exposes the tap mapping (pixels, the lines' space).</summary>
    class Editor : SkiaEditor
    {
        public int IndexAt(float x, float y) => GetCursorPosition(x, y);
    }

    static (HeadlessCanvasHost host, Editor editor) Host(string text)
    {
        var host = new HeadlessCanvasHost(500, 200, scale: 1f, background: Colors.Black);
        var editor = new Editor { WidthRequest = 400, HeightRequest = 44, MaxLines = 1, FontSize = 20, Text = text };
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

    static TextLine Line(SkiaEditor editor) => Assert.Single(editor.Label.Lines);

    /// <summary>Caret x of every editor index (the label adds a trailing space to the last line, no caret stop).</summary>
    static float[] Carets(SkiaEditor editor) => Enumerable.Range(0, editor.Text.Length + 1).Select(Line(editor).CaretX).ToArray();

    [Theory]
    [InlineData(Arabic)]
    [InlineData(Hebrew)]
    public void RightToLeftLine_CaretRunsFromRightToLeft(string text)
    {
        var (_, editor) = Host(text);
        var line = Line(editor);
        var x = Carets(editor);

        Assert.NotNull(line.Carets);
        Assert.Equal(line.Width, x[0], 0.5);
        Assert.Equal(line.Carets[text.Length - 1].Left, x[^1], 0.01); // the left edge of the last letter
        Assert.Equal(x.Min(), x[^1]);
        for (var k = 1; k < x.Length; k++)
            Assert.True(x[k] <= x[k - 1] + 0.01f, $"index {k} at {x[k]} is right of {x[k - 1]}");
    }

    [Fact]
    public void MixedLine_CaretAtTrailingEdges()
    {
        var (_, editor) = Host(Mixed);
        var line = Line(editor);
        var x = Carets(editor);

        Assert.Equal(13, x.Length);
        Assert.Equal(0, x[0], 0.5);
        Assert.True(x[1] > x[0] && x[2] > x[1] && x[3] > x[2], "\"Hi \" runs left to right");
        Assert.True(x[4] > x[3] && x[4] < line.Width - 0.5f, $"after م at {x[4]}, line {line.Width}"); // left edge of م
        for (var k = 5; k <= 8; k++)
            Assert.True(x[k] < x[k - 1], $"index {k} at {x[k]} not left of {x[k - 1]}");
        Assert.True(x[11] > x[10] && x[12] > x[11], "the digits run left to right");
        Assert.True(x[12] < x[8], "the end of the text is left of the Arabic's end");
        Assert.True(x[3] < x[10], "\"Hi \" is left of the digits");
    }

    [Theory]
    [InlineData(Arabic)]
    [InlineData(Hebrew)]
    [InlineData(Mixed)]
    public void Tap_FindsTheIndexOnScreen(string text)
    {
        var (_, editor) = Host(text);
        var line = Line(editor);
        var x = Carets(editor);

        for (var k = 0; k < x.Length; k++)
        {
            var got = editor.IndexAt(line.Bounds.Left + x[k], line.Bounds.MidY);
            Assert.True(Math.Abs(x[got] - x[k]) < 0.5f, $"a tap on index {k} at {x[k]} gave {got} at {x[got]}");
        }
    }

    [Fact]
    public void MultilineEditor_ArabicLineAtTheRight_CaretAndTapsFollow()
    {
        var host = new HeadlessCanvasHost(500, 300, scale: 1f, background: Colors.Black);
        var editor = new Editor { WidthRequest = 400, HeightRequest = 120, MaxLines = -1, FontSize = 20, Text = Arabic };
        host.Canvas.Content = new SkiaLayout { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, Children = { editor } };
        host.AdvanceFrames(4);
        editor.IsFocused = true;
        editor.CursorPosition = 0;
        host.AdvanceFrames(4);

        var line = Line(editor);
        Assert.True(line.Bounds.Left > editor.Label.DrawingRect.Left + 100, $"line at {line.Bounds.Left}, label from {editor.Label.DrawingRect.Left}");
        var expected = (line.Bounds.Left + line.CaretX(0) - editor.Label.DrawingRect.Left) / editor.RenderingScale - editor.Cursor.WidthRequest;
        Assert.Equal(expected, editor.Cursor.Left, 1);
        var x = Carets(editor);
        for (var k = 0; k < x.Length; k++)
            Assert.Equal(k, editor.IndexAt(line.Bounds.Left + x[k], line.Bounds.MidY));
    }

    [Fact]
    public void Tap_RightHalfOfFirstArabicLetter_IsIndexZero()
    {
        var (_, editor) = Host(Arabic);
        var line = Line(editor);
        var x = Carets(editor);

        Assert.Equal(0, editor.IndexAt(line.Bounds.Left + x[0] - (x[0] - x[1]) * 0.25f, line.Bounds.MidY));
        Assert.Equal(1, editor.IndexAt(line.Bounds.Left + x[0] - (x[0] - x[1]) * 0.75f, line.Bounds.MidY));
    }

    [Fact]
    public void TypingArabic_CaretStaysAfterTheNewLetter()
    {
        var (host, editor) = Host(Arabic);
        editor.CursorPosition = Arabic.Length;
        editor.StubTypeText("ب");
        host.AdvanceFrames(3);

        Assert.Equal(Arabic + "ب", editor.Text);
        Assert.Equal(6, editor.CursorPosition);
        var line = Line(editor);
        // a right-to-left caret is drawn to the left of its x
        var expected = (line.Bounds.Left + line.CaretX(6) - editor.Label.DrawingRect.Left) / editor.RenderingScale - editor.Cursor.WidthRequest;
        Assert.Equal(expected, editor.Cursor.Left, 1);
    }

    [Fact]
    public void Arrows_ArabicMoveOnScreen()
    {
        var (_, editor) = Host(Arabic);
        editor.CursorPosition = 0;

        editor.StubArrow(right: false);
        Assert.Equal(1, editor.CursorPosition); // Left goes on in the text
        editor.StubArrow(right: true);
        Assert.Equal(0, editor.CursorPosition);
        editor.StubArrow(right: true);
        Assert.Equal(0, editor.CursorPosition); // the visual edge of a single line: nothing
    }

    [Fact]
    public void Arrows_MixedLineRightWalksToTheRightmostCaret()
    {
        var (_, editor) = Host(Mixed);
        var x = Carets(editor);
        editor.CursorPosition = 0;

        var at = x[0];
        for (var step = 0; step < 12; step++)
        {
            var before = editor.CursorPosition;
            editor.StubArrow(right: true);
            if (editor.CursorPosition == before)
                break;
            Assert.True(x[editor.CursorPosition] > at - 0.01f, $"step {step}: Right went from {before} at {at} to {editor.CursorPosition}");
            at = x[editor.CursorPosition];
        }

        Assert.Equal(x.Max(), at, 0.01);
        Assert.Equal(4, editor.CursorPosition); // the left edge of م; a logical step would jump back
        editor.StubArrow(right: true);
        Assert.Equal(4, editor.CursorPosition);
    }

    [Fact]
    public void Selection_AcrossDirectionChange_TwoBlocks()
    {
        var (_, editor) = Host(Mixed);

        Assert.Equal(2, Line(editor).Boxes(0, 4).Count); // "Hi " and م at the far right
        Assert.Single(Line(editor).Boxes(3, 8)); // the whole Arabic word
    }

    /// <summary>
    /// The same caret places as DrawnUi.Rust and DrawnUi.React (x minus the line's left for every index, FontSize 20,
    /// the Noto Sans Arabic / Hebrew subsets).
    /// </summary>
    [Theory]
    [InlineData(Arabic, "RtlTestArabic", new[] { 42.08f, 31.58f, 24.46f, 12.68f, 5.82f, 0f })]
    [InlineData(Hebrew, "RtlTestHebrew", new[] { 44.54f, 29.94f, 19.5f, 13.68f, 0f })]
    public void CaretX_SameAsOtherEngines(string text, string font, float[] expected)
    {
        RegisterFonts();
        var host = new HeadlessCanvasHost(600, 200, scale: 1f, background: Colors.Black);
        var editor = new Editor
        {
            WidthRequest = 560, HeightRequest = 44, MaxLines = 1, FontSize = 20, Text = text,
            FontFamily = font, // the engines draw these letters with the fallback font: same advances
        };
        host.Canvas.Content = new SkiaLayout { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, Children = { editor } };
        host.AdvanceFrames(4);

        var x = Carets(editor);
        var min = x.Min(); // the label adds a trailing space at the line's left
        Assert.Equal(expected.Length, x.Length);
        for (var i = 0; i < x.Length; i++)
            Assert.True(Math.Abs(x[i] - min - expected[i]) < 0.5f, $"index {i}: {x[i] - min:0.00}, other engines {expected[i]}; all {string.Join(", ", x.Select(v => (v - min).ToString("0.00")))}");
    }

    static void RegisterFonts()
    {
        lock (typeof(EditorRtlCaretTests))
        {
            if (SkiaFontManager.Instance.GetFont("RtlTestArabic") is { } face && face.FamilyName.Contains("Arabic"))
                return;
            var dir = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts");
            SkiaFontManager.Instance.RegisterFont("RtlTestArabic", Path.Combine(dir, "NotoSansArabic-Subset.ttf"));
            SkiaFontManager.Instance.RegisterFont("RtlTestHebrew", Path.Combine(dir, "NotoSansHebrew-Subset.ttf"));
            SkiaFontManager.Instance.Initialize();
        }
    }

    [Fact]
    public void LeftToRightText_KeepsGlyphCarets()
    {
        var (_, editor) = Host("Hello 123");

        Assert.Null(Line(editor).Carets);
    }
}
