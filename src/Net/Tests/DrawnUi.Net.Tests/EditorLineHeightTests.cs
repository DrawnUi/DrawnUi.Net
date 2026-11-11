using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Each line of an editor is as tall as its own content (SkiaLabel default): an editor whose label draws a 40 pt line
/// over a 14 pt line puts a caret as tall as the line it is on, and AutoHeight follows the lines' own heights.
/// </summary>
public class EditorLineHeightTests
{
    /// <summary>Draws the first line of its text at 40 pt, the rest at the label's size.</summary>
    class TwoSizeLabel : SkiaLabel
    {
        protected override void OnPropertyChanged(string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
            if (propertyName != nameof(Text))
                return;
            var text = Text ?? string.Empty;
            var cut = text.IndexOf('\n');
            Spans.Clear();
            if (cut < 0)
                return;
            Spans.Add(new TextSpan { Text = text[..(cut + 1)], FontSize = 40 });
            Spans.Add(new TextSpan { Text = text[(cut + 1)..] });
        }
    }

    class Editor : SkiaEditor
    {
        public double ViewportHeight => _scroll.HeightRequest;

        protected override SkiaLabel OnCreatingLabel(SkiaLabel label) => new TwoSizeLabel
        {
            KeepSpacesOnLineBreaks = label.KeepSpacesOnLineBreaks,
            NeedsGlyphPositions = label.NeedsGlyphPositions,
            Margin = label.Margin,
            TextColor = label.TextColor,
            HorizontalOptions = label.HorizontalOptions,
        };
    }

    static (HeadlessCanvasHost Host, Editor Editor) Create()
    {
        var host = new HeadlessCanvasHost(400, 400, scale: 2f, background: Colors.Black);
        var editor = new Editor { WidthRequest = 180, FontSize = 14, MaxLines = 6, AutoHeight = true, Text = "Big\nsmall" };
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

    [Fact]
    public void Caret_AsTallAsItsLine()
    {
        var (host, editor) = Create();
        using var _ = host;
        var lines = editor.Label.Lines;
        Assert.Equal(2, lines.Length);
        Assert.True(lines[0].Bounds.Height > lines[1].Bounds.Height, $"{lines[0].Bounds.Height} vs {lines[1].Bounds.Height}");
        var scale = editor.Label.RenderingScale;

        editor.CursorPosition = editor.Text.Length;
        host.AdvanceFrames(3);
        Assert.Equal((lines[1].Bounds.Top - editor.Label.DrawingRect.Top) / scale, editor.Cursor.Top, 2);
        Assert.Equal(lines[1].Bounds.Height / scale, editor.Cursor.HeightRequest, 2);

        editor.CursorPosition = 1;
        host.AdvanceFrames(3);
        Assert.Equal(0, editor.Cursor.Top, 2);
        Assert.Equal(lines[0].Bounds.Height / scale, editor.Cursor.HeightRequest, 2);
    }

    [Fact]
    public void AutoHeight_FollowsTheLinesOwnHeights()
    {
        var (host, editor) = Create();
        using var _ = host;
        var scale = editor.Label.RenderingScale;
        // the label's measured size is in whole pixels
        Assert.Equal(editor.Label.MeasuredSize.Pixels.Height / scale, editor.ViewportHeight, 0.5);
    }
}
