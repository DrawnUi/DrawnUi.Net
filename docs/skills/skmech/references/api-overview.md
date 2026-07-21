# API Overview

## Verified source and packages

This skill was authored against the mesh API from the SkiaSharp PR branch:

- branch: `upstream/mattleibow/dev-skmesh-api`
- local working branch: `dev-skmesh-api`
- package version copied locally: `4.147.0-pr.3779.2`

Known package locations:

- public EAP feed: `https://pkgs.dev.azure.com/xamarin/public/_packaging/SkiaSharp/nuget/v3/index.json`
- PR #3779 build artifacts (Azure DevOps public build 157123, `nuget_preview` artifact)

If a user cannot resolve `SKMesh`, `SKMeshSpecification`, `SKMeshVertexBuffer`, or `DrawMesh`, suspect package mismatch before suspecting code errors.

## Managed types

### `SKMeshSpecification`

Primary factory:

```csharp
var spec = SKMeshSpecification.Make(
    attributes,
    vertexStride,
    varyings,
    vertexShader,
    fragmentShader,
    out var errors);
```

Overload with color metadata:

```csharp
var spec = SKMeshSpecification.Make(
    attributes,
    vertexStride,
    varyings,
    vertexShader,
    fragmentShader,
    colorSpace,
    SKAlphaType.Premul,
    out var errors);
```

Useful properties:

- `spec.Stride`
- `spec.UniformSize`

Support structs:

```csharp
new SKMeshSpecificationAttribute(SKMeshSpecificationAttributeType.Float2, 0, "position")
new SKMeshSpecificationVarying(SKMeshSpecificationVaryingType.Float2, "uv")
```

Attribute types:

- `Float`
- `Float2`
- `Float3`
- `Float4`
- `Ubyte4Unorm`

Varying types:

- `Float`, `Float2`, `Float3`, `Float4`
- `Half`, `Half2`, `Half3`, `Half4`

### `SKMeshVertexBuffer`

Factories:

```csharp
using var vb = SKMeshVertexBuffer.Make(MemoryMarshal.AsBytes(vertices.AsSpan()));
using var vb2 = SKMeshVertexBuffer.Make(sizeInBytes);
```

Property:

- `vb.Size`

### `SKMeshIndexBuffer`

Factories:

```csharp
using var ib = SKMeshIndexBuffer.Make(MemoryMarshal.AsBytes(indices.AsSpan()));
using var ib2 = SKMeshIndexBuffer.Make(sizeInBytes);
```

Property:

- `ib.Size`

Indices are `ushort` data.

### `SKMesh`

Non-indexed:

```csharp
var mesh = SKMesh.Make(
    spec,
    SKMeshMode.Triangles,
    vb,
    vertexCount,
    vertexOffset,
    bounds,
    out var errors);
```

Indexed:

```csharp
var mesh = SKMesh.MakeIndexed(
    spec,
    SKMeshMode.Triangles,
    vb,
    vertexCount,
    vertexOffset,
    ib,
    indexCount,
    indexOffset,
    bounds,
    out var errors);
```

Optional advanced overload data:

- `SKData uniforms`
- `ReadOnlySpan<SKRuntimeEffectChild> children`

Useful property:

- `mesh.IsValid`

### `SKCanvas.DrawMesh`

```csharp
canvas.DrawMesh(mesh, paint);
canvas.DrawMesh(mesh, blender, paint);
```

## Shader rules

Vertex shader:

```csharp
Varyings main(const Attributes attrs)
```

Fragment shader:

```csharp
float2 main(const Varyings varyings)
```

or:

```csharp
float2 main(const Varyings varyings, out half4 color)
```

The return value is the local coordinate used to sample the paint's shader. If you output a color in the fragment shader, that color is blended with the paint shader or paint color.

## Common failure modes

### `SKMesh` types missing at compile time

Cause:

- wrong package version or feed

Fix:

- use `4.147.0-pr.3779.2` packages from the EAP feed or the PR #3779 build artifacts

### `System.TypeLoadException` for `SKMesh*` types at runtime (Blazor WASM)

Cause:

- IL trimmer (active even on `dotnet build` Debug for Blazor WASM) stripped the `SKMesh*` types from the bundled `SkiaSharp.wasm` because the host project does not statically reference them. Razor-page references to `SKMeshSpecification` etc. are not seen by the trimmer's flow analysis.

Symptom (browser console):

```
System.TypeLoadException: Could not resolve type with token 0100004c from typeref
(expected class 'SkiaSharp.SKMeshSpecificationAttribute'
 in assembly 'SkiaSharp, Version=4.147.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756')
```

Verify the symptom (compile-time DLL has them, bundled webcil does not):

```bash
grep -ao "SKMesh[A-Za-z]*" bin/Debug/net10.0/SkiaSharp.dll | sort -u
tr -c '[:print:]' '\n' < bin/Debug/net10.0/wwwroot/_framework/SkiaSharp.*.wasm | grep -E "^SKMesh"
```

Fix in the host `.csproj`:

```xml
<ItemGroup>
  <TrimmerRootAssembly Include="SkiaSharp" RootMode="EntireAssembly" />
</ItemGroup>
```

`RootMode="EntireAssembly"` is required — the bare `TrimmerRootAssembly` form still lets individual unused types be removed. After the change, delete `bin/` and `obj/` of the Blazor host project before rebuilding because the webcil output is content-hashed and the trimmed cached copy will otherwise be reused.

### `spec == null`, error text `Must provide a color space if FS returns a color`

Cause:

- fragment shader has the optional `out half4 color` / `out float4 color` parameter, but `SKMeshSpecification.Make` was called via the 5-arg overload that does not accept a color space.

Fix:

- switch to the 7-arg overload that takes `SKColorSpace` + `SKAlphaType`. For typical sRGB rendering: `SKColorSpace.CreateSrgb()` + `SKAlphaType.Premul`.

### `spec == null` with other shader error text

Cause:

- invalid SkSL
- wrong shader signatures
- mismatched attribute/varying names
- illegal `position` varying declaration

Fix:

- reduce to a minimal single-attribute triangle and restore complexity one piece at a time

### `mesh == null` with no visible draw

Cause:

- bad vertex/index counts
- bad offsets
- bounds not enclosing geometry
- malformed packed bytes

Fix:

- verify byte counts, offsets, `ushort` index data, and `SKRect bounds`

### `mesh.IsValid == true`, `DrawMesh` runs without exception, nothing on screen

Cause:

- target surface is the CPU/raster backend. In `4.147.0-pr.3779.2`, `SKCanvas.DrawMesh` is a silent no-op on raster — there is no error, no log, the mesh creation succeeds, the draw call returns, but no pixels are produced.

Confirm with a control draw: `canvas.DrawPath(...)` next to the mesh. If the path renders and the mesh does not, the backend is the cause.

Fix per host:

- **DrawnUI Blazor**: set `<Canvas RenderingMode="@RenderingModeType.Accelerated" .../>`. This routes through `SkiaViewAccelerated` which uses a WebGL-backed `GRContext`, on which `DrawMesh` rasterises correctly.
- **MAUI**: use a hardware-accelerated canvas / `SkiaViewAccelerated` host.
- **Off-screen / server**: use `SKSurface.Create(GRRecordingContext, ...)` instead of `SKSurface.Create(SKImageInfo)`.

### draw succeeds but output looks wrong

Cause:

- returned local coordinates are wrong
- paint shader interaction misunderstood
- triangle winding or indexing error

Fix:

- return `varyings.position` first
- use solid fragment color first
- switch to non-indexed geometry to isolate index bugs