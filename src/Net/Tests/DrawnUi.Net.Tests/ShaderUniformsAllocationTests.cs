using SkiaSharp;
using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi;
using DrawnUi.Testing;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A shader effect drawn every frame writes a uniform into its holder only when the value changed (each write
/// allocates in SkiaSharp), and still draws the content and the values of this frame: content that changes every
/// frame, a uniform set again, a uniform array changed in place.
/// </summary>
[Collection("Multithreaded")] // counts allocations: never next to the parallel load of other classes
public class ShaderUniformsAllocationTests
{
    private readonly ITestOutputHelper _output;

    public ShaderUniformsAllocationTests(ITestOutputHelper output) => _output = output;

    private const string Tint = """
uniform float2 iResolution;
uniform float2 iImageResolution;
uniform float iTime;
uniform float2 iOffset;
uniform float4 iMouse;
uniform shader iImage1;
uniform float4 uTint;
uniform float uGain;

half4 main(float2 fragCoord)
{
    half4 c = iImage1.eval((fragCoord - iOffset) * iImageResolution / iResolution);
    return half4(c.rgb * uTint.rgb * uGain, 1.0);
}
""";

    sealed class Scene : IDisposable
    {
        public readonly HeadlessCanvasHost Host = new(40, 30, 1f, Colors.Black);
        public readonly SkiaShape Shape;
        public readonly SkiaShaderEffect Effect;
        public readonly float[] TintValues = { 1f, 1f, 1f, 1f };

        public Scene()
        {
            Effect = new ProbeEffect
            {
                ShaderCode = Tint,
                UseBackground = PostRendererEffectUseBackgroud.Always,
            }.SetUniform("uTint", TintValues).SetUniform("uGain", 1f);
            Shape = new SkiaShape
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                BackgroundColor = Colors.White,
                UseCache = SkiaCacheType.None,
            };
            Shape.VisualEffects.Add(Effect);
            Host.Canvas.Content = new SkiaLayer { Children = { Shape } };
            Host.AdvanceFrames(10);
        }

        public SKColor Pixel()
        {
            using var image = Host.Snapshot();
            using var bitmap = SKBitmap.FromImage(image);
            return bitmap.GetPixel(20, 15);
        }

        public void Dispose() => Host.Dispose();
    }

    /// <summary>The effect with its uniforms step reachable: what one draw of it writes, measured alone.</summary>
    sealed class ProbeEffect : SkiaShaderEffect
    {
        public SKRuntimeEffectUniforms MakeUniforms(SKRect destination) => CreateUniforms(destination);
    }

    [Fact]
    public void UnchangedUniforms_AreNotWrittenAgain()
    {
        // The uniforms step of one draw, called directly after the effect was drawn (compiled) a few times: no frame
        // scheduling, snapshot or other test in the count, only the writes into the uniforms holder.
        using var scene = new Scene();
        var effect = (ProbeEffect)scene.Effect;
        var destination = new SKRect(0, 0, 40, 30);
        long still = AllocationMeasure.MinBytesPerFrame(() => effect.MakeUniforms(destination), warmup: 10, frames: 50);
        _output.WriteLine($"{still} B per draw with uniforms unchanged");
        // nothing is written when nothing changed (7 writes, 336 B per draw before the fix)
        Assert.Equal(0, still);

        // A uniform changed in place, without SetUniform, is still written: read from the holder itself. What a
        // write allocates depends on the JIT tier of SkiaSharp's writer (48 B unoptimized, 0 once optimized), so it
        // can't tell whether the write happened.
        for (int i = 1; i <= 3; i++)
        {
            scene.TintValues[3] = 100 + i;
            using var data = effect.MakeUniforms(destination).ToData();
            Assert.Contains(100f + i, System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(data.AsSpan()).ToArray());
        }
    }

    [Fact]
    public void EveryFrameDrawsThisFramesContentAndValues()
    {
        using var scene = new Scene();
        Assert.Equal(new SKColor(255, 255, 255), scene.Pixel());

        // the content changes, the uniforms stay: the picture follows the content
        scene.Shape.BackgroundColor = Colors.Red;
        scene.Host.RenderFrame(16.0);
        Assert.Equal(new SKColor(255, 0, 0), scene.Pixel());
        scene.Shape.BackgroundColor = Colors.Blue;
        scene.Host.RenderFrame(16.0);
        Assert.Equal(new SKColor(0, 0, 255), scene.Pixel());

        // a uniform set again
        scene.Shape.BackgroundColor = Colors.White;
        scene.Effect.SetUniform("uGain", 0.5f);
        scene.Host.RenderFrame(16.0);
        var half = scene.Pixel();
        Assert.InRange(half.Red, 126, 129);
        Assert.Equal(half.Red, half.Blue);

        // a uniform array changed in place, without SetUniform
        scene.TintValues[1] = 0f;
        scene.Shape.Update();
        scene.Host.RenderFrame(16.0);
        var magenta = scene.Pixel();
        Assert.InRange(magenta.Red, 126, 129);
        Assert.Equal(0, magenta.Green);

        // and back
        scene.TintValues[1] = 1f;
        scene.Effect.SetUniform("uGain", 1f);
        scene.Host.RenderFrame(16.0);
        Assert.Equal(new SKColor(255, 255, 255), scene.Pixel());
    }
}
