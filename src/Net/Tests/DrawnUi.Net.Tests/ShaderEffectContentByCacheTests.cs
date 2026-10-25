using SkiaSharp;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// What a shader effect receives as iImage1 and what it replaces, per cache type of the control carrying it.
/// Rule (all engines, Nick 2026-10-25): on a CACHED control (Image, GPU, Operations, OperationsFull) the shader
/// replaces the control and iImage1 is the control's own content; on an UNCACHED control the control paints and
/// the shader draws over it, iImage1 being the canvas there (the control plus what is behind it).
/// </summary>
public class ShaderEffectContentByCacheTests
{
    readonly ITestOutputHelper _output;

    public ShaderEffectContentByCacheTests(ITestOutputHelper output) => _output = output;

    const string Header = @"
uniform float4 iMouse;
uniform float  iTime;
uniform float2 iResolution;
uniform float2 iImageResolution;
uniform shader iImage1;
uniform float2 iOffset;
";

    // returns its input: shows what iImage1 holds
    const string PassThrough = Header + @"
half4 main(float2 fragCoord)
{
    return iImage1.eval((fragCoord - iOffset) * iImageResolution.xy / iResolution.xy);
}";

    // returns nothing: shows what is left under the effect
    const string Transparent = Header + @"
half4 main(float2 fragCoord)
{
    return half4(0);
}";

    static SKColor Pixel(HeadlessCanvasHost host, int x, int y)
    {
        using var image = host.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.GetPixel(x, y);
    }

    static string Name(SKColor c) =>
        c.Red > 200 && c.Green < 60 && c.Blue < 60 ? "red(content)"
        : c.Green > 120 && c.Red < 60 && c.Blue < 60 ? "green(behind)"
        : c.ToString();

    /// <summary>Red 100x100 shape at (50,50) over a green full-canvas background; returns the center pixel.</summary>
    static SKColor Render(SkiaCacheType cache, string shader)
    {
        using var host = new HeadlessCanvasHost(200, 200, scale: 1f, background: Colors.Black);
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaShape { BackgroundColor = Colors.Green, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill },
                new SkiaShape
                {
                    UseCache = cache,
                    BackgroundColor = Colors.Red,
                    WidthRequest = 100,
                    HeightRequest = 100,
                    Margin = new Thickness(50, 50, 0, 0),
                    VisualEffects = { new SkiaShaderEffect { ShaderCode = shader } },
                },
            }
        };
        host.AdvanceFrames(8);
        return Pixel(host, 100, 100);
    }

    /// <summary>Cached: the shader replaces the control and reads its own content. Uncached: the control paints,
    /// the shader draws over it reading the canvas there. Operations / OperationsFull read the background before
    /// 1.10.7.14: the picture had no image, so the effect fell back to a canvas snapshot without the control.</summary>
    [Theory]
    [InlineData(SkiaCacheType.Image, "red(content)", "green(behind)")]
    [InlineData(SkiaCacheType.Operations, "red(content)", "green(behind)")]
    [InlineData(SkiaCacheType.OperationsFull, "red(content)", "green(behind)")]
    [InlineData(SkiaCacheType.None, "red(content)", "red(content)")]
    public void InputAndReplacement_PerCacheType(SkiaCacheType cache, string passThrough, string transparent)
    {
        var pass = Name(Render(cache, PassThrough));
        var clear = Name(Render(cache, Transparent));
        _output.WriteLine($"{cache}: passthrough={pass} transparent={clear}");
        Assert.Equal(passThrough, pass);
        Assert.Equal(transparent, clear);
    }

    /// <summary>KeepParentCache on a picture-cached shape: the picture and its image are kept, the new value drawn.</summary>
    [Fact]
    public void KeepParentCache_OnOperationsShape_KeepsPictureAndDrawsNewValue()
    {
        const string tint = Header + @"
uniform float uAmount;
half4 main(float2 fragCoord)
{
    half4 c = iImage1.eval((fragCoord - iOffset) * iImageResolution.xy / iResolution.xy);
    return mix(c, half4(0, 0, 1, 1), uAmount);
}";
        SkiaShape shape = null;
        var effect = new SkiaShaderEffect { ShaderCode = tint, KeepParentCache = true }.SetUniform("uAmount", 0f);
        using var host = new HeadlessCanvasHost(200, 200, scale: 1f, background: Colors.Black);
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaShape
                {
                    BackgroundColor = Colors.Red, WidthRequest = 100, HeightRequest = 100,
                    Margin = new Thickness(50, 50, 0, 0), VisualEffects = { effect },
                }.Assign(out shape),
            }
        };
        host.AdvanceFrames(8);
        Assert.Equal(SkiaCacheType.Operations, shape.UseCache);
        var cache = shape.RenderObject;
        Assert.NotNull(cache?.Picture);
        Assert.Equal("red(content)", Name(Pixel(host, 100, 100)));

        effect.SetUniform("uAmount", 1f);
        host.AdvanceFrames(3);

        var after = Pixel(host, 100, 100);
        Assert.True(after.Blue > 200 && after.Red < 50, $"the new uniform was not drawn: {after}");
        Assert.Same(cache, shape.RenderObject);
    }
}
