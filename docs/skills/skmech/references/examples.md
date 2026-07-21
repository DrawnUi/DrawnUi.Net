# Usage Examples

Add these namespaces when needed:

```csharp
using System;
using System.Runtime.InteropServices;
using SkiaSharp;
```

## Example 1: smallest working triangle

This is the first example to use when proving the API works.

Two pre-conditions to verify before this code can produce a visible triangle:

1. The drawing surface must be GPU-backed. Raster surfaces silently no-op `DrawMesh` in this build (see "Backend support" in `SKILL.md`).
2. (Blazor WASM) the `SkiaSharp` assembly must be rooted against the trimmer — see "Blazor WebAssembly host setup" at the bottom of this file.

```csharp
var attributes = new[]
{
    new SKMeshSpecificationAttribute(
        SKMeshSpecificationAttributeType.Float2,
        0,
        "position"),
};

var varyings = Array.Empty<SKMeshSpecificationVarying>();

const string vertexShader = @"
    Varyings main(const Attributes attrs) {
        Varyings v;
        v.position = attrs.position;
        return v;
    }";

const string fragmentShader = @"
    float2 main(const Varyings varyings, out half4 color) {
        color = half4(1.0, 0.0, 0.0, 1.0);
        return varyings.position;
    }";

using var spec = SKMeshSpecification.Make(
    attributes,
    sizeof(float) * 2,
    varyings,
    vertexShader,
    fragmentShader,
    SKColorSpace.CreateSrgb(),
    SKAlphaType.Premul,
    out var specErrors);

if (spec == null)
    throw new InvalidOperationException(specErrors);

var vertices = new float[]
{
    10, 10,
    110, 10,
    60, 90,
};

using var vertexBuffer = SKMeshVertexBuffer.Make(MemoryMarshal.AsBytes(vertices.AsSpan()));

using var mesh = SKMesh.Make(
    spec,
    SKMeshMode.Triangles,
    vertexBuffer,
    vertexCount: 3,
    vertexOffset: 0,
    bounds: new SKRect(0, 0, 120, 100),
    out var meshErrors);

if (mesh == null)
    throw new InvalidOperationException(meshErrors);

// Draw onto a GPU surface. The raster overload below is convenient but
// produces NO visible mesh on raster — this is a known no-op of DrawMesh
// on the CPU backend in 4.147.0-pr.3779.2. For an off-screen GPU surface use
// SKSurface.Create(GRRecordingContext, ...) with a real GR backend.
using var paint = new SKPaint();
canvas.DrawMesh(mesh, paint);
```

## Example 2: indexed quad

Use this when you want two triangles sharing vertices.

```csharp
using var spec = SKMeshSpecification.Make(
    new[]
    {
        new SKMeshSpecificationAttribute(SKMeshSpecificationAttributeType.Float2, 0, "position"),
    },
    sizeof(float) * 2,
    Array.Empty<SKMeshSpecificationVarying>(),
    @"
        Varyings main(const Attributes attrs) {
            Varyings v;
            v.position = attrs.position;
            return v;
        }",
    @"
        float2 main(const Varyings varyings, out half4 color) {
            color = half4(0.1, 0.5, 1.0, 1.0);
            return varyings.position;
        }",
    SKColorSpace.CreateSrgb(),
    SKAlphaType.Premul,
    out var specErrors);

if (spec == null)
    throw new InvalidOperationException(specErrors);

var vertices = new float[]
{
    10, 10,
    110, 10,
    110, 110,
    10, 110,
};

var indices = new ushort[]
{
    0, 1, 2,
    0, 2, 3,
};

using var vb = SKMeshVertexBuffer.Make(MemoryMarshal.AsBytes(vertices.AsSpan()));
using var ib = SKMeshIndexBuffer.Make(MemoryMarshal.AsBytes(indices.AsSpan()));

using var mesh = SKMesh.MakeIndexed(
    spec,
    SKMeshMode.Triangles,
    vb,
    vertexCount: 4,
    vertexOffset: 0,
    ib,
    indexCount: 6,
    indexOffset: 0,
    bounds: new SKRect(0, 0, 120, 120),
    out var meshErrors);

if (mesh == null)
    throw new InvalidOperationException(meshErrors);
```

## Example 3: adding a custom varying

Use a custom varying when the fragment shader needs interpolated data beyond position.

```csharp
var attributes = new[]
{
    new SKMeshSpecificationAttribute(SKMeshSpecificationAttributeType.Float2, 0, "position"),
    new SKMeshSpecificationAttribute(SKMeshSpecificationAttributeType.Float2, 8, "uv"),
};

var varyings = new[]
{
    new SKMeshSpecificationVarying(SKMeshSpecificationVaryingType.Float2, "uv"),
};

const string vertexShader = @"
    Varyings main(const Attributes attrs) {
        Varyings v;
        v.position = attrs.position;
        v.uv = attrs.uv;
        return v;
    }";

const string fragmentShader = @"
    float2 main(const Varyings varyings, out half4 color) {
        color = half4(varyings.uv.x, varyings.uv.y, 1.0 - varyings.uv.x, 1.0);
        return varyings.position;
    }";

var stride = sizeof(float) * 4;
```

The packed vertex layout for that example is:

```text
position.x position.y uv.x uv.y
```

## Example 4: uniforms and children

Advanced mesh creation overloads accept:

- `SKData uniforms`
- `ReadOnlySpan<SKRuntimeEffectChild> children`

Use them only after the basic examples work. The important rule is that the uniform byte layout must exactly match `spec.UniformSize` and the shader declarations.

Sketch:

```csharp
byte[] uniformBytes = BuildUniformBlobSomehow(spec.UniformSize);
using var uniforms = SKData.CreateCopy(uniformBytes);

SKRuntimeEffectChild[] children =
[
    someShader,
    someBlender,
];

using var mesh = SKMesh.Make(
    spec,
    SKMeshMode.Triangles,
    vb,
    vertexCount,
    0,
    uniforms,
    children,
    bounds,
    out var errors);
```

Do not start here when debugging. Start from Example 1.

## Debugging checklist

If `spec` is null:

1. print `specErrors`
2. reduce to one `Float2 position` attribute
3. use the exact minimal shader signatures from Example 1
4. remove custom varyings

If `mesh` is null:

1. print `meshErrors`
2. verify `vertexCount` and `indexCount`
3. verify `vertexOffset` and `indexOffset`
4. verify index data is `ushort[]`
5. verify bounds contain every generated vertex

If it draws but looks wrong:

1. return `varyings.position` from fragment shader
2. output a flat color first
3. replace indexed geometry with a single non-indexed triangle
4. verify the packed float layout matches attribute offsets exactly

## When to suggest alternatives

Do not reach for `SKMesh` if the user only needs:

- a regular textured quad without custom interpolation
- ordinary path drawing
- simple colored triangles already covered by `SKVertices`

Use `SKMesh` when they specifically need programmable vertex + fragment control, SkSL-based geometry effects, or a 2.5D style transformation pipeline.

## Blazor WebAssembly host setup (DrawnUI)

Verified working configuration in `BlazorSandbox/Pages/SkMeshProbe.razor` with `SkiaSharp 4.147.0-pr.3779.2`. Two host-side changes are required, both already documented in `SKILL.md`:

### 1. Root SkiaSharp against the trimmer

In the Blazor WASM host `.csproj` (the project that builds the `wwwroot/_framework` bundle, e.g. `BlazorSandbox.csproj`):

```xml
<ItemGroup>
  <TrimmerRootAssembly Include="SkiaSharp" RootMode="EntireAssembly" />
</ItemGroup>
```

Without this, `SkiaSharp.wasm` ships without `SKMesh*` types and the runtime throws `System.TypeLoadException` for any of them. After adding it, delete `bin/` and `obj/` of the host project before rebuilding (webcil output is content-hashed and the trimmed cache is otherwise reused).

### 2. Use the GPU-accelerated DrawnUI Canvas

```razor
<Canvas
    WidthRequest="420"
    HeightRequest="700"
    BackgroundColor="#1A1A2E"
    RenderingMode="@RenderingModeType.Accelerated"
    RootControl="@RootControl" />
```

`RenderingModeType.Default` is CPU raster — `DrawMesh` is silent no-op. `RenderingModeType.Accelerated` routes through `SkiaViewAccelerated` which uses a WebGL `GRContext` where `DrawMesh` rasterises correctly.

### Minimal verified `SkiaControl.Paint` body

```csharp
protected override void Paint(DrawingContext ctx)
{
    base.Paint(ctx);
    var canvas = ctx.Context.Canvas;
    var dest = ctx.Destination;

    float cx = dest.MidX;
    float cy = dest.MidY;
    float r  = Math.Min(dest.Width, dest.Height) * 0.30f;

    var attributes = new[]
    {
        new SKMeshSpecificationAttribute(SKMeshSpecificationAttributeType.Float2, 0, "position"),
    };
    const string vert = "Varyings main(const Attributes attrs) { Varyings v; v.position = attrs.position; return v; }";
    const string frag = "float2 main(const Varyings varyings, out half4 color) { color = half4(1.0, 1.0, 0.0, 1.0); return varyings.position; }";

    using var spec = SKMeshSpecification.Make(
        attributes,
        sizeof(float) * 2,
        Array.Empty<SKMeshSpecificationVarying>(),
        vert,
        frag,
        SKColorSpace.CreateSrgb(),  // required because frag has `out half4 color`
        SKAlphaType.Premul,
        out var specErr);
    if (spec == null) { Console.WriteLine(specErr); return; }

    var verts = new float[]
    {
        cx,     cy - r,
        cx + r, cy + r,
        cx - r, cy + r,
    };
    using var vb = SKMeshVertexBuffer.Make(MemoryMarshal.AsBytes(verts.AsSpan()));
    var bounds = new SKRect(cx - r, cy - r, cx + r, cy + r);

    using var mesh = SKMesh.Make(spec, SKMeshMode.Triangles, vb, 3, 0, bounds, out var meshErr);
    if (mesh == null) { Console.WriteLine(meshErr); return; }

    using var paint = new SKPaint { IsAntialias = true, Color = SKColors.White };
    canvas.DrawMesh(mesh, paint);
}
```

This produces a solid yellow triangle on the canvas. Confirmed in browser via Playwright on `http://localhost:5055/skmesh-probe`.

### Animated wave with uniform (Blazor WASM-safe, non-indexed)

Uses `SKMesh.Make` with `SKMeshMode.TriangleStrip`, a custom `Float2 uv` varying, and three `float` uniforms packed into `SKData`. Animated by a `PerpetualPendulumAnimator` that updates `uTime` and calls `Update()` each frame. Verified working in `BlazorSandbox/Pages/SkMeshProbe.razor`.

```csharp
private class AnimatedWaveMesh : SkiaControl
{
    private PerpetualPendulumAnimator _animator;
    private double _phase;
    private const int Cols = 24;

    protected override void OnLayoutReady()
    {
        base.OnLayoutReady();
        if (_animator != null) return;
        _animator = new PerpetualPendulumAnimator(this, v => { _phase = v; Update(); })
        {
            Amplitude = 6.28, // ~2*PI
            Speed = 1500,
        };
        _animator.Start();
    }

    protected override void Paint(DrawingContext ctx)
    {
        base.Paint(ctx);
        var canvas = ctx.Context.Canvas;
        var dest = ctx.Destination;
        if (dest.Width <= 0 || dest.Height <= 0) return;

        float pad = 20f;
        float l = dest.Left + pad, t = dest.Top + pad, r = dest.Right - pad, b = dest.Bottom - pad;

        var attrs = new[]
        {
            new SKMeshSpecificationAttribute(SKMeshSpecificationAttributeType.Float2, 0, "position"),
            new SKMeshSpecificationAttribute(SKMeshSpecificationAttributeType.Float2, 8, "uv"),
        };
        var varyings = new[]
        {
            new SKMeshSpecificationVarying(SKMeshSpecificationVaryingType.Float2, "uv"),
        };

        // SkSL uniforms must be declared OUTSIDE main, before it.
        const string vert = @"
            uniform float uTime;
            uniform float uAmp;
            uniform float uFreq;
            Varyings main(const Attributes a) {
                Varyings v;
                float dy = sin(a.position.x * uFreq + uTime) * uAmp;
                v.position = a.position + float2(0.0, dy);
                v.uv = a.uv;
                return v;
            }";
        const string frag = @"
            float2 main(const Varyings v, out half4 color) {
                float band = abs(sin(v.uv.x * 12.0));
                color = half4(band, 0.4 + 0.5 * v.uv.y, 1.0 - band, 1.0);
                return v.position;
            }";

        using var spec = SKMeshSpecification.Make(
            attrs, sizeof(float) * 4, varyings,
            vert, frag, SKColorSpace.CreateSrgb(), SKAlphaType.Premul, out var specErr);
        if (spec == null) return;

        // (Cols+1) * 2 vertices forming Cols quads as a triangle strip.
        int vertCount = (Cols + 1) * 2;
        var verts = new float[vertCount * 4];
        float w = r - l;
        for (int i = 0; i <= Cols; i++)
        {
            float u = i / (float)Cols;
            float x = l + u * w;
            int top = i * 2 * 4;
            int bot = top + 4;
            verts[top + 0] = x;       verts[top + 1] = t;
            verts[top + 2] = u;       verts[top + 3] = 0f;
            verts[bot + 0] = x;       verts[bot + 1] = b;
            verts[bot + 2] = u;       verts[bot + 3] = 1f;
        }

        using var vb = SKMeshVertexBuffer.Make(MemoryMarshal.AsBytes(verts.AsSpan()));

        // Pack uniforms in declaration order. Three floats = 12 bytes.
        // Always assert spec.UniformSize matches your packed byte size.
        Span<float> uniBlob = stackalloc float[3];
        uniBlob[0] = (float)_phase;
        uniBlob[1] = 18f;
        uniBlob[2] = 0.05f;
        using var uniforms = SKData.CreateCopy(MemoryMarshal.AsBytes(uniBlob).ToArray());
        if (spec.UniformSize != 12) return;

        // Bounds inflated to allow vertex displacement.
        var bounds = new SKRect(l, t - 30, r, b + 30);

        using var mesh = SKMesh.Make(
            spec, SKMeshMode.TriangleStrip,
            vb, vertCount, 0,
            uniforms, ReadOnlySpan<SKRuntimeEffectChild>.Empty,
            bounds, out var meshErr);
        if (mesh == null) return;

        using var paint = new SKPaint { IsAntialias = true, Color = SKColors.White };
        canvas.DrawMesh(mesh, paint);
    }

    public override void OnDisposing()
    {
        _animator?.Stop();
        _animator?.Dispose();
        _animator = null;
        base.OnDisposing();
    }
}
```

Key points demonstrated:

- Custom `Float2 uv` varying interpolated between vertices.
- SkSL `uniform` declarations must appear OUTSIDE `main`.
- Uniforms packed into `SKData` in declaration order; size must match `spec.UniformSize` exactly.
- Vertex shader displaces position via uniform-driven sine — `bounds` must be inflated to enclose the displaced range.
- Animation via `PerpetualPendulumAnimator` whose callback updates the field and calls `Update()` to invalidate.
- `OnLayoutReady` (not the constructor) is the safe place to start the animator, because parent / canvas wiring exists by then.
- `SKMeshMode.TriangleStrip` is used to avoid `MakeIndexed` (which crashes the WASM Mono interpreter — see `SKILL.md`).