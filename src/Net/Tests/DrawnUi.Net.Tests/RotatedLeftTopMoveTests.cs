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

    // the Bricks Breaker victory rays: Operations cache inside an uncached layer, scaled and rotated, parked with
    // SkipRendering after its first draw, unparked, then moved by Left / Top every frame
    [Theory]
    [InlineData(30, 1.0, false)]
    [InlineData(30, 0.8, false)]
    [InlineData(30, 0.8, true)]
    [InlineData(0, 0.8, true)]
    public void Rays_parked_scaled_and_rotated_follow_Left_and_Top(double rotation, double scale, bool park)
    {
        using var host = new HeadlessCanvasHost(600, 300, 1f, Colors.Black);
        SkiaShape rays = null;
        host.Canvas.Content = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            IsClippedToBounds = true,
            Children =
            {
                new SkiaShape { BackgroundColor = Colors.DarkBlue, WidthRequest = 40, HeightRequest = 40, Left = 500, Top = 20, UseCache = SkiaCacheType.GPU },
                new SkiaShape
                {
                    BackgroundColor = Colors.White, WidthRequest = 60, HeightRequest = 60, ZIndex = 4,
                    HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
                    UseCache = SkiaCacheType.Operations, Rotation = rotation, Scale = scale, Left = 20, Top = 120,
                }.Assign(out rays),
            }
        };
        host.AdvanceFrames(3);
        if (park)
        {
            rays.SkipRendering = true;
            host.AdvanceFrames(3);
            rays.SkipRendering = false;
            host.AdvanceFrames(2);
        }
        Assert.True(Lit(host, 50, 150), "drawn at its first place " + LitBox(host));

        for (var i = 0; i < 80; i++)
        {
            rays.Left += 5;
            host.RenderFrame(16);
        }
        host.AdvanceFrames(2);

        Assert.True(Lit(host, 450, 150), $"drawn at its new place (Left {rays.Left}) " + LitBox(host));
        Assert.False(Lit(host, 50, 150), "nothing is left at its first place");
    }

    // a copy of the game's VictoryRays: a plain SkiaControl that draws one image in Paint, Operations cache
    class ImageRays : SkiaControl
    {
        static SKImage _image;
        readonly SKPaint _paint = new() { IsAntialias = true };
        public ImageRays() { UseCache = SkiaCacheType.Operations; HorizontalOptions = LayoutOptions.Center; VerticalOptions = LayoutOptions.Center; WidthRequest = 900; HeightRequest = 900; }
        protected override void Paint(DrawingContext ctx)
        {
            base.Paint(ctx);
            if (_image == null)
            {
                using var surface = SKSurface.Create(new SKImageInfo(64, 64));
                surface.Canvas.Clear(SKColors.White);
                _image = surface.Snapshot();
            }
            ctx.Context.Canvas.DrawImage(_image, ctx.Destination, _paint);
        }
    }

    [Theory]
    [InlineData(30)]
    [InlineData(0)]
    public void Image_rays_animated_then_held_follow_Left_and_Top(double rotation)
    {
        using var host = new HeadlessCanvasHost(600, 300, 1f, Colors.Black);
        ImageRays rays = null;
        host.Canvas.Content = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, IsClippedToBounds = true,
            Children =
            {
                new ImageRays { WidthRequest = 60, HeightRequest = 60, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, ZIndex = 4, Opacity = 0.01 }.Assign(out rays),
            }
        };
        host.AdvanceFrames(3);
        rays.SkipRendering = true; // parked
        host.AdvanceFrames(3);

        // flash: placed, turned, scaled, shown, unparked, animated
        rays.Left = 20; rays.Top = 120; rays.Rotation = rotation; rays.Scale = 0.3; rays.Opacity = 1;
        rays.SkipRendering = false;
        _ = rays.RotateToAsync(rotation + 50, 300);
        _ = rays.ScaleToAsync(1, 1, 150);
        for (var i = 0; i < 30; i++) host.RenderFrame(16); // animations end, held on screen
        Assert.True(Lit(host, 50, 150), "drawn at its first place " + LitBox(host));

        for (var i = 0; i < 80; i++) // follows the paddle
        {
            rays.Left += 5;
            host.RenderFrame(16);
        }
        host.AdvanceFrames(2);

        Assert.True(Lit(host, 450, 150), $"drawn at its new place (Left {rays.Left}) " + LitBox(host));
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
