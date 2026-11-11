using SkiaSharp;
using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi;
using DrawnUi.Testing;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A shader that makes its own pixels (UseBackground Never, no input texture) on an empty Fill layer: a panel drawn
/// by a shader under the content next to it (Dungeon Run's dialog). The layer has nothing else to draw, the shader
/// must still cover the layer's area, with iOffset at the layer's corner as for an effect with an input texture.
/// </summary>
public class GenerativeShaderOnEmptyLayerTests
{
    private const string Red = """
uniform float2 iResolution;
uniform float2 iOffset;

half4 main(float2 fragCoord)
{
    // red inside the control's own area only: iOffset is where the control is drawn
    float2 uv = (fragCoord - iOffset) / iResolution;
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
        return half4(0.0);
    return half4(1.0, 0.0, 0.0, 1.0);
}
""";

    [Fact]
    public void EmptyFillLayerWithAGenerativeShader_DrawsItsArea()
    {
        using var host = new HeadlessCanvasHost(200, 200, 1f, Colors.Black);
        host.Canvas.Content = new SkiaLayer
        {
            WidthRequest = 120,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children = new List<SkiaControl>
            {
                new SkiaLayer
                {
                    VerticalOptions = LayoutOptions.Fill,
                    VisualEffects =
                    {
                        new SkiaShaderEffect
                        {
                            ShaderCode = Red,
                            UseBackground = PostRendererEffectUseBackgroud.Never,
                            AutoCreateInputTexture = false,
                        },
                    },
                },
                new SkiaStack
                {
                    Children = new List<SkiaControl> { new SkiaControl { HeightRequest = 80 } },
                },
            },
        };
        host.AdvanceFrames(3);

        using var image = host.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        // the layer spans x 40..160, y 60..140: its right and bottom parts are red only with iOffset at its corner
        Assert.Equal(new SKColor(255, 0, 0, 255), bitmap.GetPixel(100, 100));
        Assert.Equal(new SKColor(255, 0, 0, 255), bitmap.GetPixel(150, 130));
        Assert.Equal(SKColors.Black, bitmap.GetPixel(30, 100));
    }
}
