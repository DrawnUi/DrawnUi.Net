---
name: drawnui-shaders
description: Writing SkSL shaders for DrawnUI and porting GLSL / Shadertoy / CodePen shaders to it — SkiaShaderEffect in VisualEffects (inline ShaderCode or a ShaderSource file), the standard uniforms every shader must declare, custom uniforms, sampling the control's picture, compile errors, time-driven shaders, resolution-independent patterns (preview vs full photo), backdrop / glass effects, shader slide transitions, and the performance rules. Load before writing, porting or debugging any shader used in a DrawnUI app.
triggers:
  - sksl
  - shader
  - SkiaShaderEffect
  - shadertoy
  - glsl
version: 1.0.0
tags: [drawnui, skiasharp, shader, sksl, glsl]
---

# DrawnUI shaders (SkSL)

DrawnUI draws with SkiaSharp, so shaders are **SkSL runtime effects**. Load the `drawnui` skill as well for controls and caching, and `drawnui-fluent` for C# composition. Custom meshes with SkSL vertex programs: `skmech`.

## 1. A shader on a control

A shader runs as a post-renderer in a control's `VisualEffects`: it receives the control's own picture as `iImage1` and draws the result in its place.

```csharp
const string Desaturate = """
uniform float4 iMouse;
uniform float  iTime;
uniform float2 iResolution;
uniform float2 iImageResolution;
uniform shader iImage1;
uniform float2 iOffset;
uniform float  uAmount;

half4 main(float2 fragCoord) {
    float2 inputCoord = (fragCoord - iOffset) * iImageResolution / iResolution;
    half4 c = iImage1.eval(inputCoord);
    half grey = dot(c.rgb, half3(0.299, 0.587, 0.114));
    c.rgb = mix(c.rgb, half3(grey), half(uAmount));
    return c;
}
""";

new SkiaImage
{
    Source = "photo.jpg",
    UseCache = SkiaCacheType.Image,          // the cache IS the effect's input picture, see section 6
    VisualEffects =
    {
        new SkiaShaderEffect { ShaderCode = Desaturate }
            .SetUniform("uAmount", 0.7f)
            .OnShaderError((me, error) => Console.WriteLine($"[SkSL] {error}"))
    }
}
```

- **Inline code**: `ShaderCode`, written as a raw string literal (`"""`) in its own `const`, never a verbatim `@"..."` string: no `""` escaping, and the closing `"""` strips the C# indentation so compiler error columns match.
- **A file**: `ShaderSource = @"Shaders\myeffect.sksl"`. MAUI loads it from `Resources/Raw`, OpenTK / WPF from the exe folder (`CopyToOutputDirectory`), web heads from the site, asynchronously (the effect draws nothing until the file arrives). In the browser the synchronous `SkiaShader.FromResource` / `SkSl.LoadFromResources` throw for a file not loaded yet: `await SkSl.PrecompileAsync(...)` first.
- `UseBackground` defaults to `Always` (input read every frame). `Once` freezes the first picture; reset `AquiredBackground` to take a new one.

## 2. Uniforms

- **Declare every standard uniform, even unused ones**: `iMouse` (float4), `iTime` (float), `iResolution` (float2), `iImageResolution` (float2), `iImage1` (shader), `iOffset` (float2). The engine writes all of them every frame; writing one the shader does not declare throws, which kills the effect (it draws nothing).
  - `iResolution`: the destination size in pixels. `iImageResolution`: the input picture size in pixels. `iOffset`: where the destination starts on the canvas. `iTime`: seconds.
- **Custom uniforms**: `.SetUniform("uName", value)` with float / float2 / float3 / float4 overloads, or a `float[]` for an array uniform (a one-element array is written as a scalar: SkiaSharp rejects `float[1]` for a `float`). It is chainable, can be called again at runtime (a slider) and redraws by itself. A custom uniform the shader does not declare is only logged: a typo silently does nothing. The reverse is a trap too: a uniform the shader declares but nothing writes is not 0, it holds whatever memory it got (measured 2026-10-25: the same test drew red or garbage from run to run), so write every declared uniform.
- **Animated or uniform-driven effects on big cached layers: `KeepParentCache = true`** (since 1.10.7.14). By default every `SetUniform`, every `AnimatedShaderEffect` tick and every `Update()` also rebuilds the cache of the control carrying the effect, so the whole layer re-renders each frame. With `KeepParentCache = true` ticks, `SetUniform` and `UpdateUniforms()` only redraw the effect over the same cache. Call `UpdateUniforms()` after changing a value your own `CreateUniforms` reads; keep `Update()` for shader changes. Use it on `Image` / `GPU` cached parents whose content calls `Update()` itself; leave it false on `Operations` (picture) parents, the default of SkiaShape / SkiaLabel, and when the parent's content refreshes only through the effect's redraws.
- **Compile errors**: subscribe `.OnShaderError(...)`; without it a failed compile shows only as a blank effect.
- **Time-driven shaders** repaint only when something asks for frames: keep the canvas drawing with `.UpdateNonStop()` or a looping `SkiaValueAnimator` that calls `effect.Update()`.

## 3. Sampling the picture

`iImage1.eval(coord)` takes **pixel coordinates of the input picture**, not 0..1 UVs. Always map the output pixel first:

```glsl
float2 inputCoord = (fragCoord - iOffset) * iImageResolution / iResolution; // input pixels
half4 c = iImage1.eval(inputCoord);
```

This is correct in both render paths: as a `SkiaShaderEffect` (picture size = destination size) and when drawing an image directly with `SkiaShader.DrawImage` / `DrawRect(canvas, image, dst)` (picture size = image size).

- A neighbour pixel is `inputCoord + float2(1.0, 0.0) * scale` (see section 5). `1.0 / iImageResolution` is a UV step: added to a pixel coordinate it moves ~0.001 px, so the kernel silently samples the centre pixel and the effect does nothing.
- Sampling is linear by default; `p + 0.5` puts a tap between four texels, a free 2x2 average.

## 4. Porting GLSL / Shadertoy / CodePen shaders

SkSL runtime effects follow GLSL ES 2.0 strictly. Most web shaders port in minutes; these are the differences (each checked against the SkiaSharp 4.148 compiler).

| GLSL / Shadertoy | DrawnUI SkSL |
|---|---|
| `void mainImage(out vec4 fragColor, in vec2 fragCoord)` or `void main()` + `gl_FragColor` | `half4 main(float2 fragCoord) { ... return color; }`. A Shadertoy `mainImage` can stay as it is and be called from `main` |
| `gl_FragCoord` | the `main` parameter (`gl_FragCoord` is a reserved name) |
| `iResolution` (vec3) | `iResolution` (float2), destination pixels |
| `iTime`, `iMouse` | same names |
| `iChannel0` + `texture(iChannel0, uv)` | `iImage1.eval(uv * iImageResolution)`: pixels, not UV (section 3) |
| `texelFetch`, `textureLod`, `dFdx` / `fwidth` | not available: compute the step yourself (one output pixel = `iImageResolution / iResolution` input pixels) |
| `varying vec2 vUv` (three.js / CodePen) | no varyings: `float2 uv = fragCoord / iResolution` |
| `precision mediump float;`, `#define`, `#ifdef` | remove; `#define K 2.0` becomes `const float K = 2.0;` |

- **Y axis**: `fragCoord` starts at the TOP-left, in GLSL at the bottom-left. A shader that comes out upside down needs `uv.y = 1.0 - uv.y` (or `fragCoord.y = iResolution.y - fragCoord.y`) at the top of `main`.
- **Types**: `vec2/3/4` and `mat2/3/4` compile as aliases of `float2/3/4` and `float2x2`...; no renaming needed. Return the color as `half4`. Not allowed: `uint`, bit operators (`&`, `|`, `<<`), integer `%`. Use `mod(x, n)` on floats and a float hash instead of an integer one, e.g. `fract(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453)`.
- **Loops**, the usual compile error:
  - no `while` and no `do-while`: rewrite as `for`;
  - the index must be compared with a CONSTANT: `i < 8` works, `i < count` with a uniform fails. Loop to a constant maximum and `break` at the real count;
  - the index must not change inside the body.
- **Arrays**: an index must be a constant or the loop index (`a[int(x)]` fails). Array constructors (`float[3](1, 2, 3)`) fail: assign the elements one by one, or use a `float3` / `float4`. Uniform arrays (`uniform float4 colors[4];`) work.
- **Reserved names**: `out`, `input`, `gl_FragCoord`, `precision` cannot name a variable (`out` becomes `outColor`). No recursion.
- **Give the shader an input**: DrawnUI's standard uniforms (section 2) must be declared even when the ported shader only generates a pattern.
- **Respect the license** of what you port: a public CodePen is MIT by default, Shadertoy defaults to CC BY-NC-SA (no commercial use) unless the author says otherwise. Keep the author credit.

## 5. Same look at every size (preview, photo, video)

The same shader often renders a ~720-1080 px preview, a thumbnail and a 12 MP photo. Anything sized in OUTPUT pixels gets N times finer on the big picture and averages away or aliases when it is shown fit-to-screen. Build patterns and distances in picture-relative units:

```glsl
float2 inputCoord = (fragCoord - iOffset) * iImageResolution / iResolution;
float scale = min(iImageResolution.x, iImageResolution.y) / 720.0; // input pixels per reference pixel
float2 p = inputCoord / scale;                                     // pattern space, same density at any size
```

- **Cells** (dither, halftone, pixelation): look up by `floor(p)`, never by `fragCoord`.
- **Grain**: a hash like `fract(sin(dot(c, k)) * 43758.5453)` is chaotic at sub-unit steps, so scaling its input does NOT enlarge the grain. Put it on a lattice: hard grain `hash(floor(p))`, soft grain a value noise of `p`:

```glsl
float hash21(float2 p) { return fract(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float valueNoise(float2 p) {
    float2 i = floor(p);
    float2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash21(i), hash21(i + float2(1, 0)), f.x),
               mix(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), f.x), f.y);
}
```

- **Stripes / scanlines**: frequency from `p` or from a UV, never from raw pixels.
- **Pixel distances** (blur radius, kernel taps, neighbours, chromatic offsets): multiply by `scale`. Kernel WEIGHTS stay computed from the unscaled tap index.
- **Anti-aliasing is the exception**: shapes are picture-relative, but an edge's blend width must stay about one OUTPUT pixel: `aa = 0.75 * max(iImageResolution.x / iResolution.x, iImageResolution.y / iResolution.y)` in input pixels, then `smoothstep(-aa, aa, distance)`. Scaling the AA with the picture blurs edges on the photo.
- **Reference**: pick one (720 above) per app; `min(...)` keeps the look when preview and photo differ in orientation. Below the reference (thumbnails) cells become sub-pixel.
- **Grain on the output grid**: put grain on `inputCoord`, not on distorted sampling coordinates, or it drifts off the pixel grid and averages away on the photo.

## 6. Performance

- **A shader effect re-runs every frame the canvas draws**: a control with `VisualEffects` skips its cached blit, builds the uniforms and draws the shader each frame, then flushes the canvas. The cost scales with the NUMBER of shader-carrying controls on screen more than with the shader's length: a strip of ~8 live shader thumbnails over a camera preview took a high-end Windows GPU from 60 to 36 fps.
- **Keep an image cache on the shaded control.** With an `Image` / `GPU` cache the effect reads that cache as its input; without one (or with `Operations`, which stores no image) it snapshots the canvas every frame (`Canvas.Flush()` + snapshot). Removing the cache makes the control much more expensive.
- **An effect drawn while an `Image` cache is made runs on the CPU** (raster surface, drawing thread): a heavy shader costs tens of ms per tile, a stutter in a scrolling list. Give such controls a GPU cache, or bake the result off the UI thread.
- **Content that changes far slower than the frame rate gets no live effect**: render the shaded result once per change into an `SKImage` and show it in a plain cached `SkiaImage`.
- **Count texture reads**: `iImage1.eval` is the main cost of most picture effects. When comparing two versions on a desktop, render both alternately several times; a single run is noisy, and desktop CPU timings never stand in for the cost on a phone GPU: measure on the device.

## 7. Backdrop and glass

- `SkiaBackdrop` + a `SkiaShaderEffect` in its `VisualEffects` shades whatever is drawn BEHIND it (it snapshots the canvas surface; on OpenTK that includes raw GL content). Set `SkiaBackdrop.Blur = 0` when the shader does its own blur.
- Liquid glass panel: the backdrop as the first child inside the `SkiaShape`; no GPU cache on that shape (it would freeze the snapshot). Shader uniforms are in pixels: a corner radius in points goes in multiplied by `RenderingScale`.
- Inside a `SkiaShell` modal a backdrop snapshots the screen UNDER the modal, not the modal's own picture: when the picture behind the glass is your own content, put the effect on the cached layer that draws it (`UseBackground = Always` reads its own cache) and pass the panel rect as a uniform.

## 8. Shader slide transitions

`SkiaShaderCarousel` (`DrawnUi.Controls`) blends the from / to slides with a gl-transitions style shader (`vec4 transition(vec2 uv)` using `getFromColor(uv)` / `getToColor(uv)`, uniforms `progress` and `ratio`): `TransitionShader` (file), `TransitionShaderCode` (inline) or a custom `TransitionTemplate`. Cell templates must use `UseCache = Image` (the effect samples the cells' caches). A swipe during a transition finishes it in `InterruptedTransitionMs` (default 50), then plays the next.

## 9. Checking a shader without a device

A single-file console compiles and renders a shader with SkiaSharp in seconds:

```csharp
#:package SkiaSharp@4.148.0
using SkiaSharp;
var effect = SKRuntimeEffect.CreateShader(File.ReadAllText(args[0]), out var error)
             ?? throw new Exception(error);   // the error names the line
```

Set the standard uniforms (`SKRuntimeEffectUniforms`), pass a picture as `iImage1` (`SKRuntimeEffectChildren`), draw a rect into an `SKBitmap` and save it (`dotnet run check.cs -- myeffect.sksl`). Render the same picture at N and 4N pixels to check that patterns scale with the picture (section 5). In the DrawnUI Fiddle use inline code (`ShaderCode`, `TransitionShaderCode`). Working samples: HelloMaui `Pages/ShadersPage.cs` in the DrawnUi repo.
