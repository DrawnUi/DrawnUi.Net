using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Synthetic italic (SkewX) gets room for its slant once, at the end of a line that ends with an italic letter. Every
/// italic run used to get it twice, so text after an italic span ("ANY " then "span") started a quarter of the font size
/// or more further right than after the same span upright: a double-width gap.
/// </summary>
public class LabelItalicSpacingTests
{
    private readonly ITestOutputHelper _out;
    public LabelItalicSpacingTests(ITestOutputHelper o) { _out = o; }

    const float Size = 17;

    static SkiaLabel Spans(string first, bool italic, string second) => new()
    {
        FontSize = Size,
        TextColor = Colors.White,
        Spans =
        {
            new TextSpan { Text = first, IsItalic = italic },
            new TextSpan { Text = second },
        }
    };

    static (HeadlessCanvasHost host, SkiaLabel[] labels) Scene(params SkiaLabel[] labels)
    {
        var host = new HeadlessCanvasHost(600, 400, scale: 1f, background: Colors.Black);
        var stack = new SkiaStack();
        foreach (var label in labels)
            stack.Children.Add(label);
        host.Canvas.Content = stack;
        host.AdvanceFrames(4);
        return (host, labels);
    }

    static float FirstSpanWidth(SkiaLabel label) => label.Lines[0].Spans[0].Size.Width;

    [Theory]
    [InlineData("ANY ")] // ends with a space: no ink to make room for
    [InlineData("ANY")]  // ends with a letter mid-line: the slant leans over what follows
    public void ItalicSpanInsideALine_TakesTheSameRoomAsUpright(string first)
    {
        var (host, labels) = Scene(Spans(first, false, "span"), Spans(first, true, "span"));
        using var _ = host;

        var upright = FirstSpanWidth(labels[0]);
        var italic = FirstSpanWidth(labels[1]);
        _out.WriteLine($"'{first}': upright {upright}, italic {italic}");
        Assert.True(Math.Abs(italic - upright) <= 1, $"italic '{first}' took {italic - upright:0.0} px more than upright");
    }

    [Fact]
    public void ItalicLetterAtTheEndOfALine_KeepsRoomForItsSlant()
    {
        var upright = new SkiaLabel("Italic end") { FontSize = Size, TextColor = Colors.White };
        var italic = new SkiaLabel("Italic end") { FontSize = Size, TextColor = Colors.White, FontAttributes = FontAttributes.Italic };
        var (host, _) = Scene(upright, italic);
        using var __ = host;

        var extra = italic.Lines[0].Width - upright.Lines[0].Width;
        _out.WriteLine($"line end: upright {upright.Lines[0].Width}, italic {italic.Lines[0].Width}");
        Assert.True(extra >= Size * 0.25f - 1, $"italic line end got {extra:0.0} px of slant room");
    }

    [Fact]
    public void ItalicSpaceAtTheEndOfALine_NeedsNoRoom()
    {
        var upright = new SkiaLabel("tail ") { FontSize = Size, TextColor = Colors.White, KeepSpacesOnLineBreaks = true };
        var italic = new SkiaLabel("tail ") { FontSize = Size, TextColor = Colors.White, KeepSpacesOnLineBreaks = true, FontAttributes = FontAttributes.Italic };
        var (host, _) = Scene(upright, italic);
        using var __ = host;

        _out.WriteLine($"'tail ': upright {upright.Lines[0].Width}, italic {italic.Lines[0].Width}");
        Assert.True(Math.Abs(italic.Lines[0].Width - upright.Lines[0].Width) <= 1,
            $"italic 'tail ' line {italic.Lines[0].Width}, upright {upright.Lines[0].Width}");
    }

    [Fact]
    public void SlantRoom_NeverWidensALinePastTheLabelWidth()
    {
        var label = new SkiaLabel("Italic text that fills the width of a narrow label and wraps")
        {
            FontSize = Size, TextColor = Colors.White, WidthRequest = 160, FontAttributes = FontAttributes.Italic,
        };
        var (host, _) = Scene(label);
        using var __ = host;

        foreach (var line in label.Lines)
            Assert.True(line.Width <= 160 + 0.5f, $"line '{line.Value}' is {line.Width} px wide in a 160 px label");
    }
}
