using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// ImageComposite: a child removed and the same object added back at another place before the next draw (a pooled
/// sprite) leaves nothing behind: the area where it was drawn is erased, wherever the object is now.
/// </summary>
public class CompositeRemovedChildTests
{
    [Theory]
    [InlineData(SkiaCacheType.ImageComposite)]
    [InlineData(SkiaCacheType.Image)] // a plain cache redraws everything: the same result, the reference
    public void A_pooled_child_moved_in_one_frame_leaves_no_pixels_behind(SkiaCacheType cache)
    {
        using var host = new HeadlessCanvasHost(400, 200, scale: 1f, background: Colors.Black);
        SkiaShape brick = null;
        var layer = new SkiaLayer
        {
            UseCache = cache,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaShape { BackgroundColor = Colors.SteelBlue, WidthRequest = 40, HeightRequest = 40, Left = 300, Top = 120 },
                new SkiaShape { BackgroundColor = Colors.Orange, WidthRequest = 40, HeightRequest = 40, Left = 20, Top = 20 }.Assign(out brick),
            },
        };
        host.Canvas.Content = layer;
        host.AdvanceFrames(3);
        Assert.True(Lit(host, 40, 40), "the brick is drawn at its first place");

        // back to the pool and out again at another place, before the next draw
        layer.RemoveSubView(brick);
        brick.Left = 160;
        brick.Top = 120;
        layer.AddSubView(brick);
        host.AdvanceFrames(3);

        Assert.False(Lit(host, 40, 40), "nothing is left where the brick was");
        Assert.True(Lit(host, 180, 140), "the brick is drawn at its new place");
        Assert.True(Lit(host, 320, 140), "the other child is still drawn");
    }

    static bool Lit(HeadlessCanvasHost host, int x, int y)
    {
        using var image = host.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        var c = bitmap.GetPixel(x, y);
        return c.Red + c.Green + c.Blue > 60;
    }
}
