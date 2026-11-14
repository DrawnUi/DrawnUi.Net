using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A shader effect that renders into a part of its control (a subclass clipping the destination, as a transition limited
/// to the photo band) still finds the control's texture placed over the whole control: the pixels it paints are the
/// ones under them, not the texture's top shifted down to where the part starts.
/// </summary>
public class ShaderEffectClippedDestinationTests
{
    private const string Blit = """
uniform float2 iResolution;
uniform float2 iImageResolution;
uniform float iTime;
uniform float2 iOffset;
uniform float4 iMouse;
uniform shader iImage1;

half4 main(float2 fragCoord)
{
    return iImage1.eval(fragCoord - iOffset);
}
""";

    /// <summary>Paints only the lower half of its control.</summary>
    class LowerHalfEffect : SkiaShaderEffect
    {
        public override void Render(DrawingContext ctx)
        {
            var d = ctx.Destination;
            base.Render(ctx.WithDestination(new SKRect(d.Left, d.MidY, d.Right, d.Bottom)));
        }
    }

    [Theory]
    [InlineData(SkiaCacheType.Image)]
    [InlineData(SkiaCacheType.Operations)]
    public void ClippedDestination_SamplesTheControlWhereItIs(SkiaCacheType cache)
    {
        using var host = new HeadlessCanvasHost(100, 200, 1f, Colors.Black);
        var effect = new LowerHalfEffect { ShaderCode = Blit, UseBackground = PostRendererEffectUseBackgroud.Always };
        var control = new SkiaLayer
        {
            UseCache = cache,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                // red top half, blue bottom half
                new SkiaShape { BackgroundColor = Colors.Red, HorizontalOptions = LayoutOptions.Fill, HeightRequest = 100 },
                new SkiaShape { BackgroundColor = Colors.Blue, HorizontalOptions = LayoutOptions.Fill, HeightRequest = 100, TranslationY = 100 },
            }
        };
        control.VisualEffects.Add(effect);
        host.Canvas.Content = control;
        host.AdvanceFrames(20);

        using var image = host.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        // the lower half painted by the effect shows what is there: blue, not the red top half moved down
        var lower = bitmap.GetPixel(50, 150);
        Assert.True(lower.Blue > 200 && lower.Red < 60, $"lower half {lower}");
        var justBelowMiddle = bitmap.GetPixel(50, 105);
        Assert.True(justBelowMiddle.Blue > 200 && justBelowMiddle.Red < 60, $"just below the middle {justBelowMiddle}");
    }
}
