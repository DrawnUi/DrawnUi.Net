using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A shader effect on a cached control samples the control's cache where it is drawn now: after its page scrolled,
/// a pass-through shader still shows the same pixels as the control without the effect.
/// </summary>
public class ShaderEffectScrolledTests
{
    const string PassThrough = """
uniform shader iImage1;
uniform float2 iResolution;
uniform float2 iImageResolution;
uniform float2 iOffset;
uniform float  iTime;
uniform float4 iMouse;

half4 main(float2 fragCoord) {
    float2 uv = (fragCoord - iOffset) / iResolution.xy;
    return iImage1.eval(uv * iImageResolution);
}
""";

    static SKBitmap Render(bool effect)
    {
        using var host = new HeadlessCanvasHost(400, 400, 1.25f);
        var box = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            HeightRequest = 120,
            UseCache = SkiaCacheType.Image,
            Children =
            {
                // a vertical gradient: a wrong row reads as a wrong color
                new SkiaShape
                {
                    HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
                    FillGradient = new SkiaGradient { Type = GradientType.Linear, StartXRatio = 0, StartYRatio = 0, EndXRatio = 0, EndYRatio = 1, Colors = { Colors.Red, Colors.Blue } },
                },
            },
        };
        if (effect)
            box.VisualEffects.Add(new SkiaShaderEffect { ShaderCode = PassThrough });
        var scroll = new SkiaScroll
        {
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack { Children = { new SkiaLayer { HeightRequest = 150 }, box, new SkiaLayer { HeightRequest = 1000 } } },
        };
        host.Canvas.Content = scroll;
        host.AdvanceFrames(6);
        scroll.ViewportOffsetY = -100; // the box moves up, its cache is drawn again at the new place
        host.AdvanceFrames(10);
        using var image = host.Snapshot();
        return SKBitmap.FromImage(image);
    }

    [Fact]
    public void PassThrough_AfterScroll_ShowsTheCacheWhereItIsDrawn()
    {
        using var plain = Render(false);
        using var withEffect = Render(true);
        // the box now spans 50..170 pt: sample rows inside it
        foreach (var y in new[] { 80, 110, 150 })
        {
            var px = (int)(y * 1.25f);
            var a = plain.GetPixel(200, px);
            var b = withEffect.GetPixel(200, px);
            Assert.True(Math.Abs(a.Red - b.Red) < 12 && Math.Abs(a.Blue - b.Blue) < 12, $"row {y}: {a} vs {b}");
        }
    }
}
