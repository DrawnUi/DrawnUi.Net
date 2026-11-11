using SkiaSharp;
using DrawnUi.Infrastructure;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The engine feeds the standard uniforms (iResolution, iImageResolution, iTime, iOffset, iMouse) every frame. A
/// shader that leaves some of them out (it does not use them) must still draw: writing a uniform the compiled shader
/// does not declare threw, so the effect drew nothing.
/// </summary>
public class ShaderStandardUniformsTests
{
    // Only iResolution and iOffset: no iImageResolution, iTime or iMouse.
    private const string PartialUniforms = """
uniform float2 iResolution;
uniform float2 iOffset;

half4 main(float2 fragCoord)
{
    float2 uv = (fragCoord - iOffset) / iResolution;
    return half4(1.0, uv.x * 0.0, 0.0, 1.0);
}
""";

    // No uniform at all.
    private const string NoUniforms = """
half4 main(float2 fragCoord)
{
    return half4(1.0, 0.0, 0.0, 1.0);
}
""";

    [Theory]
    [InlineData(PartialUniforms)]
    [InlineData(NoUniforms)]
    public void ShaderWithoutSomeStandardUniforms_StillDraws(string code)
    {
        using var shader = SkiaShader.FromCode(code, e => throw new Exception(e));
        Assert.True(shader.IsCompiled);

        using var surface = SKSurface.Create(new SKImageInfo(40, 30));
        surface.Canvas.Clear(SKColors.Black);
        shader.DrawRect(surface.Canvas, new SKRect(0, 0, 40, 30));
        surface.Canvas.Flush();

        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        var pixel = bitmap.GetPixel(20, 15);
        Assert.Equal(new SKColor(255, 0, 0, 255), pixel);
    }

    /// <summary>A shader that does not read iImage1 still draws when the engine has an input texture for it.</summary>
    [Fact]
    public void ShaderWithoutImage1_DrawsWithAnInputTexture()
    {
        using var shader = SkiaShader.FromCode(NoUniforms, e => throw new Exception(e));
        using var sourceSurface = SKSurface.Create(new SKImageInfo(40, 30));
        sourceSurface.Canvas.Clear(SKColors.Blue);
        using var source = sourceSurface.Snapshot();

        using var surface = SKSurface.Create(new SKImageInfo(40, 30));
        surface.Canvas.Clear(SKColors.Black);
        shader.DrawRect(surface.Canvas, source, new SKRect(0, 0, 40, 30));
        surface.Canvas.Flush();

        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        Assert.Equal(new SKColor(255, 0, 0, 255), bitmap.GetPixel(20, 15));
    }
}
