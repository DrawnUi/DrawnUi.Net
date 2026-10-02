using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A Fill child in a SkiaWrap is measured with the whole line width, as in DrawnUi.React and DrawnUi.Rust: after
/// other children it does not fit, so it takes a line of its own and the children after it start the next line.
/// It used to get the rest of its line (1.9.7.4 flex-fill), which squeezed a panel into the strip left beside wide
/// siblings (the HelloMaui Sprites card cut its buttons).
/// </summary>
public class WrapFillChildTests
{
    private readonly ITestOutputHelper _out;
    public WrapFillChildTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void FillChild_TakesALineOfItsOwn_NextChildStartsTheNextLine()
    {
        using var host = new HeadlessCanvasHost(400, 300, scale: 1f, background: Colors.Black);
        SkiaShape first = null, fill = null, after = null;
        host.Canvas.Content = new SkiaWrap
        {
            Spacing = 0,
            Children =
            {
                new SkiaShape { BackgroundColor = Colors.Red, WidthRequest = 50, HeightRequest = 20 }.Assign(out first),
                new SkiaShape { BackgroundColor = Colors.Green, HorizontalOptions = LayoutOptions.Fill, HeightRequest = 20 }.Assign(out fill),
                new SkiaShape { BackgroundColor = Colors.Blue, WidthRequest = 50, HeightRequest = 20 }.Assign(out after),
            }
        };
        host.AdvanceFrames(3);

        _out.WriteLine($"first {first.DrawingRect} fill {fill.DrawingRect} after {after.DrawingRect}");
        Assert.Equal(0, fill.DrawingRect.Left, 0.5);
        Assert.True(fill.DrawingRect.Top >= first.DrawingRect.Bottom - 0.5f, "the Fill child stayed on the first line");
        Assert.Equal(400, fill.DrawingRect.Width, 2.5); // the whole line (the shape draws 1 px inside its slot)
        Assert.True(after.DrawingRect.Top >= fill.DrawingRect.Bottom - 0.5f, "the next child shared the Fill child's line");
        Assert.Equal(0, after.DrawingRect.Left, 0.5);
    }
}
