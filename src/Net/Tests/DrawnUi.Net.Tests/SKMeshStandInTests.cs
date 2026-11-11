using System.Runtime.InteropServices;
using DrawnUi.Draw.Meshes;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The SKMesh stand-in (DrawnUi.Draw.Meshes) on a raster canvas: geometry, modes, uniforms, varyings,
/// paint and blender rules, and the Make error paths of SkiaSharp's real SKMesh API.
/// </summary>
public class SKMeshStandInTests
{
    static readonly SKMeshSpecificationAttribute Pos = new(SKMeshSpecificationAttributeType.Float2, 0, "position");
    const string PassVs = "Varyings main(const Attributes a) { Varyings v; v.position = a.position; return v; }";
    const string RedFs = "float2 main(const Varyings v, out half4 color) { color = half4(1, 0, 0, 1); return v.position; }";

    static SKMeshVertexBuffer Vb(params float[] f) => SKMeshVertexBuffer.Make(MemoryMarshal.AsBytes(f.AsSpan()));
    static SKData Uniforms(params float[] f) => SKData.CreateCopy(MemoryMarshal.AsBytes(f.AsSpan()));

    static SKMeshSpecification Spec(SKMeshSpecificationAttribute[] attrs, int stride, SKMeshSpecificationVarying[] varyings,
        string vs, string fs)
    {
        var spec = SKMeshSpecification.Make(attrs, stride, varyings, vs, fs, SKColorSpace.CreateSrgb(), SKAlphaType.Premul, out var errors);
        Assert.True(spec != null, errors);
        return spec;
    }

    static SKBitmap Render(int w, int h, Action<SKCanvas> draw, SKColorType type = SKColorType.Rgba8888)
    {
        var bmp = new SKBitmap(new SKImageInfo(w, h, type, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Black);
        draw(canvas);
        canvas.Flush();
        return bmp;
    }

    [Fact]
    public void Triangle_FillsItsArea()
    {
        using var spec = Spec(new[] { Pos }, 8, Array.Empty<SKMeshSpecificationVarying>(), PassVs, RedFs);
        using var mesh = SKMesh.Make(spec, SKMeshMode.Triangles, Vb(10, 10, 90, 10, 10, 90), 3, 0, new SKRect(0, 0, 100, 100), out var e);
        Assert.True(mesh != null, e);
        Assert.True(mesh!.IsValid);
        using var paint = new SKPaint { Color = SKColors.White };
        using var bmp = Render(100, 100, c => c.DrawMesh(mesh, paint));

        Assert.Equal(SKColors.Red, bmp.GetPixel(20, 20));
        Assert.Equal(SKColors.Red, bmp.GetPixel(45, 45));
        Assert.Equal(SKColors.Black, bmp.GetPixel(70, 70)); // other side of the diagonal
        Assert.Equal(SKColors.Black, bmp.GetPixel(5, 5));
    }

    [Fact]
    public void IndexedQuad_UvGradient_MatchesReference()
    {
        var attrs = new[] { Pos, new SKMeshSpecificationAttribute(SKMeshSpecificationAttributeType.Float2, 8, "uv") };
        var vary = new[] { new SKMeshSpecificationVarying(SKMeshSpecificationVaryingType.Float2, "uv") };
        using var spec = Spec(attrs, 16, vary,
            "Varyings main(const Attributes a) { Varyings v; v.position = a.position; v.uv = a.uv; return v; }",
            "float2 main(const Varyings v, out half4 color) { color = half4(half2(v.uv), 0, 1); return v.position; }");
        using var vb = Vb(0, 0, 0, 0, 100, 0, 1, 0, 0, 100, 0, 1, 100, 100, 1, 1);
        using var ib = SKMeshIndexBuffer.Make(MemoryMarshal.AsBytes(new ushort[] { 0, 1, 2, 1, 3, 2 }.AsSpan()));
        using var mesh = SKMesh.MakeIndexed(spec, SKMeshMode.Triangles, vb, 4, 0, ib, 6, 0, new SKRect(0, 0, 100, 100), out var e);
        Assert.True(mesh != null, e);
        using var paint = new SKPaint { Color = SKColors.White };
        using var bmp = Render(100, 100, c => c.DrawMesh(mesh!, paint));

        foreach (var (x, y) in new[] { (5, 5), (50, 50), (90, 20), (20, 90), (97, 97), (30, 70) })
        {
            var px = bmp.GetPixel(x, y);
            Assert.InRange(px.Red, (x + 0.5) / 100 * 255 - 1.5, (x + 0.5) / 100 * 255 + 1.5);
            Assert.InRange(px.Green, (y + 0.5) / 100 * 255 - 1.5, (y + 0.5) / 100 * 255 + 1.5);
        }
    }

    [Fact]
    public void TriangleStrip_VertexProgramMovesPositions_WithUniform()
    {
        // The vertex program shifts every vertex by a uniform: positions come from running it on the CPU.
        var attrs = new[] { Pos };
        using var spec = Spec(attrs, 8, Array.Empty<SKMeshSpecificationVarying>(),
            "uniform float2 uShift; Varyings main(const Attributes a) { Varyings v; v.position = a.position + uShift; return v; }",
            RedFs);
        // strip of 2 triangles = quad 0..40 x 0..40
        using var vb = Vb(0, 0, 0, 40, 40, 0, 40, 40);
        using var mesh = SKMesh.Make(spec, SKMeshMode.TriangleStrip, vb, 4, 0, Uniforms(50, 30), new SKRect(0, 0, 100, 100), out var e);
        Assert.True(mesh != null, e);
        using var paint = new SKPaint { Color = SKColors.White };
        using var bmp = Render(100, 100, c => c.DrawMesh(mesh!, paint));

        Assert.Equal(SKColors.Black, bmp.GetPixel(20, 20));
        Assert.Equal(SKColors.Red, bmp.GetPixel(55, 35));
        Assert.Equal(SKColors.Red, bmp.GetPixel(85, 65));
        Assert.Equal(SKColors.Red, bmp.GetPixel(60, 65)); // both triangles of the strip
        Assert.Equal(SKColors.Red, bmp.GetPixel(85, 35));
        Assert.Equal(SKColors.Black, bmp.GetPixel(95, 75));
    }

    [Fact]
    public void MeshesSharingAVertexBuffer_EachUseTheirOwnUniforms()
    {
        // One static strip, drawn twice with different uniforms (the geometry built once is shared),
        // the first mesh disposed before the second draws.
        using var spec = Spec(new[] { Pos }, 8, Array.Empty<SKMeshSpecificationVarying>(),
            "uniform float2 uShift; Varyings main(const Attributes a) { Varyings v; v.position = a.position + uShift; return v; }",
            RedFs);
        using var vb = Vb(0, 0, 0, 20, 20, 0, 20, 20);
        using var paint = new SKPaint { Color = SKColors.White };
        var a = SKMesh.Make(spec, SKMeshMode.TriangleStrip, vb, 4, 0, Uniforms(0, 0), new SKRect(0, 0, 100, 100), out _)!;
        using var b = SKMesh.Make(spec, SKMeshMode.TriangleStrip, vb, 4, 0, Uniforms(60, 60), new SKRect(0, 0, 100, 100), out _)!;
        using var first = Render(100, 100, c => c.DrawMesh(a, paint));
        a.Dispose();
        using var second = Render(100, 100, c => c.DrawMesh(b, paint));

        Assert.Equal(SKColors.Red, first.GetPixel(10, 10));
        Assert.Equal(SKColors.Black, first.GetPixel(70, 70));
        Assert.Equal(SKColors.Black, second.GetPixel(10, 10));
        Assert.Equal(SKColors.Red, second.GetPixel(70, 70));
    }

    [Theory]
    [InlineData("Varyings main(const Attributes a) { Varyings v; v.position = a.position; return v; }", true)]
    [InlineData("Varyings main(const Attributes attrs) {\n Varyings o;\n o.position = attrs.position; // copy\n o.uv = attrs.uv;\n return o;\n}", true)]
    [InlineData("uniform float t; Varyings main(const Attributes a) { Varyings v; v.position = a.position + t; return v; }", false)]
    [InlineData("Varyings main(const Attributes a) { Varyings v; v.position = a.position; v.position.x += 1; return v; }", false)]
    [InlineData("void f(inout Varyings x) { x.position *= 2; } Varyings main(const Attributes a) { Varyings v; v.position = a.position; f(v); return v; }", false)]
    [InlineData("Varyings main(const Attributes a) { Varyings v; v.position = a.uv; return v; }", false)]
    public void PositionsTakenFromTheAttribute_OnlyWhenTheVertexProgramCopiesIt(string vs, bool copies)
    {
        var attrs = new[] { Pos, new SKMeshSpecificationAttribute(SKMeshSpecificationAttributeType.Float2, 8, "uv") };
        var vary = new[] { new SKMeshSpecificationVarying(SKMeshSpecificationVaryingType.Float2, "uv") };
        using var spec = Spec(attrs, 16, vary, vs.Contains("o.uv") ? vs : vs.Replace("return v;", "v.uv = a.uv; return v;"), RedFs);
        var positionProgram = typeof(SKMeshSpecification)
            .GetProperty("PositionEffect", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(spec);
        Assert.Equal(copies, positionProgram == null);
    }

    [Fact]
    public void Uniforms_SharedBetweenPrograms_LaidOutVertexFirst()
    {
        // t in both programs (one slot), k only in VS, c only in FS: layout t, k, c = 4 + 8 + 16 bytes.
        using var spec = Spec(new[] { Pos }, 8, Array.Empty<SKMeshSpecificationVarying>(),
            "uniform float t; uniform float2 k; Varyings main(const Attributes a) { Varyings v; v.position = a.position * t + k; return v; }",
            "uniform float4 c; uniform float t; float2 main(const Varyings v, out half4 color) { color = half4(c * t); return v.position; }");
        Assert.Equal(28, spec.UniformSize);
        using var mesh = SKMesh.Make(spec, SKMeshMode.Triangles, Vb(0, 0, 50, 0, 0, 50), 3, 0,
            Uniforms(1, 10, 10, 0.5f, 0.25f, 1, 1), new SKRect(0, 0, 100, 100), out var e);
        Assert.True(mesh != null, e);
        using var paint = new SKPaint { Color = SKColors.White };
        using var bmp = Render(100, 100, c => c.DrawMesh(mesh!, paint));

        Assert.Equal(SKColors.Black, bmp.GetPixel(5, 5)); // moved by k
        var px = bmp.GetPixel(20, 20);
        Assert.InRange(px.Red, 126, 129);
        Assert.InRange(px.Green, 63, 65);
        Assert.Equal(255, px.Blue);
    }

    [Fact]
    public void ThreeFloat4Varyings_InterpolateLikeACpuReference()
    {
        // Dungeon layout: position + a, b, c float4 attributes, a, b, c float4 varyings, one selected per draw.
        var attrs = new[]
        {
            Pos,
            new SKMeshSpecificationAttribute(SKMeshSpecificationAttributeType.Float4, 8, "a"),
            new SKMeshSpecificationAttribute(SKMeshSpecificationAttributeType.Float4, 24, "b"),
            new SKMeshSpecificationAttribute(SKMeshSpecificationAttributeType.Float4, 40, "c"),
        };
        var vary = new[]
        {
            new SKMeshSpecificationVarying(SKMeshSpecificationVaryingType.Float4, "a"),
            new SKMeshSpecificationVarying(SKMeshSpecificationVaryingType.Float4, "b"),
            new SKMeshSpecificationVarying(SKMeshSpecificationVaryingType.Float4, "c"),
        };
        using var spec = Spec(attrs, 56, vary,
            "Varyings main(const Attributes a) { Varyings v; v.position = a.position; v.a = a.a; v.b = a.b; v.c = a.c; return v; }",
            "uniform float which; float2 main(const Varyings v, out float4 color) { color = which < 0.5 ? v.a : which < 1.5 ? v.b : v.c; return v.position; }");

        var rnd = new Random(7);
        var p = new[] { new SKPoint(3, 4), new SKPoint(61, 9), new SKPoint(20, 60) };
        var values = new float[3, 12];
        for (int k = 0; k < 3; k++)
            for (int j = 0; j < 12; j++)
                values[k, j] = j % 4 == 3 ? 1 : (float)(0.05 + 0.9 * rnd.NextDouble()); // alpha 1: premul rgb = raw value
        var floats = new List<float>();
        for (int k = 0; k < 3; k++)
        {
            floats.Add(p[k].X);
            floats.Add(p[k].Y);
            for (int j = 0; j < 12; j++)
                floats.Add(values[k, j]);
        }
        using var vb = Vb(floats.ToArray());

        double area = Cross(p[0], p[1], p[2]);
        int checkedPixels = 0;
        double maxError = 0;
        for (int which = 0; which < 3; which++)
        {
            using var mesh = SKMesh.Make(spec, SKMeshMode.Triangles, vb, 3, 0, Uniforms(which), new SKRect(0, 0, 64, 64), out var e);
            Assert.True(mesh != null, e);
            using var paint = new SKPaint { Color = SKColors.White };
            using var bmp = Render(64, 64, c => c.DrawMesh(mesh!, paint), SKColorType.RgbaF32);
            var pixels = MemoryMarshal.Cast<byte, float>(bmp.GetPixelSpan());
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    var q = new SKPoint(x + 0.5f, y + 0.5f);
                    double w0 = Cross(q, p[1], p[2]) / area, w1 = Cross(p[0], q, p[2]) / area, w2 = Cross(p[0], p[1], q) / area;
                    if (w0 < 0.02 || w1 < 0.02 || w2 < 0.02)
                        continue; // edge pixels: coverage rules, not interpolation
                    for (int ch = 0; ch < 3; ch++)
                    {
                        int j = which * 4 + ch;
                        double expected = w0 * values[0, j] + w1 * values[1, j] + w2 * values[2, j];
                        double got = pixels[(y * 64 + x) * 4 + ch];
                        maxError = Math.Max(maxError, Math.Abs(got - expected));
                    }
                    checkedPixels++;
                }
        }
        Assert.True(checkedPixels > 1500, $"checked {checkedPixels}");
        Assert.True(maxError < 2e-3, $"max error {maxError}");
    }

    static double Cross(SKPoint a, SKPoint b, SKPoint c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    [Fact]
    public void LargePositions_KeepFullPrecision()
    {
        // Attributes travel as fp16 hi + lo: a position near 3000 must still interpolate to the pixel.
        var vary = new[] { new SKMeshSpecificationVarying(SKMeshSpecificationVaryingType.Float2, "p") };
        using var spec = Spec(new[] { Pos }, 8, vary,
            "Varyings main(const Attributes a) { Varyings v; v.position = a.position; v.p = a.position; return v; }",
            "float2 main(const Varyings v, out float4 color) { color = float4(fract(v.p.x), fract(v.p.y), 0, 1); return v.position; }");
        using var vb = Vb(3000, 3000, 3064, 3000, 3000, 3064);
        using var mesh = SKMesh.Make(spec, SKMeshMode.Triangles, vb, 3, 0, new SKRect(3000, 3000, 3064, 3064), out var e);
        Assert.True(mesh != null, e);
        using var paint = new SKPaint { Color = SKColors.White };
        using var bmp = Render(64, 64, c => { c.Translate(-3000, -3000); c.DrawMesh(mesh!, paint); }, SKColorType.RgbaF32);
        var pixels = MemoryMarshal.Cast<byte, float>(bmp.GetPixelSpan());
        for (int y = 2; y < 30; y += 3)
            for (int x = 2; x < 30; x += 3)
            {
                Assert.InRange(pixels[(y * 64 + x) * 4], 0.5 - 0.01, 0.5 + 0.01);
                Assert.InRange(pixels[(y * 64 + x) * 4 + 1], 0.5 - 0.01, 0.5 + 0.01);
            }
    }

    [Fact]
    public void ManyTriangles_EachGetsItsOwnVertices()
    {
        // 12k triangles in a grid of 2x2 px cells, each triangle colored by its own Ubyte4Unorm attribute.
        const int cols = 120, rows = 50;
        var attrs = new[] { Pos, new SKMeshSpecificationAttribute(SKMeshSpecificationAttributeType.Ubyte4Unorm, 8, "col") };
        var vary = new[] { new SKMeshSpecificationVarying(SKMeshSpecificationVaryingType.Half4, "c") };
        using var spec = Spec(attrs, 12, vary,
            "Varyings main(const Attributes a) { Varyings v; v.position = a.position; v.c = a.col; return v; }",
            "float2 main(const Varyings v, out half4 color) { color = v.c; return v.position; }");
        var bytes = new List<byte>();
        void Vertex(float x, float y, byte r, byte g)
        {
            bytes.AddRange(BitConverter.GetBytes(x));
            bytes.AddRange(BitConverter.GetBytes(y));
            bytes.AddRange(new byte[] { r, g, 0, 255 });
        }
        for (int j = 0; j < rows; j++)
            for (int i = 0; i < cols; i++)
            {
                float x = i * 4, y = j * 4;
                byte r = (byte)(i * 2), g = (byte)(j * 5);
                Vertex(x, y, r, g); Vertex(x + 4, y, r, g); Vertex(x, y + 4, r, g);
                Vertex(x + 4, y, r, g); Vertex(x + 4, y + 4, r, g); Vertex(x, y + 4, r, g);
            }
        using var vb = SKMeshVertexBuffer.Make(bytes.ToArray());
        int count = cols * rows * 6;
        using var mesh = SKMesh.Make(spec, SKMeshMode.Triangles, vb, count, 0, new SKRect(0, 0, cols * 4, rows * 4), out var e);
        Assert.True(mesh != null, e);
        using var paint = new SKPaint { Color = SKColors.White };
        using var bmp = Render(cols * 4, rows * 4, c => c.DrawMesh(mesh!, paint));
        for (int j = 0; j < rows; j += 7)
            for (int i = 0; i < cols; i += 11)
            {
                var px = bmp.GetPixel(i * 4 + 1, j * 4 + 2);
                Assert.Equal((byte)(i * 2), px.Red);
                Assert.Equal((byte)(j * 5), px.Green);
            }
    }

    [Fact]
    public void NoColorOutput_LocalCoordsSampleThePaintShader()
    {
        using var spec = SKMeshSpecification.Make(new[] { Pos }, 8, Array.Empty<SKMeshSpecificationVarying>(), PassVs,
            "float2 main(const Varyings v) { return v.position; }", out var se);
        Assert.True(spec != null, se);
        using var mesh = SKMesh.Make(spec!, SKMeshMode.Triangles, Vb(0, 0, 100, 0, 0, 100), 3, 0, new SKRect(0, 0, 100, 100), out var e);
        Assert.True(mesh != null, e);
        using var gradient = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(100, 0),
            new[] { SKColors.Blue, SKColors.Yellow }, SKShaderTileMode.Clamp);
        using var paint = new SKPaint { Shader = gradient };
        using var bmp = Render(100, 100, c => c.DrawMesh(mesh!, paint));
        using var reference = Render(100, 100, c => c.DrawRect(0, 0, 100, 100, paint));
        foreach (var (x, y) in new[] { (5, 5), (40, 20), (70, 10), (10, 70) })
            AssertClose(reference.GetPixel(x, y), bmp.GetPixel(x, y), 2);

        // no shader, no color output: the paint color
        using var solid = new SKPaint { Color = SKColors.Lime };
        using var bmp2 = Render(100, 100, c => c.DrawMesh(mesh!, solid));
        Assert.Equal(SKColors.Lime, bmp2.GetPixel(20, 20));
    }

    [Fact]
    public void ColorOutput_ModulatesPaintAndHonoursBlender()
    {
        using var spec = Spec(new[] { Pos }, 8, Array.Empty<SKMeshSpecificationVarying>(), PassVs,
            "float2 main(const Varyings v, out half4 color) { color = half4(1, 0.5, 0.25, 1); return v.position; }");
        using var mesh = SKMesh.Make(spec, SKMeshMode.Triangles, Vb(0, 0, 100, 0, 0, 100), 3, 0, new SKRect(0, 0, 100, 100), out var e);
        Assert.True(mesh != null, e);

        // default Modulate: mesh color * paint color
        using var gray = new SKPaint { Color = new SKColor(128, 255, 255) };
        using var a = Render(100, 100, c => c.DrawMesh(mesh!, gray));
        AssertClose(new SKColor(128, 128, 64), a.GetPixel(20, 20), 2);

        // Dst: the mesh color alone, Src: the paint alone
        using var dst = SKBlender.CreateBlendMode(SKBlendMode.Dst);
        using var b = Render(100, 100, c => c.DrawMesh(mesh!, dst, gray));
        AssertClose(new SKColor(255, 128, 64), b.GetPixel(20, 20), 2);
        using var src = SKBlender.CreateBlendMode(SKBlendMode.Src);
        using var s = Render(100, 100, c => c.DrawMesh(mesh!, src, gray));
        AssertClose(new SKColor(128, 255, 255), s.GetPixel(20, 20), 2);

        // paint alpha with a shader: applied after the blend
        using var shader = SKShader.CreateColor(SKColors.White);
        using var half = new SKPaint { Shader = shader, Color = SKColors.White.WithAlpha(128) };
        using var h = Render(100, 100, c => c.DrawMesh(mesh!, half));
        AssertClose(new SKColor(128, 64, 32), h.GetPixel(20, 20), 2);
    }

    [Fact]
    public void Children_PassedInFragmentOrder()
    {
        using var spec = Spec(new[] { Pos }, 8, Array.Empty<SKMeshSpecificationVarying>(), PassVs,
            "uniform shader s1; uniform shader s2; float2 main(const Varyings v, out half4 color) { color = v.position.x < 50 ? s1.eval(v.position) : s2.eval(v.position); return v.position; }");
        using var red = SKShader.CreateColor(SKColors.Red);
        using var blue = SKShader.CreateColor(SKColors.Blue);
        using var mesh = SKMesh.Make(spec, SKMeshMode.Triangles, Vb(0, 0, 100, 0, 0, 100), 3, 0, null,
            new SKRuntimeEffectChild[] { red, blue }, new SKRect(0, 0, 100, 100), out var e);
        Assert.True(mesh != null, e);
        using var paint = new SKPaint { Color = SKColors.White };
        using var bmp = Render(100, 100, c => c.DrawMesh(mesh!, paint));
        Assert.Equal(SKColors.Red, bmp.GetPixel(20, 20));
        Assert.Equal(SKColors.Blue, bmp.GetPixel(60, 20));
    }

    [Fact]
    public void BoundsOutsideTheClip_SkipTheDraw()
    {
        using var spec = Spec(new[] { Pos }, 8, Array.Empty<SKMeshSpecificationVarying>(), PassVs, RedFs);
        // bounds lie far away: the draw is culled although the triangle itself is on screen
        using var mesh = SKMesh.Make(spec, SKMeshMode.Triangles, Vb(10, 10, 90, 10, 10, 90), 3, 0, new SKRect(500, 500, 600, 600), out var e);
        Assert.True(mesh != null, e);
        using var paint = new SKPaint { Color = SKColors.White };
        using var bmp = Render(100, 100, c => c.DrawMesh(mesh!, paint));
        Assert.Equal(SKColors.Black, bmp.GetPixel(20, 20));
    }

    [Fact]
    public void SameHelperNameInBothPrograms_Compiles()
    {
        using var spec = Spec(new[] { Pos }, 8, Array.Empty<SKMeshSpecificationVarying>(),
            "float h(float x) { return x; } Varyings main(const Attributes a) { Varyings v; v.position = a.position * h(1); return v; }",
            "float h(float x) { return x * 0.5; } float2 main(const Varyings v, out half4 color) { color = half4(h(1), 0, 0, 1); return v.position; }");
        using var mesh = SKMesh.Make(spec, SKMeshMode.Triangles, Vb(0, 0, 100, 0, 0, 100), 3, 0, new SKRect(0, 0, 100, 100), out var e);
        using var paint = new SKPaint { Color = SKColors.White };
        using var bmp = Render(100, 100, c => c.DrawMesh(mesh!, paint));
        AssertClose(new SKColor(128, 0, 0), bmp.GetPixel(20, 20), 2);
    }

    static void AssertClose(SKColor expected, SKColor actual, int tolerance)
    {
        Assert.True(Math.Abs(expected.Red - actual.Red) <= tolerance && Math.Abs(expected.Green - actual.Green) <= tolerance
                    && Math.Abs(expected.Blue - actual.Blue) <= tolerance && Math.Abs(expected.Alpha - actual.Alpha) <= tolerance,
            $"expected {expected}, got {actual}");
    }

    // ---- Make error paths: the same messages SkiaSharp's SKMesh returns (4.147.0-pr.3779.2) ----

    static string SpecError(SKMeshSpecificationAttribute[] attrs, int stride, SKMeshSpecificationVarying[] vary, string vs, string fs, bool colorSpace = true)
    {
        var spec = colorSpace
            ? SKMeshSpecification.Make(attrs, stride, vary, vs, fs, SKColorSpace.CreateSrgb(), SKAlphaType.Premul, out var e)
            : SKMeshSpecification.Make(attrs, stride, vary, vs, fs, out e);
        Assert.Null(spec);
        return e;
    }

    static SKMeshSpecificationVarying V(string name, SKMeshSpecificationVaryingType type = SKMeshSpecificationVaryingType.Float) => new(type, name);

    [Fact]
    public void SpecificationErrors_MatchSkia()
    {
        var none = Array.Empty<SKMeshSpecificationVarying>();
        Assert.Throws<ArgumentException>(() => SKMeshSpecification.Make(Array.Empty<SKMeshSpecificationAttribute>(), 8, none, PassVs, RedFs, out _));
        Assert.Equal("A maximum of 8 attributes is allowed.", SpecError(Enumerable.Range(0, 9)
            .Select(i => new SKMeshSpecificationAttribute(SKMeshSpecificationAttributeType.Float, i * 4, "a" + i)).ToArray(), 40, none, PassVs, RedFs));
        Assert.Equal("Vertex stride must be a non-zero multiple of 4.", SpecError(new[] { Pos }, 6, none, PassVs, RedFs));
        Assert.Equal("Stride cannot exceed 1024.", SpecError(new[] { Pos }, 2048, none, PassVs, RedFs));
        Assert.Equal("Attribute offset must be a multiple of 4.", SpecError(new[] { new SKMeshSpecificationAttribute(SKMeshSpecificationAttributeType.Float2, 2, "position") }, 12, none, PassVs, RedFs));
        Assert.Equal("Attribute offset plus size cannot exceed stride.", SpecError(new[] { new SKMeshSpecificationAttribute(SKMeshSpecificationAttributeType.Float2, 4, "position") }, 8, none, PassVs, RedFs));
        Assert.Equal("\"\" is not a valid attribute name.", SpecError(new[] { Pos, new SKMeshSpecificationAttribute(SKMeshSpecificationAttributeType.Float, 0, "") }, 8, none, PassVs, RedFs));
        Assert.Equal("A maximum of 6 varyings is allowed.", SpecError(new[] { Pos }, 8, Enumerable.Range(0, 6).Select(i => V("v" + i)).ToArray(), PassVs, RedFs));
        Assert.NotNull(SKMeshSpecification.Make(new[] { Pos }, 8, Enumerable.Range(0, 5).Select(i => V("v" + i)).Append(V("position", SKMeshSpecificationVaryingType.Float2)).ToArray(),
            PassVs, RedFs, SKColorSpace.CreateSrgb(), SKAlphaType.Premul, out _));
        Assert.Equal("Varying \"position\" must have type float2.", SpecError(new[] { Pos }, 8, new[] { V("position", SKMeshSpecificationVaryingType.Half2) }, PassVs, RedFs));
        Assert.Equal("\"\" is not a valid varying name.", SpecError(new[] { Pos }, 8, new[] { V("") }, PassVs, RedFs));
        Assert.Equal("Must provide a color space if FS returns a color.", SpecError(new[] { Pos }, 8, none, PassVs, RedFs, colorSpace: false));
        Assert.Null(SKMeshSpecification.Make(new[] { Pos }, 8, none, PassVs, RedFs, SKColorSpace.CreateSrgb(), SKAlphaType.Unknown, out var unknown));
        Assert.Equal("Must provide a valid alpha type if FS returns a color.", unknown);
        Assert.Equal("No main function found.", SpecError(new[] { Pos }, 8, none, "Varyings f(const Attributes a) { Varyings v; v.position = a.position; return v; }", RedFs));
        Assert.StartsWith("VS: error:", SpecError(new[] { Pos }, 8, none, "Varyings main(const Attributes a) { return 1; }", RedFs));
        Assert.StartsWith("FS: error:", SpecError(new[] { Pos }, 8, none, PassVs, "float2 main(const Varyings v) { return v.nope; }"));
        Assert.StartsWith("VS: error: effects are not permitted in mesh vertex shaders",
            SpecError(new[] { Pos }, 8, none, "uniform shader s; " + PassVs, RedFs));
        Assert.Equal("Uniform t declared with different types in vertex and fragment shaders.", SpecError(new[] { Pos }, 8, none,
            "uniform float t; Varyings main(const Attributes a) { Varyings v; v.position = a.position * t; return v; }",
            "uniform float2 t; float2 main(const Varyings v) { return v.position * t; }"));
    }

    [Fact]
    public void MeshErrors_MatchSkia()
    {
        var none = Array.Empty<SKMeshSpecificationVarying>();
        using var spec = Spec(new[] { Pos }, 8, none, PassVs, RedFs);
        using var uspec = Spec(new[] { Pos }, 8, none,
            "uniform float t; Varyings main(const Attributes a) { Varyings v; v.position = a.position * t; return v; }", RedFs);
        using var cspec = Spec(new[] { Pos }, 8, none, PassVs,
            "uniform shader s1; float2 main(const Varyings v, out half4 color) { color = s1.eval(v.position); return v.position; }");
        using var vb = Vb(0, 0, 10, 0, 0, 10, 10, 10);
        using var ib = SKMeshIndexBuffer.Make(MemoryMarshal.AsBytes(new ushort[] { 0, 1, 2, 1, 2, 3 }.AsSpan()));
        var b = new SKRect(0, 0, 10, 10);

        string Err(SKMesh? m, string e) { Assert.Null(m); return e; }

        Assert.Equal("triangles mode requires at least 3 vertices but vertex count is 2.", Err(SKMesh.Make(spec, SKMeshMode.Triangles, vb, 2, 0, b, out var e1), e1));
        Assert.Equal("triangle-strip mode requires at least 3 vertices but vertex count is 2.", Err(SKMesh.Make(spec, SKMeshMode.TriangleStrip, vb, 2, 0, b, out var e2), e2));
        Assert.Equal("The vertex buffer offset and vertex count reads beyond the end of the vertex buffer.", Err(SKMesh.Make(spec, SKMeshMode.Triangles, vb, 5, 0, b, out var e3), e3));
        Assert.Equal("The vertex offset (4) must be a multiple of the vertex stride (8).", Err(SKMesh.Make(spec, SKMeshMode.Triangles, vb, 3, 4, b, out var e4), e4));
        Assert.NotNull(SKMesh.Make(spec, SKMeshMode.Triangles, vb, 3, 8, b, out _));
        Assert.Throws<ArgumentNullException>(() => SKMesh.Make(spec, SKMeshMode.Triangles, null!, 3, 0, b, out _));
        Assert.Equal("The uniform data is 0 bytes but must be at least 4.", Err(SKMesh.Make(uspec, SKMeshMode.Triangles, vb, 3, 0, b, out var e5), e5));
        Assert.Equal("The uniform data is 2 bytes but must be at least 4.", Err(SKMesh.Make(uspec, SKMeshMode.Triangles, vb, 3, 0, SKData.CreateCopy(new byte[2]), b, out var e6), e6));
        Assert.NotNull(SKMesh.Make(uspec, SKMeshMode.Triangles, vb, 3, 0, SKData.CreateCopy(new byte[8]), b, out _));
        Assert.Equal("The mesh specification declares 1 child effects, but the mesh supplies 0.", Err(SKMesh.Make(cspec, SKMeshMode.Triangles, vb, 3, 0, null, b, out var e7), e7));
        using var red = SKShader.CreateColor(SKColors.Red);
        Assert.Equal("The mesh specification declares 1 child effects, but the mesh supplies 2.",
            Err(SKMesh.Make(cspec, SKMeshMode.Triangles, vb, 3, 0, null, new SKRuntimeEffectChild[] { red, red }, b, out var e8), e8));
        using var filter = SKColorFilter.CreateBlendMode(SKColors.Red, SKBlendMode.Src);
        Assert.Equal("Child effect 's1' was specified as a shader, but passed as a color filter.",
            Err(SKMesh.Make(cspec, SKMeshMode.Triangles, vb, 3, 0, null, new SKRuntimeEffectChild[] { filter }, b, out var e9), e9));
        Assert.NotNull(SKMesh.Make(cspec, SKMeshMode.Triangles, vb, 3, 0, null, new SKRuntimeEffectChild[] { new((SKShader)null!) }, b, out _));

        Assert.Equal("triangles mode requires at least 3 indices but index count is 2.", Err(SKMesh.MakeIndexed(spec, SKMeshMode.Triangles, vb, 4, 0, ib, 2, 0, b, out var i1), i1));
        Assert.Equal("The index buffer offset and index count reads beyond the end of the index buffer.", Err(SKMesh.MakeIndexed(spec, SKMeshMode.Triangles, vb, 4, 0, ib, 7, 0, b, out var i2), i2));
        Assert.Equal("The index offset must be a multiple of 2.", Err(SKMesh.MakeIndexed(spec, SKMeshMode.Triangles, vb, 4, 0, ib, 3, 1, b, out var i3), i3));
        Assert.NotNull(SKMesh.MakeIndexed(spec, SKMeshMode.Triangles, vb, 4, 0, ib, 5, 0, b, out _));
        Assert.NotNull(SKMesh.MakeIndexed(spec, SKMeshMode.Triangles, vb, 0, 0, ib, 3, 0, b, out _));
        Assert.Throws<ArgumentNullException>(() => SKMesh.MakeIndexed(spec, SKMeshMode.Triangles, vb, 4, 0, null!, 3, 0, b, out _));
    }
}
