---
name: skmech
description: Guidance for SkiaSharp SKMesh custom mesh drawing with SkSL shaders. Use this whenever the user mentions SKMesh, SKMech, custom vertex meshes, mesh shaders, SkSL vertex/fragment programs, 2.5D effects, perspective-warped quads, indexed triangle meshes, or wants to draw geometry through SKCanvas.DrawMesh instead of regular paths or SKVertices.
version: 1.0.1
tags: [drawnui, skiasharp, mesh, shader, sksl]
---

# SKMech / SKMesh

Use this skill for the experimental SkiaSharp mesh API that wraps Skia's `SkMesh`.

This API is not the ordinary released 4.147.0 preview surface. Verified on:

- SkiaSharp source branch: `mattleibow/dev-skmesh-api`
- package version: `4.147.0-pr.3779.2` (PR #3779 build artifacts)
- EAP feed short URL: `https://aka.ms/skiasharp-eap/index.json`

When helping a user, first establish which of these they actually have:

1. mesh-enabled package version
2. mesh-enabled source branch
3. a project that restores from the correct feed or local source

If they are on ordinary stable SkiaSharp packages, do not assume `SKMesh` exists.

If `SKMesh`, `SKMeshSpecification`, `SKMeshVertexBuffer`, `SKMeshIndexBuffer`, or `SKCanvas.DrawMesh(...)` cannot be found, treat that as a package/source mismatch first, not as a coding mistake. In that case use the verified mesh-enabled packages, version `4.147.0-pr.3779.2`.

Those packages were verified from the Azure DevOps public PR build artifacts for PR `#3779`:

- build page: `https://dev.azure.com/xamarin/public/_build/results?buildId=157123`
- artifact source: `nuget_preview` from build `157123`

Do not assume `4.147.0-preview.1.1` or `4.147.0-preview.1.3` contain `SKMesh`; they were checked and did not expose the mesh API.

## Core mental model

The flow is always:

1. Define a mesh specification with attributes, varyings, and SkSL shader source.
2. Create vertex data, and optionally index data.
3. Wrap that data in `SKMeshVertexBuffer` and optionally `SKMeshIndexBuffer`.
4. Create `SKMesh` or `SKMesh.MakeIndexed(...)`.
5. Draw it with `SKCanvas.DrawMesh(...)`.

The important objects are:

- `SKMeshSpecification`: schema + SkSL programs
- `SKMeshVertexBuffer`: raw vertex bytes
- `SKMeshIndexBuffer`: raw index bytes (`ushort` indices)
- `SKMesh`: compiled draw object
- `SKCanvas.DrawMesh(mesh, paint)` or `SKCanvas.DrawMesh(mesh, blender, paint)`

## Constraints to remember

These come from the native `SkMesh` design and matter during diagnosis:

- At least one attribute is required.
- Vertex shader signature must be `Varyings main(const Attributes attrs)`.
- Fragment shader signature must be either `float2 main(const Varyings varyings)` or `float2 main(const Varyings varyings, out half4 color)` / `out float4 color`.
- A `float2` varying named `position` must exist in the final varying set. If omitted, native Skia adds it implicitly; if declared manually with the wrong type, specification creation fails.
- Maximum stride is `1024` bytes.
- Maximum attributes is `8`.
- Maximum varyings is `6`.
- Stride and offsets must be 4-byte aligned.
- Indexed meshes use unsigned 16-bit indices.
- Use `SKRect bounds` that fully encloses the generated geometry. Bad bounds lead to undefined results or clipped output.
- **If the fragment shader has the `out half4 color` / `out float4 color` parameter you MUST call the `SKMeshSpecification.Make` overload that takes `SKColorSpace` + `SKAlphaType`.** The 5-arg overload (no color space) returns `null` with error: `Must provide a color space if FS returns a color`. Pass `SKColorSpace.CreateSrgb()` and `SKAlphaType.Premul` for normal cases.

## Indexed mesh on Blazor WASM - broken PInvoke

In `4.147.0-pr.3779.2`, calling `SKMeshIndexBuffer.Make(...)` followed by `SKMesh.MakeIndexed(...)` from a Blazor WASM host crashes the Mono interpreter immediately and aborts the runtime. Symptom in browser console:

```
[MONO] /__w/1/s/src/runtime/src/mono/mono/mini/interp/interp.c:1502 <disabled>
  ... ves_pinvoke_method ... get_build_args_from_sig_info
program exited (with status: 1) ... ExitStatus
```

The non-indexed `SKMesh.Make(...)` path works fine. The crash is on the PInvoke signature build for the indexed-mesh native call, not on shader / spec / buffer content. Until the SkiaSharp WASM build fixes that signature, on Blazor:

- Use `SKMesh.Make(spec, SKMeshMode.Triangles, vb, vertexCount, 0, bounds, out err)` and duplicate shared vertices (6 verts for a quad instead of 4 + indices).
- For triangle strips a contiguous strip via `SKMesh.Make(spec, SKMeshMode.TriangleStrip, ...)` is also fine.

This restriction is WASM-specific. MAUI / desktop AOT paths can still use `MakeIndexed`.

## Backend support - CRITICAL

**`SKCanvas.DrawMesh` is a silent no-op on the CPU/raster backend in the current `4.147.0-pr.3779.2` build.** The mesh creation succeeds (`spec != null`, `mesh.IsValid == true`), `DrawMesh` returns without exception, but no pixels are produced. You MUST draw onto a GPU surface for the mesh to be visible.

Surface implications by host:

- **Blazor WebAssembly (DrawnUI)**: default `Canvas` `RenderingMode` is `Default` -> CPU raster -> mesh invisible. Set `RenderingMode="@RenderingModeType.Accelerated"` to switch to `SkiaViewAccelerated` (WebGL surface) where `DrawMesh` actually rasterises. Verified working with the yellow triangle in `BlazorSandbox/Pages/SkMeshProbe.razor`.
- **MAUI / desktop**: ensure draw target is a GPU-backed surface (e.g. `SKCanvasView` with hardware acceleration / `SkiaViewAccelerated`).
- **Server-side / `SKSurface.Create(SKImageInfo)`**: that is a raster surface - mesh will not draw. Use `SKSurface.Create(GRRecordingContext, ...)` if you need offscreen GPU.

If a mesh "draws nothing" but no errors appear and `mesh.IsValid` is `true`, the backend is the first suspect, before shader or buffer packing.

## Blazor WebAssembly trimming - CRITICAL

`SkiaSharp` ships as a *trimmable* assembly. In a default Blazor WASM build (Debug `dotnet build` is enough - does not require Publish) the trimmer removes every `SKMesh*` type from the runtime `SkiaSharp.wasm` because the host project does not statically reference them at build time, even when user razor pages reference them. Razor-compiled references are not seen by the trimmer's flow analysis early enough to keep the type metadata.

Symptom at runtime:

```
System.TypeLoadException: Could not resolve type with token 0100004c from typeref
(expected class 'SkiaSharp.SKMeshSpecificationAttribute'
 in assembly 'SkiaSharp, Version=4.147.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756')
```

Verification commands:

```bash
# compile-time DLL still has the types
grep -ao "SKMesh[A-Za-z]*" bin/Debug/net10.0/SkiaSharp.dll | sort -u
# webcil-bundled WASM assembly does NOT
tr -c '[:print:]' '\n' < bin/Debug/net10.0/wwwroot/_framework/SkiaSharp.*.wasm | grep -E "^SKMesh"
```

Fix - add to the host `.csproj` (e.g. `BlazorSandbox.csproj`):

```xml
<ItemGroup>
  <TrimmerRootAssembly Include="SkiaSharp" RootMode="EntireAssembly" />
</ItemGroup>
```

Do NOT use `<TrimmerRootAssembly Include="SkiaSharp" />` alone - without `RootMode="EntireAssembly"` the assembly is rooted but individual unused types are still pruned, so the symptom persists. After changing this you usually have to **delete `bin/` and `obj/` of the Blazor host project** before rebuilding because the webcil output is content-hashed and the cached trimmed copy is otherwise reused.

## Practical workflow

When asked to implement or debug mesh drawing:

1. Verify package/branch availability first.
2. Verify host is rendering on a GPU surface (Accelerated mode, WebGL canvas, hardware MAUI canvas) - raster = silent no-op.
3. For Blazor WASM, verify trimmer is rooting `SkiaSharp` (`TrimmerRootAssembly` with `RootMode="EntireAssembly"`).
4. Start from a tiny non-indexed triangle.
5. Keep the first spec minimal: one `Float2` position attribute, no custom varyings.
6. Make the fragment shader output a solid color first - and use the `Make` overload with `SKColorSpace` + `SKAlphaType` because `out half4 color` requires it.
7. Only after that works, add indexed geometry, uniforms, children, or perspective math.
8. Treat the `errors` out parameter from `SKMeshSpecification.Make(...)` and `SKMesh.Make(...)` as the first diagnostic signal.

## Diagnostic decision tree

Symptom-first triage when "mesh doesn't draw":

1. `System.TypeLoadException` for any `SKMesh*` type -> trimmer stripped types. Apply `TrimmerRootAssembly RootMode="EntireAssembly"` and clean `bin/`+`obj/`.
2. Mono interp crash with `ves_pinvoke_method` / `get_build_args_from_sig_info` after a successful unrelated mesh draw -> indexed-mesh PInvoke broken on Blazor WASM. Switch to non-indexed `SKMesh.Make`.
3. `spec == null`, error contains `Must provide a color space if FS returns a color` -> switch to color-space overload of `SKMeshSpecification.Make`.
4. `spec == null`, other shader text -> SkSL syntax / missing varying / wrong main signature.
5. `mesh == null` -> vertex/index count, offset, or bounds wrong.
6. `mesh.IsValid == true`, `DrawMesh` returns silently, no pixels -> CPU raster backend. Switch host to GPU surface (Accelerated mode in Blazor / hardware-accelerated surface in MAUI).
7. Pixels appear but wrong color/position -> fragment returning bad position; flip to outputting solid color and `return varyings.position` as-is to verify pipeline.

## API reminders

- `SKMeshSpecification.Make(...)` returns `null` plus an error string on shader/spec failure.
- `SKMesh.Make(...)` and `SKMesh.MakeIndexed(...)` also return `null` plus an error string on failure.
- `mesh.IsValid` is a useful sanity check after creation.
- `SKMeshVertexBuffer.Make(ReadOnlySpan<byte>)` and `SKMeshIndexBuffer.Make(ReadOnlySpan<byte>)` copy raw bytes, so callers usually pack `float[]` or `ushort[]` with `MemoryMarshal.AsBytes(...)`.
- `SKMesh.Make(...)` can take optional `SKData uniforms` and `ReadOnlySpan<SKRuntimeEffectChild> children`.
- `SKCanvas.DrawMesh(...)` still requires a normal `SKPaint`.

## What to read next

Read [references/api-overview.md](references/api-overview.md) when you need the API surface, packaging notes, and common failure modes.

Read [references/examples.md](references/examples.md) when you need copyable examples for:

- first triangle
- indexed quad
- upgrade from plain color to paint shader/blender interaction
- debugging checklist

## Output expectations

When answering with this skill active:

- state whether the user's package/source likely contains `SKMesh`
- give the smallest working example first
- call out whether the issue is package mismatch, shader/spec error, buffer packing error, or bounds error
- prefer concrete code over abstract explanation