using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Two fixed-width children of (line - spacing) / 2 share their SkiaWrap line at any scale. The wrap took 2 px off its
/// line ("fix pixels roundings"), and each child rounded to pixels on its own can pass the line by a pixel, so the
/// second one always started a new line: the HelloMaui root page showed one column of cards on Mac Catalyst (scale 2).
/// </summary>
public class WrapExactFitTests
{
    private readonly ITestOutputHelper _out;
    public WrapExactFitTests(ITestOutputHelper output) => _out = output;

    [Theory]
    [InlineData(772, 1f)]
    [InlineData(772, 1.5f)]
    [InlineData(772, 1.75f)]
    [InlineData(772, 2f)]
    [InlineData(772, 3f)]
    [InlineData(743, 1f)]
    [InlineData(743, 1.25f)]
    [InlineData(743, 2.25f)]
    [InlineData(745, 1.5f)]
    [InlineData(745, 3f)]
    public void TwoHalves_ShareALine_ThirdStartsTheNext(double line, float scale)
    {
        var half = (line - 16) / 2;
        using var host = new HeadlessCanvasHost((int)(900 * scale), (int)(300 * scale), scale: scale, background: Colors.Black);
        SkiaShape a = null, b = null, c = null;
        host.Canvas.Content = new SkiaWrap
        {
            Spacing = 16,
            WidthRequest = line,
            HorizontalOptions = LayoutOptions.Start,
            Children =
            {
                new SkiaShape { BackgroundColor = Colors.Red, WidthRequest = half, HeightRequest = 40 }.Assign(out a),
                new SkiaShape { BackgroundColor = Colors.Green, WidthRequest = half, HeightRequest = 40 }.Assign(out b),
                new SkiaShape { BackgroundColor = Colors.Blue, WidthRequest = half, HeightRequest = 40 }.Assign(out c),
            }
        };
        host.AdvanceFrames(3);

        _out.WriteLine($"line {line} scale {scale}: a {a.DrawingRect} b {b.DrawingRect} c {c.DrawingRect}");
        Assert.Equal(a.DrawingRect.Top, b.DrawingRect.Top, 0.5);
        Assert.True(b.DrawingRect.Left > a.DrawingRect.Right, "the second half did not stay beside the first");
        Assert.True(c.DrawingRect.Top >= a.DrawingRect.Bottom, "the third child stayed on the full line");
    }
}
