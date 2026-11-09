using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkewX / SkewY skew a control whatever their sign: a negative skew leans the other way, the mirror of the positive
/// one around the control's center (the default pivot).
/// </summary>
public class NegativeSkewTests
{
    const int W = 200, H = 200;

    static SKBitmap Draw(float skewX, float skewY)
    {
        using var host = new HeadlessCanvasHost(W, H, 1f, Colors.Black);
        host.Canvas.Content = new SkiaShape
        {
            WidthRequest = 80, HeightRequest = 80, BackgroundColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
            SkewX = skewX, SkewY = skewY,
        };
        host.AdvanceFrames(3);
        using var image = host.Snapshot();
        return SKBitmap.FromImage(image);
    }

    static bool White(SKBitmap b, int x, int y) => b.GetPixel(x, y).Red > 200;

    [Theory]
    [InlineData(25, 0)]
    [InlineData(0, 25)]
    public void A_negative_skew_leans_the_other_way(float skewX, float skewY)
    {
        using var none = Draw(0, 0);
        using var plus = Draw(skewX, skewY);
        using var minus = Draw(-skewX, -skewY);

        var differsFromNone = false;
        var mirrored = true;
        for (var y = 0; y < H; y++)
        for (var x = 0; x < W; x++)
        {
            if (White(minus, x, y) != White(none, x, y)) differsFromNone = true;
            // skewX leans along x: the negative one is the positive one flipped left-right; skewY: top-bottom
            var mx = skewX != 0 ? W - 1 - x : x;
            var my = skewY != 0 ? H - 1 - y : y;
            if (White(minus, x, y) != White(plus, mx, my)) mirrored = false;
        }

        Assert.True(differsFromNone, "a negative skew changes the drawing");
        Assert.True(mirrored, "it is the mirror of the positive skew");
    }
}
