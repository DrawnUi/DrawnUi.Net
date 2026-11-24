using SkiaSharp;
using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi.Testing;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A shader effect on a SkiaImage whose size changes: the texture the effect gets must match what is drawn now. The
/// image keeps a rescaled copy of its source made at its first size; after a resize it may draw the source directly
/// (size within 5% of the source) or stretch the copy (RescaleSource false), and the copy at the old size must not
/// reach the effect (a window resized on desktop: the paywall picture repeated its last row below the old height).
/// </summary>
public class ImageEffectAfterResizeTests
{
    private readonly ITestOutputHelper _output;

    public ImageEffectAfterResizeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Samples the input texture 1:1, like the app's look shaders.</summary>
    private const string OneToOneShader = @"
uniform float4 iMouse;
uniform float  iTime;
uniform float2 iResolution;
uniform float2 iImageResolution;
uniform shader iImage1;
uniform float2 iOffset;
uniform float2 iOrigin;

half4 main(float2 fragCoord)
{
    return iImage1.eval(fragCoord - iOffset);
}
";

    /// <summary>Square source: green, a red band at rows 900..1000, blue below it.</summary>
    private static SKImage MakeSource(int size = 1088)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        surface.Canvas.Clear(SKColors.Green);
        using var red = new SKPaint { Color = SKColors.Red };
        using var blue = new SKPaint { Color = SKColors.Blue };
        surface.Canvas.DrawRect(new SKRect(0, 900, size, 1000), red);
        surface.Canvas.DrawRect(new SKRect(0, 1000, size, size), blue);
        return surface.Snapshot();
    }

    [Theory]
    [InlineData(866, 1092)] // first size rescales, the new one draws the source as is (within 5%)
    [InlineData(866, 1000)] // the new size stretches the old copy (RescaleSource false)
    public void Effect_GetsCurrentPixels_AfterResize(int first, int second)
    {
        var host = new HeadlessCanvasHost(1200, 1200, scale: 1f, background: Colors.Black);

        var image = new SkiaImage
        {
            Aspect = TransformAspect.AspectCover,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            VisualEffects = { new SkiaShaderEffect { ShaderCode = OneToOneShader } },
        };

        var tile = new SkiaLayer
        {
            WidthRequest = first,
            HeightRequest = first,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            UseCache = SkiaCacheType.Image,
            Children = { image },
        };
        host.Canvas.Content = tile;

        image.SetImageInternal(MakeSource());
        host.AdvanceFrames(6);

        tile.WidthRequest = second;
        tile.HeightRequest = second;
        host.AdvanceFrames(6);

        var texture = image.CachedImage;
        _output.WriteLine($"{first} -> {second}: display {image.DisplayRect.Width:0}x{image.DisplayRect.Height:0}, " +
                          $"texture {(texture.IsValid ? $"{texture.Image.Width}x{texture.Image.Height}" : "none")}");
        if (texture.IsValid)
        {
            Assert.Equal((int)Math.Round(image.DisplayRect.Width), texture.Image.Width);
            Assert.Equal((int)Math.Round(image.DisplayRect.Height), texture.Image.Height);
        }

        // the red band, scaled to the new size, must be where the source has it
        using var snapshot = host.Canvas.CanvasView.Surface.Snapshot();
        using var bmp = SKBitmap.FromImage(snapshot);
        var y = (int)(950 * second / 1088f);
        var c = bmp.GetPixel(second / 2, y);
        _output.WriteLine($"  pixel at y {y}: {c}");
        Assert.True(c.Red > 150 && c.Green < 100 && c.Blue < 100, $"expected the red band at y {y}, got {c}");
    }
}
