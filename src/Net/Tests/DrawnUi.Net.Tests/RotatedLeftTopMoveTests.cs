using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A cached control moved by Left / Top draws at its new place also when it is rotated: the rotation turns it around
/// its own center wherever Left / Top put it.
/// </summary>
public class RotatedLeftTopMoveTests
{
    // Left / Top place a control only when it is cached (UseCache other than None), so the cached types are covered,
    // inside parents with every cache type a game layer uses
    [Theory]
    [InlineData(SkiaCacheType.Operations, 30, SkiaCacheType.None)]
    [InlineData(SkiaCacheType.Operations, 0, SkiaCacheType.None)]
    [InlineData(SkiaCacheType.Image, 30, SkiaCacheType.None)]
    [InlineData(SkiaCacheType.Operations, 30, SkiaCacheType.ImageComposite)]
    [InlineData(SkiaCacheType.Operations, 0, SkiaCacheType.ImageComposite)]
    [InlineData(SkiaCacheType.Operations, 30, SkiaCacheType.Operations)]
    [InlineData(SkiaCacheType.Operations, 30, SkiaCacheType.Image)]
    public void A_rotated_sprite_follows_Left_and_Top(SkiaCacheType cache, double rotation, SkiaCacheType parentCache)
    {
        using var host = new HeadlessCanvasHost(600, 300, 1f, Colors.Black);
        SkiaShape sprite = null;
        host.Canvas.Content = new SkiaLayer
        {
            UseCache = parentCache,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaShape
                {
                    BackgroundColor = Colors.White, WidthRequest = 60, HeightRequest = 60,
                    HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
                    UseCache = cache, Rotation = rotation, Left = 20, Top = 120,
                }.Assign(out sprite),
            }
        };
        host.AdvanceFrames(3);
        Assert.True(Lit(host, 50, 150), "drawn at its first place " + LitBox(host));

        for (var i = 0; i < 80; i++) // a game loop moving it right, 5 points a frame
        {
            sprite.Left += 5;
            host.RenderFrame(16);
        }
        host.AdvanceFrames(2);

        Assert.True(Lit(host, 450, 150), $"drawn at its new place (Left {sprite.Left})");
        Assert.False(Lit(host, 50, 150), "nothing is left at its first place");
    }

    static string LitBox(HeadlessCanvasHost host)
    {
        using var image = host.Snapshot();
        using var b = SKBitmap.FromImage(image);
        int x0 = 9999, y0 = 9999, x1 = -1, y1 = -1;
        for (var y = 0; y < b.Height; y++)
        for (var x = 0; x < b.Width; x++)
            if (b.GetPixel(x, y).Red > 128) { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
        return $"lit box {x0},{y0} - {x1},{y1}";
    }

    static bool Lit(HeadlessCanvasHost host, int x, int y)
    {
        using var image = host.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.GetPixel(x, y).Red > 128;
    }
}
