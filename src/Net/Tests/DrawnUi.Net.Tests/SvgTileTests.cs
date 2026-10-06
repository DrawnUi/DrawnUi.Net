using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaSvg Aspect Tile repeats the picture at its natural size (one SVG unit per point) from the copy placed
/// by HorizontalAlignment / VerticalAlignment, like SkiaImage. It used to draw one aspect-fit copy.
/// </summary>
public class SvgTileTests
{
    // 10x10 blue tile with a 2x2 white marker in its top-left corner
    const string MarkedTile = """
        <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 10 10">
          <rect width="10" height="10" fill="#0000FF"/>
          <rect width="2" height="2" fill="#FFFFFF"/>
        </svg>
        """;

    static readonly SKColor Blue = new(0, 0, 255);
    static readonly SKColor White = new(255, 255, 255);

    static SKBitmap Render(float scale, DrawImageAlignment alignment)
    {
        var px = (int)(100 * scale);
        using var host = new HeadlessCanvasHost(px, px, scale, Colors.Black);
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaSvg
                {
                    SvgString = MarkedTile,
                    Aspect = TransformAspect.Tile,
                    HorizontalAlignment = alignment,
                    VerticalAlignment = alignment,
                    WidthRequest = 100,
                    HeightRequest = 100,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Start,
                }
            }
        };
        host.AdvanceFrames(3);

        using var snapshot = host.Snapshot();
        return SKBitmap.FromImage(snapshot);
    }

    static void AssertColor(SKColor expected, SKColor actual, string at)
    {
        Assert.True(Math.Abs(expected.Red - actual.Red) <= 8
                    && Math.Abs(expected.Green - actual.Green) <= 8
                    && Math.Abs(expected.Blue - actual.Blue) <= 8,
            $"{at}: expected {expected}, got {actual}");
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    public void Tile_RepeatsAtNaturalSize(float scale)
    {
        using var bmp = Render(scale, DrawImageAlignment.Start);
        var step = (int)(10 * scale);
        var px = (int)(100 * scale);
        for (var x = 0; x < px; x += step)
        {
            for (var y = 0; y < px; y += step)
            {
                AssertColor(White, bmp.GetPixel(x + 1, y + 1), $"marker {x + 1},{y + 1}");
                AssertColor(Blue, bmp.GetPixel(x + step / 2, y + step / 2), $"tile {x + step / 2},{y + step / 2}");
            }
        }
    }

    [Fact]
    public void Tile_StartsAtTheAlignedCopy()
    {
        // centered: one copy at 45..55, the pattern runs out from there both ways
        using var bmp = Render(1f, DrawImageAlignment.Center);
        AssertColor(White, bmp.GetPixel(46, 46), "center copy");
        AssertColor(White, bmp.GetPixel(6, 96), "left bottom copy");
        AssertColor(Blue, bmp.GetPixel(41, 41), "between markers");
        AssertColor(Blue, bmp.GetPixel(1, 1), "corner is not a marker");
    }
}
