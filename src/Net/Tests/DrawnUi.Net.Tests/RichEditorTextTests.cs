#if DEBUG // SkiaRichEditor is built in Debug only
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A rich editor's label takes its spans from the editor's document. The label's own text parsing (SkiaRichLabel
/// markdown) must not clear them when it measures: text set before the first frame, or kept across a font change,
/// stays on screen.
/// </summary>
public class RichEditorTextTests
{
    static HeadlessCanvasHost Host(SkiaControl content)
    {
        var host = new HeadlessCanvasHost(400, 300, scale: 1f, background: Colors.Black);
        host.Canvas.Content = new SkiaStack { HorizontalOptions = LayoutOptions.Fill, Children = { content } };
        return host;
    }

    static void AssertShows(SkiaRichEditor editor, string text)
    {
        var label = editor.Label;
        Assert.True(label.Spans.Count > 0, "label has no spans");
        Assert.Equal(text, string.Concat(label.Spans.Select(s => s.Text)));
        Assert.Equal(text.Split('\n').Length, label.Lines?.Length ?? 0);
        Assert.True(label.DrawingRect.Height > 0, "label not drawn");
    }

    [Fact]
    public void TextSetBeforeFirstFrame_IsShown()
    {
        var editor = new SkiaRichEditor { WidthRequest = 200, FontSize = 14, MaxLines = 6, Text = "Big\nsmall", TextColor = Colors.White };
        using var host = Host(editor);
        host.AdvanceFrames(4);
        AssertShows(editor, "Big\nsmall");
    }

    [Fact]
    public void DocumentText_SurvivesFontChange()
    {
        var editor = new SkiaRichEditor { WidthRequest = 200, FontSize = 14, MaxLines = 6, TextColor = Colors.White };
        using var host = Host(editor);
        host.AdvanceFrames(4);
        editor.Document.SetText("one\ntwo");
        host.AdvanceFrames(4);
        AssertShows(editor, "one\ntwo");

        editor.FontSize = 20;
        host.AdvanceFrames(4);
        AssertShows(editor, "one\ntwo");
    }
}
#endif
