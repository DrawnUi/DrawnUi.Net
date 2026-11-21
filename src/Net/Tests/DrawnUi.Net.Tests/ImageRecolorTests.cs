using SkiaSharp;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// AddEffect = Recolor paints a picture's brightness in ColorTint, stretched by Contrast; on SkiaImageTiles the
/// control's effect applies to the whole tiled result.
/// </summary>
public class ImageRecolorTests
{
    /// <summary>w x h picture: left half gray 0.25, right half gray 0.75.</summary>
    static SKImage TwoGrays(int w, int h)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        using var paint = new SKPaint { Color = new SKColor(64, 64, 64) };
        surface.Canvas.DrawRect(0, 0, w / 2f, h, paint);
        paint.Color = new SKColor(191, 191, 191);
        surface.Canvas.DrawRect(w / 2f, 0, w / 2f, h, paint);
        return surface.Snapshot();
    }

    static SKColor Expected(byte gray, Color tint, float contrast)
    {
        var l = gray / 255f;
        var v = (l - 0.5f) * contrast + 0.5f;
        byte C(double t) => (byte)Math.Clamp(Math.Round(v * t * 255), 0, 255);
        return new SKColor(C(tint.Red), C(tint.Green), C(tint.Blue));
    }

    static void AssertNear(SKColor expected, SKColor actual, string at)
    {
        Assert.True(Math.Abs(expected.Red - actual.Red) <= 3 && Math.Abs(expected.Green - actual.Green) <= 3
                    && Math.Abs(expected.Blue - actual.Blue) <= 3, $"{at}: expected {expected}, got {actual}");
    }

    static SKBitmap Render(SkiaImage image, Action<SkiaImage> load)
    {
        using var host = new HeadlessCanvasHost(100, 100, 1f, Colors.Black);
        host.Canvas.Content = new SkiaLayout { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, Children = { image } };
        load(image);
        host.RenderFrame(); host.RenderFrame(); host.RenderFrame();
        using var snapshot = host.Snapshot();
        return SKBitmap.FromImage(snapshot);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(2.6f)]
    public void Recolor_PaintsBrightnessInTheTint(float contrast)
    {
        var tint = Color.FromRgb(97, 56, 140);
        var image = new SkiaImage
        {
            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, Aspect = TransformAspect.Fill,
            AddEffect = SkiaImageEffect.Recolor, ColorTint = tint, Contrast = contrast,
        };
        using var bmp = Render(image, i => i.SetImageInternal(TwoGrays(100, 100)));
        AssertNear(Expected(64, tint, contrast), bmp.GetPixel(25, 50), "dark half");
        AssertNear(Expected(191, tint, contrast), bmp.GetPixel(75, 50), "light half");
    }

    [Fact]
    public void Tiles_TheEffectAppliesToTheWholeResult()
    {
        var tint = Color.FromRgb(190, 140, 45);
        var tiles = new SkiaImageTiles
        {
            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
            TileAspect = TransformAspect.Cover, TileWidth = 20, TileHeight = 20,
            AddEffect = SkiaImageEffect.Recolor, ColorTint = tint, Contrast = 1.6,
        };
        using var bmp = Render(tiles, i => i.SetImageInternal(TwoGrays(40, 40)));
        for (var x = 0; x < 100; x += 20)
        {
            AssertNear(Expected(64, tint, 1.6f), bmp.GetPixel(x + 5, 50), $"tile {x} dark");
            AssertNear(Expected(191, tint, 1.6f), bmp.GetPixel(x + 15, 50), $"tile {x} light");
        }
    }

    class TilesProbe : SkiaImageTiles
    {
        public SkiaImage Inner => Tile;
    }

    [Fact]
    public void Tiles_DrawWithTheControlsRescalingQuality()
    {
        var tiles = new TilesProbe
        {
            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
            TileWidth = 20, TileHeight = 20, RescalingQuality = FilterQuality.Medium,
        };
        using var bmp = Render(tiles, i => i.SetImageInternal(TwoGrays(40, 40)));
        Assert.NotNull(tiles.Inner);
        Assert.Equal(FilterQuality.Medium, tiles.Inner.RescalingQuality);
    }
}
