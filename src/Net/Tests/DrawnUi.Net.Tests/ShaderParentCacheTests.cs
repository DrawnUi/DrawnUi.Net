using SkiaSharp;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A shader effect paints over its parent's cached content. By default a uniform change or an animation tick redraws
/// the effect with the new value and the parent keeps its cache; with
/// <see cref="SkiaShaderEffect.ShouldInvalidateParentCache"/> every such change rebuilds the parent's cache, as before
/// 1.10.7.14.
/// </summary>
[Collection("Multithreaded")] // reads pixels of fresh canvases: flaky next to the parallel load of other classes
public class ShaderParentCacheTests
{
    // Every uniform a shader declares must be written: an unset one holds whatever memory it got, not 0.
    // So the SetUniform shader declares only uAmount, the animated one only what AnimatedShaderEffect writes.
    const string Header = @"
uniform float4 iMouse;
uniform float  iTime;
uniform float2 iResolution;
uniform float2 iImageResolution;
uniform shader iImage1;
uniform float2 iOffset;
";

    const string Body = @"
half4 main(float2 fragCoord)
{
    float2 inputCoord = (fragCoord - iOffset) * iImageResolution.xy / iResolution.xy;
    return mix(iImage1.eval(inputCoord), half4(0, 0, 1, 1), AMOUNT);
}
";

    static readonly string TintShader = Header + "uniform float uAmount;" + Body.Replace("AMOUNT", "uAmount");

    static readonly string ProgressShader = Header + "uniform float progress; uniform float2 iCenter;"
                                            + Body.Replace("AMOUNT", "progress");

    static SKColor Center(HeadlessCanvasHost host)
    {
        using var image = host.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.GetPixel(100, 100);
    }

    static (HeadlessCanvasHost host, SkiaLayout card) Scene(SkiaShaderEffect effect)
    {
        var host = new HeadlessCanvasHost(200, 200, scale: 1f, background: Colors.Black);
        SkiaLayout card = null;
        host.Canvas.Content = new SkiaLayout
        {
            UseCache = SkiaCacheType.Image,
            WidthRequest = 200,
            HeightRequest = 200,
            VisualEffects = { effect },
            Children = { new SkiaShape { BackgroundColor = Colors.Red, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill } },
        }.Assign(out card);
        host.AdvanceFrames(6);
        return (host, card);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetUniform_DrawsTheNewValue_ParentCacheRebuiltOnlyWhenAsked(bool invalidate)
    {
        var effect = new SkiaShaderEffect { ShaderCode = TintShader, ShouldInvalidateParentCache = invalidate }.SetUniform("uAmount", 0f);
        var (host, card) = Scene(effect);
        using (host)
        {
            var cache = card.RenderObject;
            Assert.NotNull(cache);
            var before = Center(host);
            Assert.True(before.Red > 200 && before.Blue < 50, $"expected red before, got {before}");

            effect.SetUniform("uAmount", 1f);
            Assert.True(host.NeedsFrame, "SetUniform did not ask for a frame");
            host.AdvanceFrames(3);

            var after = Center(host);
            Assert.True(after.Blue > 200 && after.Red < 50, $"the new uniform was not drawn: {after}");
            if (invalidate)
                Assert.NotSame(cache, card.RenderObject);
            else
                Assert.Same(cache, card.RenderObject);
        }
    }

    [Fact]
    public void AnimatedTicks_ByDefault_AnimateOverTheSameCache()
    {
        var effect = new AnimatedShaderEffect { ShaderCode = ProgressShader, DurationMs = 200 };
        var (host, card) = Scene(effect);
        using (host)
        {
            effect.Play();
            host.AdvanceFrames(2);
            var cache = card.RenderObject;
            Assert.NotNull(cache);

            host.AdvanceFrames(20);

            Assert.True(effect.Progress > 0.99, $"the animation did not run: {effect.Progress}");
            var after = Center(host);
            Assert.True(after.Blue > 200 && after.Red < 50, $"the last tick was not drawn: {after}");
            Assert.Same(cache, card.RenderObject);
        }
    }
}
