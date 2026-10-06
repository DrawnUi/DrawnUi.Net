using SkiaSharp;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaImage members that were declared but drew nothing: Aspect Tile, the sprite sheet cell
/// (SpriteWidth / SpriteHeight / SpriteIndex), UseGradient + StartColor / EndColor, the Grayscale
/// effect and UseAssembly. Each one rendered headless and checked on pixels.
/// </summary>
public class SkiaImageFeaturesTests
{
    static readonly SKColor Blue = new(0, 0, 255);
    static readonly SKColor White = new(255, 255, 255);

    static readonly SKColor[] CellColors =
    {
        new(255, 0, 0), new(0, 255, 0), new(0, 0, 255),
        new(255, 255, 0), new(255, 0, 255), new(0, 255, 255),
    };

    static SKImage MakeImage(int w, int h, Action<SKCanvas> draw)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        surface.Canvas.Clear(SKColors.Transparent);
        draw(surface.Canvas);
        return surface.Snapshot();
    }

    static void Fill(SKCanvas canvas, SKRect rect, SKColor color)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = false };
        canvas.DrawRect(rect, paint);
    }

    /// <summary>10x10 blue tile with a 2x2 white marker in its top-left corner.</summary>
    static SKImage MarkedTile() => MakeImage(10, 10, c =>
    {
        Fill(c, new SKRect(0, 0, 10, 10), Blue);
        Fill(c, new SKRect(0, 0, 2, 2), White);
    });

    /// <summary>30x20 sheet: 3 columns x 2 rows of 10x10 cells, cell i painted CellColors[i].</summary>
    static SKImage SpriteSheet() => MakeImage(30, 20, c =>
    {
        for (var i = 0; i < 6; i++)
        {
            var x = i % 3 * 10;
            var y = i / 3 * 10;
            Fill(c, new SKRect(x, y, x + 10, y + 10), CellColors[i]);
        }
    });

    static HeadlessCanvasHost Host(SkiaImage image, int size = 100, float scale = 1f)
    {
        var host = new HeadlessCanvasHost(size, size, scale, Colors.Black);
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { image }
        };
        return host;
    }

    static SkiaImage Image(double size, TransformAspect aspect) => new()
    {
        WidthRequest = size,
        HeightRequest = size,
        HorizontalOptions = LayoutOptions.Start,
        VerticalOptions = LayoutOptions.Start,
        Aspect = aspect,
    };

    static SKBitmap Pixels(HeadlessCanvasHost host)
    {
        using var snapshot = host.Snapshot();
        return SKBitmap.FromImage(snapshot);
    }

    static void AssertColor(SKColor expected, SKColor actual, int tolerance = 2, string at = "")
    {
        Assert.True(Math.Abs(expected.Red - actual.Red) <= tolerance
                    && Math.Abs(expected.Green - actual.Green) <= tolerance
                    && Math.Abs(expected.Blue - actual.Blue) <= tolerance,
            $"{at}: expected {expected}, got {actual}");
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    public void Tile_RepeatsAtNaturalPixelSize(float scale)
    {
        // 100 points: 100 px at scale 1, 200 px at scale 2; the 10 px tile repeats every 10 px either way
        var image = Image(100, TransformAspect.Tile);
        image.HorizontalAlignment = DrawImageAlignment.Start;
        image.VerticalAlignment = DrawImageAlignment.Start;
        var px = (int)(100 * scale);
        using var host = Host(image, px, scale);
        image.SetImageInternal(MarkedTile());

        host.RenderFrame();
        host.RenderFrame();

        using var bmp = Pixels(host);
        for (var x = 0; x < px; x += 10)
        {
            for (var y = 0; y < px; y += 10)
            {
                AssertColor(White, bmp.GetPixel(x + 1, y + 1), at: $"marker {x + 1},{y + 1}");
                AssertColor(Blue, bmp.GetPixel(x + 5, y + 5), at: $"tile {x + 5},{y + 5}");
            }
        }
    }

    [Fact]
    public void Tile_StartsAtTheAlignedCopy()
    {
        // centered: one copy at 45..55, the pattern runs out from there both ways
        var image = Image(100, TransformAspect.Tile);
        using var host = Host(image);
        image.SetImageInternal(MarkedTile());

        host.RenderFrame();
        host.RenderFrame();

        using var bmp = Pixels(host);
        AssertColor(White, bmp.GetPixel(46, 46), at: "center copy");
        AssertColor(White, bmp.GetPixel(6, 96), at: "left bottom copy");
        AssertColor(Blue, bmp.GetPixel(41, 41), at: "between markers");
        AssertColor(Blue, bmp.GetPixel(1, 1), at: "corner is not a marker");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sprite_DrawsTheChosenCell_AndNothingOutsideTheSheet(bool bitmapSource)
    {
        var image = Image(50, TransformAspect.Fill);
        image.SpriteWidth = 10;
        image.SpriteHeight = 10;
        image.SpriteIndex = 4;
        using var host = Host(image);
        if (bitmapSource)
            image.SetBitmapInternal(SKBitmap.FromImage(SpriteSheet())); // what async loads hand over
        else
            image.SetImageInternal(SpriteSheet());

        host.RenderFrame();
        host.RenderFrame();

        using (var bmp = Pixels(host))
        {
            AssertColor(CellColors[4], bmp.GetPixel(25, 25), at: "cell 4 center");
            // linear filtering must not pull in the neighbor cells at the edges
            AssertColor(CellColors[4], bmp.GetPixel(0, 25), at: "cell 4 left edge");
            AssertColor(CellColors[4], bmp.GetPixel(49, 25), at: "cell 4 right edge");
            AssertColor(CellColors[4], bmp.GetPixel(25, 0), at: "cell 4 top edge");
            AssertColor(CellColors[4], bmp.GetPixel(25, 49), at: "cell 4 bottom edge");
        }

        image.SpriteIndex = 6; // 3x2 sheet: 0..5
        host.RenderFrame();

        using (var bmp = Pixels(host))
        {
            AssertColor(SKColors.Black, bmp.GetPixel(25, 25), at: "index out of range");
        }
    }

    [Fact]
    public void Sprite_AutoSizesToTheCell()
    {
        var image = new SkiaImage
        {
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Aspect = TransformAspect.None,
            SpriteWidth = 10,
            SpriteHeight = 10,
            SpriteIndex = 2,
        };
        using var host = Host(image);
        image.SetImageInternal(SpriteSheet());

        host.RenderFrame();
        host.RenderFrame();
        host.RenderFrame();

        Assert.Equal(10, image.DrawingRect.Width, 0.5);
        Assert.Equal(10, image.DrawingRect.Height, 0.5);
        using var bmp = Pixels(host);
        AssertColor(CellColors[2], bmp.GetPixel(5, 5), at: "cell 2");
        AssertColor(SKColors.Black, bmp.GetPixel(15, 5), at: "outside the cell");
    }

    [Fact]
    public void Sprite_TilesTheCellOnly()
    {
        var image = Image(40, TransformAspect.Tile);
        image.HorizontalAlignment = DrawImageAlignment.Start;
        image.VerticalAlignment = DrawImageAlignment.Start;
        image.SpriteWidth = 10;
        image.SpriteHeight = 10;
        image.SpriteIndex = 1;
        using var host = Host(image);
        image.SetImageInternal(SpriteSheet());

        host.RenderFrame();
        host.RenderFrame();

        using var bmp = Pixels(host);
        // the whole sheet tiled would put cell 0 here
        AssertColor(CellColors[1], bmp.GetPixel(5, 5), at: "first copy");
        AssertColor(CellColors[1], bmp.GetPixel(35, 35), at: "last copy");
    }

    [Fact]
    public void Gradient_GoesTopToBottom_ThroughTheImageAlpha()
    {
        var image = Image(100, TransformAspect.Fill);
        image.RescalingQuality = FilterQuality.None;
        image.UseGradient = true;
        image.StartColor = Colors.Red;
        image.EndColor = Colors.Blue;
        using var host = Host(image, 120);
        // left half opaque, right half transparent
        image.SetImageInternal(MakeImage(20, 20, c => Fill(c, new SKRect(0, 0, 10, 20), White)));

        host.RenderFrame();
        host.RenderFrame();

        using var bmp = Pixels(host);
        var rect = image.DrawingRect;
        var x = (int)(rect.Left + rect.Width / 4);
        var top = (int)Math.Ceiling(rect.Top);
        var bottom = (int)Math.Floor(rect.Bottom) - 1;
        AssertColor(new SKColor(255, 0, 0), bmp.GetPixel(x, top), tolerance: 4, at: "top");
        AssertColor(new SKColor(0, 0, 255), bmp.GetPixel(x, bottom), tolerance: 4, at: "bottom");
        var middle = bmp.GetPixel(x, (int)rect.MidY);
        Assert.InRange(middle.Red, 100, 155);
        Assert.InRange(middle.Blue, 100, 155);
        AssertColor(SKColors.Black, bmp.GetPixel((int)(rect.Left + rect.Width * 3 / 4), (int)rect.MidY), at: "transparent source stays empty");
    }

    [Fact]
    public void Grayscale_Effect_MakesGray()
    {
        var image = Image(50, TransformAspect.Fill);
        image.AddEffect = SkiaImageEffect.Grayscale;
        using var host = Host(image);
        image.SetImageInternal(MakeImage(10, 10, c => Fill(c, new SKRect(0, 0, 10, 10), new SKColor(255, 0, 0))));

        host.RenderFrame();
        host.RenderFrame();

        using var bmp = Pixels(host);
        var c = bmp.GetPixel(25, 25);
        Assert.True(c.Red == c.Green && c.Green == c.Blue && c.Red > 0, $"not gray: {c}");
    }

    [Fact]
    public void Grayscale_ColorPresetEffect_MakesGray()
    {
        var image = Image(50, TransformAspect.Fill);
        image.VisualEffects.Add(new ColorPresetEffect { Preset = SkiaImageEffect.Grayscale });
        using var host = Host(image);
        image.SetImageInternal(MakeImage(10, 10, c => Fill(c, new SKRect(0, 0, 10, 10), new SKColor(255, 0, 0))));

        host.RenderFrame();
        host.RenderFrame();

        using var bmp = Pixels(host);
        var c = bmp.GetPixel(25, 25);
        Assert.True(c.Red == c.Green && c.Green == c.Blue && c.Red > 0, $"not gray: {c}");
    }

    public static IEnumerable<object[]> Assemblies()
    {
        yield return new object[] { typeof(SkiaImageFeaturesTests).Assembly };
        yield return new object[] { typeof(SkiaImageFeaturesTests).Assembly.GetName().Name! };
    }

    [Theory]
    [MemberData(nameof(Assemblies))]
    public void UseAssembly_LoadsAPlainPathAsEmbeddedResource(object assembly)
    {
        var image = Image(20, TransformAspect.Fill);
        image.UseAssembly = assembly;
        image.Source = "Resources/green.png"; // embedded as DrawnUi.Net.Tests.Resources.green.png
        using var host = Host(image);

        SKColor c = default;
        for (var i = 0; i < 100; i++)
        {
            host.RenderFrame();
            using var bmp = Pixels(host);
            c = bmp.GetPixel(10, 10);
            if (c.Green > 150)
                break;
            Thread.Sleep(20);
        }

        AssertColor(new SKColor(0, 200, 0), c, at: "embedded image");
    }
}
