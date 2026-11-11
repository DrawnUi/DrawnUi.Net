using System.Runtime.InteropServices;
using DrawnUi.Draw.Meshes;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A mesh made again every frame (Dungeon Run's corridor: new vertices, a few triangles more or less, a uniform that
/// changes and one that stays) puts only SkiaSharp's wrappers of the new native objects on the GC heap: the stand-in
/// writes a program uniform only when its value changed and wraps the vertex bytes without closures or pixmap
/// wrappers. Every frame still draws its own triangles and values.
/// </summary>
[Collection("Multithreaded")] // counts allocations: never next to the parallel load of other classes
public class SKMeshAllocationTests
{
    private readonly ITestOutputHelper _output;

    public SKMeshAllocationTests(ITestOutputHelper output) => _output = output;

    static readonly SKMeshSpecificationAttribute Pos = new(SKMeshSpecificationAttributeType.Float2, 0, "position");
    const string ShiftVs = "uniform float2 uShift; Varyings main(const Attributes a) { Varyings v; v.position = a.position + uShift; return v; }";
    const string PassVs = "Varyings main(const Attributes a) { Varyings v; v.position = a.position; return v; }";
    const string TintFs = "uniform float4 uTint; float2 main(const Varyings v, out half4 color) { color = half4(uTint); return v.position; }";
    const string KeepTintFs = "uniform float2 uKeep; uniform float4 uTint; float2 main(const Varyings v, out half4 color) { color = half4(uTint) * half(uKeep.x); return v.position; }";

    [Theory]
    [InlineData(false)] // positions from the attribute (Dungeon Run)
    [InlineData(true)]  // positions from the vertex program run on the CPU
    public void MeshMadeEveryFrame_AllocatesOnlyTheWrappersOfItsNewObjects(bool vertexProgram)
    {
        using var spec = SKMeshSpecification.Make(new[] { Pos }, 8, Array.Empty<SKMeshSpecificationVarying>(),
            vertexProgram ? ShiftVs : PassVs, vertexProgram ? TintFs : KeepTintFs,
            SKColorSpace.CreateSrgb(), SKAlphaType.Premul, out var errors);
        Assert.True(spec != null, errors);
        const int most = 560;
        var data = new float[most * 6];
        for (int t = 0; t < most; t++)
        {
            float x = t % 24, y = t / 24;
            (data[t * 6], data[t * 6 + 1], data[t * 6 + 2], data[t * 6 + 3], data[t * 6 + 4], data[t * 6 + 5]) = (x, y, x + 1, y, x, y + 1);
        }
        var uniforms = new float[6]; // uShift / uKeep stays, uTint changes every frame
        uniforms[0] = vertexProgram ? 0 : 1;
        using var bmp = new SKBitmap(new SKImageInfo(32, 32, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bmp);
        using var paint = new SKPaint { Color = SKColors.White };

        void Frame(int i)
        {
            int triangles = 480 + i * 7 % 80;
            (uniforms[2], uniforms[3], uniforms[4], uniforms[5]) = (i % 2, 1 - i % 2, 0, 1); // red, green, red...
            using var vb = SKMeshVertexBuffer.Make(MemoryMarshal.AsBytes(data.AsSpan(0, triangles * 6)));
            using var u = SKData.CreateCopy(MemoryMarshal.AsBytes(uniforms.AsSpan()));
            using var mesh = SKMesh.Make(spec!, SKMeshMode.Triangles, vb, triangles * 3, 0, u, new SKRect(0, 0, 32, 32), out _);
            canvas.DrawMesh(mesh!, paint);
        }

        int frame = 0;
        long perFrame = AllocationMeasure.MinBytesPerFrame(() => Frame(frame++), warmup: 80, frames: 80);
        _output.WriteLine($"{perFrame} B per frame");

        // each frame draws its own tint: even frames green, odd frames red
        Frame(160);
        Assert.Equal(SKColors.Lime, bmp.GetPixel(10, 10));
        Frame(161);
        Assert.Equal(SKColors.Red, bmp.GetPixel(10, 10));
        Assert.True(perFrame <= (vertexProgram ? 2700 : 1900), $"{perFrame} B per frame");
    }

    // Measured with the fix (fewest bytes of a frame): 1832 B (attribute) and 2528 B (vertex program); before it about
    // 216 B more (attribute) and a new target surface per count (vertex program, ~2.3 KB more). What is left is SkiaSharp's wrappers of the objects new vertices need: the mesh and its
    // buffers, the vertex and packed images, the shaders made from them (~330 B each) and the DrawVertices object.
}
