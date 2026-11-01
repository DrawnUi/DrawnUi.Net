using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A span's underline has the color of its letters: the span's TextColor when set, else the label's.
/// </summary>
public class LabelSpanUnderlineColorTests
{
    [Fact]
    public void SpanWithoutColor_UnderlineInTheLabelColor()
    {
        using var host = new HeadlessCanvasHost(300, 120, 1f, Colors.Black);
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl>
            {
                new SkiaLabel
                {
                    FontSize = 40,
                    TextColor = Colors.Red,
                    Margin = new Thickness(10),
                    Spans = { new TextSpan { Text = "File", Underline = true, UnderlineWidth = 4 } },
                },
            },
        };
        host.AdvanceFrames(3);

        using var image = host.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        long red = 0, other = 0;
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var p = bitmap.GetPixel(x, y);
            if (p.Red < 40 && p.Green < 40 && p.Blue < 40)
                continue; // background
            if (p.Red > 120 && p.Green < 80 && p.Blue < 80)
                red++;
            else if (p.Green > 120)
                other++; // a green-yellow underline
        }

        Assert.True(red > 0, "nothing drawn");
        Assert.True(other == 0, $"{other} pixels not in the label color");
    }
}
