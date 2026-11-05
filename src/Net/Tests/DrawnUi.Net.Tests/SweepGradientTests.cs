using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A Sweep gradient without a sweep angle (Value2 = 0, the default) goes round the full circle, as in DrawnUi.React
/// and DrawnUi.Rust: its colors change around the center instead of one flat color.
/// </summary>
public class SweepGradientTests
{
    [Fact]
    public void A_sweep_without_an_angle_covers_the_full_circle()
    {
        using var host = new HeadlessCanvasHost(200, 200, scale: 1f, background: Colors.Black);
        host.Canvas.Content = new SkiaShape
        {
            Type = ShapeType.Rectangle,
            WidthRequest = 200,
            HeightRequest = 200,
            FillGradient = new SkiaGradient
            {
                Type = GradientType.Sweep,
                Colors = new List<Color> { Colors.Red, Colors.Yellow, Colors.Lime, Colors.Blue, Colors.Red },
            },
        };
        host.AdvanceFrames(3);

        using var image = host.Snapshot();
        using var bitmap = SkiaSharp.SKBitmap.FromImage(image);
        var right = bitmap.GetPixel(180, 100);  // angle 0
        var below = bitmap.GetPixel(100, 180);  // angle 90
        var left = bitmap.GetPixel(20, 100);    // angle 180

        Assert.NotEqual(right, below);
        Assert.NotEqual(below, left);
    }
}
