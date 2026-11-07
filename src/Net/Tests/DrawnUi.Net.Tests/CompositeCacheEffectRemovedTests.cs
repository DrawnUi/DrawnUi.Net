using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A cached layer keeps drawing its content after a shader effect that read its cache is removed (and disposed).
/// An ImageComposite cache re-records on its own surface; with nothing redrawn since, the surface gives the same
/// image again, and the cache it came from must not dispose it (Bricks Breaker level reveal: the bricks went blank).
/// </summary>
public class CompositeCacheEffectRemovedTests
{
    const string Pass = """
uniform float4 iMouse;
uniform float  iTime;
uniform float2 iResolution;
uniform float2 iImageResolution;
uniform shader iImage1;
uniform float2 iOffset;

half4 main(float2 fragCoord) {
    float2 inputCoord = (fragCoord - iOffset) * iImageResolution / iResolution;
    return iImage1.eval(inputCoord);
}
""";

    [Theory]
    [InlineData(SkiaCacheType.ImageComposite)]
    [InlineData(SkiaCacheType.ImageCompositeGPU)]
    [InlineData(SkiaCacheType.Image)]
    public void Layer_still_draws_after_its_shader_effect_is_removed(SkiaCacheType cache)
    {
        using var host = new HeadlessCanvasHost(300, 200, 2f, Colors.Black);
        SkiaLayout layer = null;
        host.Canvas.Content = new SkiaLayout
        {
            UseCache = cache,
            WidthRequest = 280,
            HeightRequest = 180,
            Children = Enumerable.Range(0, 12).Select(i => (SkiaControl)new SkiaShape
            {
                WidthRequest = 40, HeightRequest = 20, Left = (i % 4) * 60, Top = (i / 4) * 40,
                BackgroundColor = Colors.Orange, UseCache = SkiaCacheType.Operations,
            }).ToList(),
        }.Assign(out layer);
        host.AdvanceFrames(3);
        var before = host.NonBackgroundFraction(Colors.Black);
        Assert.True(before > 0.1);

        var effect = new SkiaShaderEffect
        {
            ShaderCode = Pass, UseBackground = PostRendererEffectUseBackgroud.Always, AutoCreateInputTexture = false,
        };
        layer.VisualEffects.Add(effect);
        host.AdvanceFrames(5);

        layer.VisualEffects.Remove(effect);
        host.AdvanceFrames(5);
        Assert.Equal(before, host.NonBackgroundFraction(Colors.Black), 3);

        effect.Dispose();
        host.AdvanceFrames(5);
        Assert.Equal(before, host.NonBackgroundFraction(Colors.Black), 3);
    }
}
