using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A label's FillGradient goes to one place: the glyphs of a label without a background, the background of a label with
/// one (its text keeps TextColor), the same rule as DrawnUi.React and DrawnUi.Rust.
/// </summary>
public class LabelGradientTargetTests
{
    private static SkiaGradient PurpleToCyan() => new()
    {
        Type = GradientType.Linear,
        EndXRatio = 1, EndYRatio = 0,
        Colors = new List<Color> { Color.Parse("#6610F2"), Color.Parse("#0DCAF0") },
    };

    private static int WhitePixels(HeadlessCanvasHost host)
    {
        using var image = host.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var c = bitmap.GetPixel(x, y);
            if (c.Red > 230 && c.Green > 230 && c.Blue > 230)
                count++;
        }
        return count;
    }

    [Fact]
    public void With_a_background_the_gradient_fills_it_and_the_text_keeps_its_color()
    {
        using var host = new HeadlessCanvasHost(300, 120, scale: 1f, background: Colors.Black);
        host.Canvas.Content = new SkiaLabel("bg + text")
        {
            FontSize = 40,
            TextColor = Colors.White,
            BackgroundColor = Color.Parse("#212529"),
            Padding = new Thickness(12, 8),
            GradientByLines = false,
            FillGradient = PurpleToCyan(),
        };
        host.AdvanceFrames(3);

        Assert.True(WhitePixels(host) > 50, "the text disappeared into the gradient");
    }

    [Fact]
    public void Without_a_background_the_gradient_colors_the_text()
    {
        using var host = new HeadlessCanvasHost(300, 120, scale: 1f, background: Colors.Black);
        host.Canvas.Content = new SkiaLabel("gradient")
        {
            FontSize = 40,
            TextColor = Colors.White,
            GradientByLines = false,
            FillGradient = PurpleToCyan(),
        };
        host.AdvanceFrames(3);

        Assert.True(WhitePixels(host) < 5, "the text kept its own color instead of the gradient");
        Assert.True(host.NonBackgroundFraction(Colors.Black) > 0.01, "nothing was drawn");
    }
}
