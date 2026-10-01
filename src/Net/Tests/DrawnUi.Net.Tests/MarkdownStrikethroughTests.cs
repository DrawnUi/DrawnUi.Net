using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaRichLabel markdown: ~~text~~ becomes a struck-out span, the tildes are not drawn.
/// </summary>
public class MarkdownStrikethroughTests
{
    private static List<TextSpan> Render(string text)
    {
        using var host = new HeadlessCanvasHost(400, 200, scale: 1f, background: Colors.Black);
        var label = new SkiaRichLabel { Text = text, FontSize = 16 };
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { label }
        };
        host.AdvanceFrames(3);
        return label.Spans.ToList();
    }

    [Fact]
    public void DoubleTilde_StrikesOut()
    {
        var spans = Render("Was ~~$59.99~~ now $29.99");

        Assert.Equal(new[] { "$59.99" }, spans.Where(s => s.Strikeout).Select(s => s.Text));
        Assert.DoesNotContain(spans, s => s.Text.Contains('~'));
    }

    [Fact]
    public void SingleTilde_StaysText()
    {
        var spans = Render("about ~5 km");

        Assert.DoesNotContain(spans, s => s.Strikeout);
        Assert.Contains("~5", string.Concat(spans.Select(s => s.Text)));
    }
}
