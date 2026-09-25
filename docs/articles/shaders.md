# Shaders

DrawnUI ships with a thin, reusable SKSL shader layer on top of SkiaSharp's
`SKRuntimeEffect`. It is designed for **hot-path rendering** — applying custom
fragment shaders every frame at 60+ FPS without generating garbage or churning
GPU handles.

See a [live example](https://drawnui.net/sandbox/effects-shader) running on Blazor WebAssembly.

There are two entry points:

| Type | Namespace | Purpose |
|---|---|---|
| `SkiaShader` | `DrawnUi.Infrastructure` | Lightweight reusable engine. Use directly for manual rendering or as a building block. |
| `SkiaShaderEffect` | `DrawnUi.Draw` | XAML-bindable `IPostRendererEffect` that wraps `SkiaShader`. Attach to any `SkiaControl` through its `VisualEffects` collection. |

Ready-made subclasses of `SkiaShaderEffect`:

| Type | Adds |
|---|---|
| `AnimatedShaderEffect` | A 0..1 `progress` animation with `Play()` / `Completed`, plus an `iCenter` uniform. |
| `ShaderDoubleTexturesEffect` | A second input texture, `iImage2`. |
| `ShaderTransitionEffect` | Two textures plus `progress` and `ratio`, with a built-in gl-transitions template. Used by `SkiaShaderCarousel`. |

## Quick start — effect on a control

```xml
<draw:SkiaImage Source="photo.jpg"
                Aspect="AspectCover"
                UseCache="Image">
    <draw:SkiaImage.VisualEffects>
        <draw:SkiaShaderEffect ShaderSource="Shaders/ripples.sksl" />
    </draw:SkiaImage.VisualEffects>
</draw:SkiaImage>
```

The shader receives the control's rendered output as `iImage1` and draws the
result back onto the canvas. No code-behind required.

## Standard uniforms

`SkiaShader` provides a Shadertoy-style uniform set out of the box:

```glsl
uniform shader iImage1;          // input texture (the control's output)
uniform float2 iResolution;      // size of the drawn area, in pixels
uniform float2 iImageResolution; // input texture size, in pixels (see below)
uniform float  iTime;            // frame timestamp in seconds (see Animation)
uniform float2 iOffset;          // canvas position of the texture's top-left pixel
uniform float4 iMouse;           // xy = current position, zw = start position, pixels
```

You do not populate them — the engine writes every one of them on every frame.
That is why the five non-texture uniforms (`iResolution`, `iImageResolution`,
`iTime`, `iOffset`, `iMouse`) are **mandatory declarations**, even when your
shader never reads them:

- Writing a uniform the compiled shader does not declare throws
  `ArgumentOutOfRangeException` ("Variable was not found for name").
- Declaring one with a different type throws too ("Unable to write a ... value
  to a ... uniform"). `iMouse` must be `float4`, not `float2`.

The exception escapes the effect's `Render` on every frame, so the control
stops drawing correctly. An unused declared uniform costs nothing.

`iImage1` must be declared whenever the effect passes a texture, which is every
`UseBackground` mode except `Never` (see below).

What the values actually are:

- **`iResolution`** is the size of the area the effect draws, in pixels.
- **`iImageResolution`** — inside `SkiaShaderEffect` it is the same value as
  `iResolution`. Only direct `SkiaShader` calls that take an `SKImage`
  (`ApplyTo`, `DrawRect(canvas, image, rect)`, `DrawImage`) pass the image's real
  size. Writing `(fragCoord - iOffset) * iImageResolution / iResolution` keeps a
  shader correct in both cases.
- **`iOffset`** is where the input texture's top-left pixel sits on the canvas.
  It usually equals the control's top-left, but not always: a cache enlarged to
  fit a shadow, or a texture taken from another control (`ControlFrom`), starts
  elsewhere.
- **`iTime`** and **`iMouse`** are covered in [Animation](#animation-and-itime)
  and [Mouse and touch](#mouse-and-touch).

Shaders assembled from a template (`//script-goes-here`, see
[Templates](#templates)) declare them once in the template, not per snippet.

## Coordinates

`main(float2 fragCoord)` receives **canvas pixel coordinates**: origin at the
top-left, Y pointing down, covering the area the effect draws. No local matrix
is applied, so the control's position on the canvas is included.

```glsl
half4 main(float2 fragCoord)
{
    float2 texel = (fragCoord - iOffset) * iImageResolution / iResolution; // pixel in iImage1
    float2 uv    = (fragCoord - iOffset) / iResolution;                     // 0..1 across the area
    return iImage1.eval(texel);
}
```

There is no automatic Y flip. GLSL sources (Shadertoy, gl-transitions) use a
bottom-left origin — flip `uv.y = 1.0 - uv.y` when porting them.

## Resource loading

### MAUI

Shader files go in `Resources/Raw/Shaders/` (lowercase filenames — iOS is
case-sensitive). Load with `ShaderSource="Shaders/myeffect.sksl"`. The file is
read synchronously from the app package the first time the effect renders.

### Blazor, WebAssembly, OpenTK and other DrawnUi.Net hosts

Synchronous resource loading is not available there: `ShaderSource`,
`ShaderTemplate` and `SkiaShader.FromResource` throw `NotSupportedException`
unless the file is already loaded. Pick one:

- Preload at startup, before any control uses the file. This fetches the files
  over `HttpClient` (for Blazor: from `wwwroot`):

  ```csharp
  await SkSl.PrecompileAsync("Shaders/ripples.sksl", "Shaders/blit.sksl");
  // template files are only loaded, not compiled:
  await SkSl.LoadFromResourcesAsync("Shaders/mytemplate.sksl");
  ```

- Or pass the code itself with `ShaderCode`. On desktop hosts, where
  `HttpClient` has no base address to fetch from, read the file yourself:
  `ShaderCode = File.ReadAllText("Shaders/ripples.sksl")`.

### Compile cache

- Effects loaded through `ShaderSource` are compiled once per process and cached
  by that path, so many controls using the same `.sksl` share one compiled effect.
- `ShaderCode` is compiled per effect instance and never cached.
- The cache key is the path alone: changing `ShaderTemplate` while keeping the
  same `ShaderSource` keeps returning the previously compiled effect.
- Set either `ShaderCode` or `ShaderSource`, not both. When both are set,
  `ShaderCode` is used.

### Writing inline SkSL in C#

Put the shader in a **raw string literal**, hoisted to its own `const`, rather
than a verbatim `@"..."` string inlined at the property:

```csharp
const string ripple = """
uniform float2 iResolution;
uniform float2 iImageResolution;
uniform float  iTime;
uniform float2 iOffset;
uniform float4 iMouse;

half4 main(float2 fragCoord)
{
    float2 uv = (fragCoord - iOffset) / iResolution.xy;
    float  w  = sin(length(uv - 0.5) * 40.0 - iTime * 4.0) * 0.5 + 0.5;
    return half4(w, w, w, 1.0);
}
""";

// ...
new SkiaShaderEffect
{
    ShaderCode = ripple,
    UseBackground = PostRendererEffectUseBackgroud.Never, // generative, no input texture
}
```

A verbatim string needs every `"` doubled and carries the surrounding C#
indentation into the shader source; the raw literal takes the SkSL byte for
byte and strips the indentation of the closing `"""`, so compiler error columns
line up with what you wrote.

It also buys syntax highlighting in Monaco-based editors such as
[DrawnUI Fiddle](https://drawfiddle.com/app). Monaco's bundled `csharp`
tokenizer has a rule for `@"` — a verbatim shader is painted as one flat string
— but no rule for `"""`, so the raw-literal body falls through to ordinary code
tokenization and `float`, `return`, numbers and `//` comments all get coloured.
That part is a tokenizer gap rather than a guaranteed feature; the escaping and
indentation benefits hold regardless.

## Templates

`ShaderTemplate` names a second `.sksl` file that wraps your shader: the text of
the shader replaces the `//script-goes-here` line of the template. Use it to
declare the standard uniforms and shared helper functions once for a family of
shaders. Your snippet still provides its own `half4 main(float2 fragCoord)` —
the template does not generate one.

Compiler error line numbers count the template lines that precede the snippet.

## Effect properties

| Property | Default | Purpose |
|---|---|---|
| `ShaderSource` | — | Path of the `.sksl` file (see [Resource loading](#resource-loading)). |
| `ShaderCode` | — | SkSL source string. |
| `ShaderTemplate` | — | Path of a wrapping template (see [Templates](#templates)). |
| `UseBackground` | `Always` | How `iImage1` is sourced (see below). |
| `AutoCreateInputTexture` | `true` | Take a snapshot of the drawn area when no cache image is available. Set `false` for output-only shaders. |
| `UseContext` | `true` | Snapshot from the current drawing context (which may be a parent's cache surface). `false` reads the canvas surface instead. |
| `BlendMode` | `SrcOver` | Blend used to draw the shader output. `Plus` suits additive generative overlays. |
| `FilterMode` / `MipmapMode` / `TileMode` | `Linear` / `None` / `Clamp` | Sampling of the `iImage1` texture. |
| `MouseCurrent` / `MouseInitial` | `(0,0)` | Values written to `iMouse`. |
| `Uniforms`, `SetUniform(...)` | empty | Custom uniforms (see below). |

Event `OnCompilationError` (fluent `.OnShaderError(...)`) reports SkSL compile errors.

## `UseBackground` modes

`SkiaShaderEffect.UseBackground` controls how the input texture (`iImage1`) is
sourced:

| Mode | Behavior | When to use |
|---|---|---|
| `Always` | Uses the control's own cache image when it has one; otherwise snapshots the drawn area every frame. | Live effects over animated content. |
| `Once` | Takes the texture on first render, freezes, and keeps feeding the same image. Reset with `AquiredBackground = false`. | One-shot transitions, reveal animations. |
| `Never` | Passes no texture; `iImage1` is not set. | Generative shaders (noise, gradients, procedural patterns). |

> **Tip:** For `Always` mode, set a cache type on the control that carries the
> effect (`Image` works well), so the effect reuses the already-rasterised image
> instead of flushing and snapshotting the canvas every frame.

## How the effect draws

- **On a cached control, the cache image is not drawn.** When a control has any
  post-render effect, the engine skips its usual cache blit and runs the effects
  instead. The shader output is the only thing painted, so a pass-through shader
  must return the texture (`return iImage1.eval(...)`). Returning transparent
  hides the control.
- **On an uncached control**, the control paints normally and the effect draws
  its output on top of it with `BlendMode`.
- **Every draw of the control runs the shader** and then flushes the canvas. The
  cost therefore grows with the number of shader-carrying controls on screen,
  more than with the complexity of each shader. If the shaded result changes
  much slower than the frame rate (thumbnails, previews), render it into an
  `SKImage` once per change and display that image without an effect.
- **Several effects on one cached control all receive the same input** — the
  control's cache image. The second shader does not see the first one's output.
  On an uncached control each effect snapshots the canvas, so shaders do chain.

## Animation and `iTime`

`iTime` is the canvas frame timestamp converted to seconds. It is **not** time
since the effect started: it is a platform timestamp that keeps growing, so do
not expect it to begin at 0.

**Nothing redraws the control for you.** A time-driven shader only advances
when something invalidates the control. Keep it redrawing with an animator, for
example the fluent `.UpdateNonStop()` on the control that carries the effect:

```csharp
new SkiaLayer
{
    VisualEffects = { new SkiaShaderEffect { ShaderCode = ripple, UseBackground = PostRendererEffectUseBackgroud.Never } },
}
.UpdateNonStop()
```

Because `iTime` is a large number stored in a 32-bit float, its precision drops
as the timestamp grows. For animations that must stay smooth over long
sessions, keep your own time value wrapped to the animation period and pass it
with `SetUniform`.

`SetUniform` and `SkiaEffect.Update()` both request a redraw.

### AnimatedShaderEffect

Runs a linear 0..1 animation over `DurationMs` (default 2500) and writes it to
the `progress` uniform. It also writes `iCenter` (`float2`, normalized 0..1,
from `Center`, default 0.5,0.5). Declare both in the shader.

```csharp
var fx = new AnimatedShaderEffect
{
    ShaderSource = "Shaders/dissolve.sksl",
    UseBackground = PostRendererEffectUseBackgroud.Once,
    DurationMs = 1200,
};
fx.Completed += (s, e) =>
{
    image.VisualEffects.Remove(fx);
    fx.Dispose();
};
image.VisualEffects.Add(fx);
fx.Play(); // needs the effect attached to a control
```

`Play()` restarts from 0 and resets `AquiredBackground`, so `Once` mode captures
a fresh texture. `Stop()` stops without raising `Completed`.

## Mouse and touch

`iMouse` is written from `MouseCurrent` (xy) and `MouseInitial` (zw), in pixels.
The engine does **not** fill them from gestures — set them yourself from a
gesture handler, then call `Update()` on the effect.

Older sample shaders also declare `uniform float2 iOrigin`; the engine never
writes it, so it stays zero.

## Code-behind rendering

`SkiaShader` can be used directly inside any control's `Paint` override:

```csharp
// FromResource works on MAUI; on web and desktop hosts use FromCode (see Resource loading)
private readonly SkiaShader _shader = SkiaShader.FromResource("Shaders/noise.sksl");

protected override void Paint(DrawingContext ctx)
{
    base.Paint(ctx);

    _shader.Time = (float)(ctx.Context.FrameTimeNanos * 1e-9);
    _shader.Offset = new SKPoint(ctx.Destination.Left, ctx.Destination.Top);
    _shader.DrawRect(ctx.Context.Canvas, ctx.Destination);
}

public override void OnDisposing()
{
    _shader.Dispose();
    base.OnDisposing();
}
```

`Offset` defaults to `(0,0)` and none of the `Draw*` methods set it. Set it to
the destination's top-left, as above, or `fragCoord - iOffset` will not start at
zero for a control that is not at the canvas origin.

## Custom uniforms

### SetUniform

The quickest path, no subclass needed:

```csharp
new SkiaShaderEffect { ShaderCode = mySksl }
    .SetUniform("uIntensity", 0.7f)          // uniform float  uIntensity;
    .SetUniform("uTint", 1f, 0.5f, 0.2f)     // uniform float3 uTint;
```

Overloads exist for `float`, `float2`, `float3` and `float4`. Call `SetUniform`
again at any time (from a slider, for example); it requests a redraw. Names the
shader does not declare are logged and skipped, not thrown.

### Subclass pattern

Subclass either `SkiaShader` (engine-level) or `SkiaShaderEffect` (XAML-level).
Override `CreateUniforms` and set extra keys on the returned instance:

```csharp
public class RippleEffect : SkiaShaderEffect
{
    public float Intensity { get; set; } = 1f;

    // Pre-allocated buffer — see "Performance contract" below
    private readonly float[] _bufCenter = new float[2];
    public SKPoint Center { get; set; } = new(0.5f, 0.5f);

    protected override SKRuntimeEffectUniforms CreateUniforms(SKRect destination)
    {
        var uniforms = base.CreateUniforms(destination);

        uniforms["intensity"] = Intensity;

        _bufCenter[0] = Center.X;
        _bufCenter[1] = Center.Y;
        uniforms["iCenter"] = _bufCenter;

        return uniforms;
    }
}
```

Uniforms set this way must be declared in the shader: unlike `SetUniform`, a
missing name throws.

Additional input textures work the same way via `CreateTexturesUniforms` /
`SkiaShader.CreateChildren`:

```csharp
protected override SKRuntimeEffectChildren CreateTexturesUniforms(
    SkiaDrawingContext ctx, SKRect destination, SKShader primaryTexture)
{
    var children = base.CreateTexturesUniforms(ctx, destination, primaryTexture);
    children["iImage2"] = _secondaryTextureShader;
    return children;
}
```

## Two textures — ShaderDoubleTexturesEffect

`ShaderDoubleTexturesEffect` passes a second texture as `iImage2`. `iImage2` is
set only when both textures are available.

| Texture | Source, first match wins |
|---|---|
| `iImage1` | `ControlFrom` — that control's cache image (the control must be cached). `PrimarySource` — an image file, loaded in the background and resized to the control's `DrawingRect`. Otherwise the normal `UseBackground` logic. |
| `iImage2` | `ControlTo` — that control's cache image, re-imported whenever its cache is recreated. `SecondarySource` — an image file. Or call `CompileSecondaryTexture(SKImage)` yourself. |

File sources starting with `file://` are read from the local file system; other
paths come from the app package on MAUI and over `HttpClient` on web and desktop hosts.

## Performance contract

Shaders run on the hot path — every frame, on the render thread. `SkiaShader`
is carefully allocation-free in steady state:

- `SKRuntimeEffectUniforms`, `SKRuntimeEffectChildren`, and the texture
  `SKShader` are **cached on the instance** and reused across frames. They are
  rebuilt automatically only when the compiled effect changes, the source
  image handle changes, or sampling options change.
- The only unavoidable per-frame allocation is the final `SKShader` returned
  by `SKRuntimeEffect.ToShader(...)` — SkiaSharp snapshots uniforms at that
  call, so it has to be recreated each frame.
- All standard uniform float arrays (`iResolution`, `iMouse`, `iOffset`, …)
  are stored in pre-allocated `float[]` fields on the base class.

`ShaderDoubleTexturesEffect` (and so `ShaderTransitionEffect`) does not follow
this yet: it builds a new `SKRuntimeEffectChildren` every frame.

**Rules when subclassing:**

1. **Do not dispose** the object returned from `base.CreateUniforms(...)` or
   `base.CreateChildren(...)`. It is owned by the engine and reused.
2. **Do not return a different instance** — mutate the one returned by `base`
   and return it.
3. **Do not allocate per frame inside `CreateUniforms`.** If you need a
   `float[]` uniform, store it as a field and overwrite its slots each call
   (see `_bufCenter` above).
4. **Do not dispose** the shader returned by
   `SkiaShader.CreateTextureShader(source)` — it is cached per source handle.
5. **Never cache** a layer that hosts a shader effect in `SkiaScroll`,
   `SkiaDrawer`, `SkiaCarousel`, or any layout that virtualizes — follow the
   standard DrawnUI caching rules for dynamic content.
6. **PROHIBITED: Do NOT cache controls with GPU-surface shaders using
   `Operations` or `GPU` cache types.** `Operations` records draw commands into
   an `SKPicture` which cannot replay GPU-surface shader programs. `GPU` cache
   creates its own GPU surface that conflicts with the shader's surface
   requirements. Use `Image`, `ImageDoubleBuffered`, or `ImageComposite`
   instead.
7. **PROHIBITED: Do NOT nest children that use GPU-backed cache types (`GPU`,
   `ImageCompositeGPU`) inside a parent cached with `Operations`** —
   `SKPicture` recording cannot capture GPU-surface output from children.

Breaking these rules turns a 60 FPS render loop into a GC-thrashing one —
every disposed-then-rebuilt uniforms/children pair is a native handle round
trip and a managed allocation.

## Lifecycle and disposal

- `SkiaShader.DisposeCompiled()` tears down the compiled effect **and** its
  cached uniforms/children/texture shader (they are all bound to the effect).
  Call this before recompiling.
- `SkiaShader.Dispose()` releases everything including the owned paint.
- `SkiaShaderEffect.OnDisposing()` disposes the engine automatically. If you
  attach an effect and later remove it, dispose it explicitly:

  ```csharp
  _image.VisualEffects.Remove(_effect);
  _effect.Dispose();
  ```

## Debugging

- **Compile errors:** hook `OnCompilationError` (fluent `.OnShaderError(...)`)
  to get the SkSL compiler message. Without a handler the error is logged as
  `[SkiaShaderEffect] Failed to compile shader`. Either way a failed shader is
  not recompiled every frame; changing `ShaderCode` or `ShaderSource` retries.
- **Exception every frame** ("Variable was not found for name" or "Unable to
  write a ... value to a ... uniform"): a standard uniform is missing from the
  shader or declared with the wrong type — see [Standard uniforms](#standard-uniforms).
- **Nothing visible:** check the log for a compile error, check that the shader
  returns the texture on a cached control (see [How the effect draws](#how-the-effect-draws)),
  and check that it declares `iImage1` when `UseBackground` is not `Never`.
- **Animation frozen:** nothing is redrawing the control — see
  [Animation and iTime](#animation-and-itime).
- **Output shifted:** sample with `fragCoord - iOffset`, not `fragCoord`.
- Line endings in `.sksl` files are normalised automatically via
  `SkiaShader.NormalizeLineEndings` — no need to worry about CRLF/LF mixing.

## Slide transitions — SkiaShaderCarousel

`SkiaShaderCarousel` (`DrawnUi.Controls`) renders slide changes with an SkSL transition
instead of translating slides: an attached `ShaderTransitionEffect` (a
`ShaderDoubleTexturesEffect` adding `progress` and `ratio` uniforms) blends the cached
images of the outgoing and incoming cells.

```xml
<draw:SkiaShaderCarousel
    IsLooped="True"
    LinearSpeedMs="750"
    TransitionShader="Shaders/transitions/cube.sksl"
    ItemsSource="{Binding Items}">
    <!-- cell template MUST use UseCache="Image" — the effect samples cell caches -->
</draw:SkiaShaderCarousel>
```

The shader is a gl-transitions style `transition(vec2 uv)` function using
`getFromColor` / `getToColor` — any transition from
[gl-transitions](https://github.com/gl-transitions/gl-transitions) ported to SkSL works.
Provide it via:

- `TransitionShader` — path inside Resources/Raw
- `TransitionShaderCode` — raw SkSL string (required on OpenTK/`DRAWNUI_NET`)
- `TransitionTemplate` — replace the built-in adapter template
  (`ShaderTransitionEffect.DefaultTemplate`) when you need custom uniforms or sampling.
  A custom template must declare every standard uniform, including `iTime` and `iMouse`.

The built-in template flips Y inside `getFromColor` / `getToColor`; a ported
transition's own `main` computes `uv` from `(fragCoord - iOffset) / iResolution`
and flips `uv.y` before calling `transition(uv)`.

Behavior notes:

- One gesture moves at most ONE slide; direction comes from velocity or displacement,
  the target from the gesture-origin snap point.
- A swipe during a running transition wraps it up within `InterruptedTransitionMs`
  (default 50) and then plays the next transition.
- Override `CreateTransitionEffect()` to plug a custom `ShaderTransitionEffect`
  subclass (extra uniforms, render-area clipping).

### Letterboxed slides (AspectFit)

When slides letterbox their content (photo viewer), the transition spans the whole
cell — a cube's reflection projects at screen bottom instead of under the photo. Fix
with a custom effect: clip `ctx.Destination` in `Render` to the union of the from/to
images' `DisplayRect`, re-anchor `GetEngine().Offset` to the clipped origin in
`SyncEngineState`, and remap `getFromColor`/`getToColor` sampling into that band via
custom uniforms (cell textures stay full-size). For a single-texture effect the clip
alone is enough — with `AutoCreateInputTexture` the snapshot is taken from the clipped
destination.

## See also

- [Drawing Pipeline](drawing-pipeline.md) — where effects fit in the render loop
- [Visual Effects](controls/effects.md) — effect kinds and how effects affect caching
- [Fluent C# Extensions](fluent-extensions.md) — attaching effects from code
- [Carousels](controls/carousels.md) — `SkiaShaderCarousel` details and gallery patterns
