using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Right-to-left text in a plain SkiaLabel: Arabic and Hebrew are shaped from the text itself, and a line that mixes
/// directions is laid out in visual order (Unicode bidi, implicit levels), each direction run shaped on its own.
/// </summary>
public class LabelBidiTests
{
    private static SkiaLabel Render(SkiaLabel label)
    {
        var host = new HeadlessCanvasHost(700, 200, scale: 1f, background: Colors.Black);
        label.FontSize = 20;
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { label }
        };
        host.AdvanceFrames(3);
        return label;
    }

    private static string[] Visual(SkiaLabel label) => Assert.Single(label.Lines).Spans.Select(s => s.Text).ToArray();

    [Theory]
    [InlineData("مرحبا بكم")]
    [InlineData("שלום עולם")]
    public void RightToLeftText_IsShaped(string text)
    {
        var line = Assert.Single(Render(new SkiaLabel(text)).Lines);

        Assert.All(line.Spans, s => Assert.True(s.NeedsShaping));
        Assert.Equal(text, string.Concat(line.Spans.Select(s => s.Text)));
    }

    [Fact]
    public void PlainLatin_Untouched()
    {
        var line = Assert.Single(Render(new SkiaLabel("Plain text, no right to left.")).Lines);

        var span = Assert.Single(line.Spans);
        Assert.False(span.NeedsShaping);
        Assert.Equal("Plain text, no right to left.", span.Text);
    }

    [Fact]
    public void MixedLine_LeftToRightParagraph_RunsInVisualOrder()
    {
        var label = Render(new SkiaLabel("Hi مرحبا 123 שלום end"));

        Assert.Equal(new[] { "Hi ", " שלום", "123", "مرحبا ", " end" }, Visual(label));
        var line = label.Lines[0];
        Assert.Equal(line.Spans.Sum(s => s.Size.Width), line.Width, 3);
    }

    [Fact]
    public void MixedLine_RightToLeftParagraph_StartsFromTheRight()
    {
        var label = Render(new SkiaLabel("مرحبا Hello 2026 بكم"));

        Assert.Equal(new[] { " بكم", "Hello 2026", "مرحبا " }, Visual(label));
    }

    [Fact]
    public void Span_InsideRightToLeftLine_KeepsItsSpan()
    {
        var highlight = new TextSpan { Text = "DrawnUI", BackgroundColor = Colors.Purple };
        var label = Render(new SkiaLabel
        {
            Spans = { new TextSpan { Text = "مرحبا " }, highlight, new TextSpan { Text = " بكم" } }
        });

        var pieces = Assert.Single(label.Lines).Spans;
        Assert.Equal(new[] { " بكم", "DrawnUI", "مرحبا " }, pieces.Select(s => s.Text));
        Assert.Same(highlight, pieces.Single(s => s.Text == "DrawnUI").Span);
    }

    [Theory]
    // levels per char, LTR paragraph: Latin 0, Arabic 1, digits after Arabic 2
    [InlineData("ab مر 12", false, "00011122")]
    // RTL paragraph: Latin 2, Arabic 1, trailing whitespace back to the paragraph level
    [InlineData("مر ab ", true, "111221")]
    public void Levels_FollowUnicodeBidi(string text, bool rightToLeft, string expected)
    {
        var levels = SkiaLabel.Bidi.GetLevels(text, rightToLeft);

        Assert.Equal(expected, string.Concat(levels.Select(l => (char)('0' + l))));
    }
}
