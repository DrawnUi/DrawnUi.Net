using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A label inside a stack sized by its content is measured on an unbounded width first and its lines are reused for
/// the final width. Every change that affects the text layout must still lay the lines out again.
/// </summary>
public class LabelReuseLinesTests
{
    static (HeadlessCanvasHost host, SkiaLabel label, SkiaLabel spanned, TextSpan span) Scene()
    {
        var host = new HeadlessCanvasHost(800, 600, 1f, Colors.Black);
        var span = new TextSpan { Text = "span text" };
        SkiaLabel label, spanned;
        host.Canvas.Content = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaStack
                {
                    HorizontalOptions = LayoutOptions.Center,
                    MaximumWidthRequest = 400,
                    Children =
                    {
                        new SkiaLabel("Some text") { FontSize = 16, HorizontalOptions = LayoutOptions.Fill }.Assign(out label),
                        new SkiaLabel { FontSize = 16, HorizontalOptions = LayoutOptions.Fill, Spans = { span } }.Assign(out spanned),
                    }
                }
            }
        };
        host.AdvanceFrames(4);
        return (host, label, spanned, span);
    }

    static string Text(SkiaLabel label) => string.Join("|", label.Lines.Select(l => l.Value));

    static void Settle(HeadlessCanvasHost host) => host.AdvanceFrames(4);

    [Fact]
    public void Text_Changes() { var (h, l, _, _) = Scene(); l.Text = "Other words here"; Settle(h); Assert.Equal("Other words here", Text(l)); }

    [Fact]
    public void LongerText_Wraps()
    {
        var (h, l, _, _) = Scene();
        l.Text = string.Join(" ", Enumerable.Repeat("wrapping", 30));
        Settle(h);
        Assert.True(l.LinesCount > 1, $"lines {l.LinesCount}");
        Assert.True(l.DrawingRect.Width <= 401, $"width {l.DrawingRect.Width}");
    }

    [Fact]
    public void FontSize_Changes()
    {
        var (h, l, _, _) = Scene();
        var before = l.Lines[0].Width;
        l.FontSize = 32;
        Settle(h);
        Assert.True(l.Lines[0].Width > before * 1.8f, $"{before} -> {l.Lines[0].Width}");
    }

    [Fact]
    public void Bold_Changes()
    {
        var (h, l, _, _) = Scene();
        var lines = l.Lines;
        l.FontAttributes = FontAttributes.Bold;
        Settle(h);
        Assert.NotSame(lines, l.Lines); // laid out again

        // the same width as a label made bold from the start
        Assert.Equal(new SkiaLabel("Some text") { FontSize = 16, FontAttributes = FontAttributes.Bold }.Measure(float.PositiveInfinity, 100, 1f).Pixels.Width,
            l.Lines[0].Width, 1);
    }

    [Fact]
    public void CharacterSpacing_Changes()
    {
        var (h, l, _, _) = Scene();
        var before = l.Lines[0].Width;
        l.CharacterSpacing = 3;
        Settle(h);
        Assert.True(l.Lines[0].Width > before, $"{before} -> {l.Lines[0].Width}");
    }

    [Fact]
    public void TextTransform_Changes()
    {
        var (h, l, _, _) = Scene();
        l.TextTransform = TextTransform.Uppercase;
        Settle(h);
        Assert.Equal("SOME TEXT", Text(l));
    }

    [Fact]
    public void MaxLines_Changes()
    {
        var (h, l, _, _) = Scene();
        l.Text = string.Join(" ", Enumerable.Repeat("wrapping", 30));
        Settle(h);
        l.MaxLines = 1;
        Settle(h);
        Assert.Equal(1, l.LinesCount);
    }

    [Fact]
    public void SpanText_Changes()
    {
        var (h, _, s, span) = Scene();
        span.Text = "a different span";
        Settle(h);
        Assert.Equal("a different span", Text(s));
    }

    [Fact]
    public void SpanFontSize_Changes()
    {
        var (h, _, s, span) = Scene();
        var before = s.Lines[0].Width;
        span.FontSize = 30;
        Settle(h);
        Assert.True(s.Lines[0].Width > before * 1.5f, $"{before} -> {s.Lines[0].Width}");
    }

    [Fact]
    public void SpanAdded_Shows()
    {
        var (h, _, s, _) = Scene();
        s.Spans.Add(new TextSpan { Text = " more" });
        Settle(h);
        Assert.Equal("span text more", Text(s));
    }

    [Fact]
    public void NarrowerParent_Wraps()
    {
        var (h, l, _, _) = Scene();
        l.Text = "Some text that is long enough to wrap in a narrow box";
        Settle(h);
        var stack = (SkiaStack)l.Parent;
        stack.MaximumWidthRequest = 120;
        Settle(h);
        Assert.True(l.LinesCount > 1, $"lines {l.LinesCount}");
    }
}
