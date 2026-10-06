using DrawnUi.Draw;
using DrawnUi.Infrastructure;
using DrawnUi.Testing;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A shader file next to the app loads by its relative path on the desktop heads, as MAUI raw assets do.
/// OpenTK used to throw NotSupportedException for every ShaderSource; WPF needed a startup preload.
/// </summary>
public class ShaderPackageFileTests
{
    // the uniforms every SkiaShaderEffect sets
    private const string Green = """
        uniform float4 iMouse;
        uniform float iTime;
        uniform float2 iResolution;
        uniform float2 iImageResolution;
        uniform shader iImage1;
        uniform float2 iOffset;
        uniform float2 iOrigin;
        half4 main(float2 p) { return half4(0, 1, 0, 1); }
        """;

    [Fact]
    public void ShaderSourceFile_LoadsAndDraws()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "shaderfile-test");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "green.sksl"), Green);
        const string source = "shaderfile-test/green.sksl";
        try
        {
            Assert.True(SkSl.TryLoadFromResources(source, out var code));
            Assert.Equal(Green, code);
            using (var stream = SkSl.OpenPackageFileAsync(source).GetAwaiter().GetResult())
                Assert.Equal(Green.Length, stream.Length);

            using var host = new HeadlessCanvasHost(100, 100, 1f, Colors.Black);
            host.Canvas.Content = new SkiaShape
            {
                WidthRequest = 50,
                HeightRequest = 50,
                BackgroundColor = Colors.Red,
                UseCache = SkiaCacheType.Image,
                VisualEffects = { new SkiaShaderEffect { ShaderSource = source } }
            };
            host.AdvanceFrames(5);

            using var snapshot = host.Snapshot();
            using var bitmap = SKBitmap.FromImage(snapshot);
            var pixel = bitmap.GetPixel(25, 25);
            Assert.True(pixel.Green > 200 && pixel.Red < 50, $"expected the shader's green, got {pixel}");
        }
        finally
        {
            Directory.Delete(folder, true);
            SkSl.LoadedCache.TryRemove(source, out _);
            SkSl.CompiledCache.TryRemove(source, out _);
        }
    }
}
