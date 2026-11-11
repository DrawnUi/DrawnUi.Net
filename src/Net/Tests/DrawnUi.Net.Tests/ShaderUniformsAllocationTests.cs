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
            Effect = new SkiaShaderEffect
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

    static long BytesPerFrame(bool changeUniformEveryFrame)
    {
        using var scene = new Scene();
        const int frames = 200;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < frames; i++)
        {
            if (changeUniformEveryFrame)
                scene.TintValues[3] = i; // changed in place, the effect sees it at the next draw
            scene.Shape.Update(); // the content is drawn again every frame
            scene.Host.RenderFrame(16.0);
        }
        return (GC.GetAllocatedBytesForCurrentThread() - before) / frames;
    }

    [Fact]
    public void UnchangedUniforms_AreNotWrittenAgain()
    {
        long still = BytesPerFrame(false);
        long changing = BytesPerFrame(true);
        _output.WriteLine($"{still} B/frame uniforms unchanged, {changing} B/frame one uniform changed every frame");
        // writing uTint again every frame is what an unchanged frame no longer pays
        Assert.True(changing - still >= 24, $"unchanged {still} B/frame, changing {changing} B/frame");
        Assert.True(still <= StillBudget, $"unchanged uniforms: {still} B/frame");
    }

    // measured with the fix: what an effect drawn every frame still allocates (snapshot, shader, SkiaSharp wrappers)
    // (765 measured, iTime written each frame included; every uniform written each frame was ~75 B more per uniform)
    const long StillBudget = 800;

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
