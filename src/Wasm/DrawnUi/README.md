
# DrawnUi.Web

Pure .NET for WebAssembly, no Blazor deps.

DrawnUI rendering for the browser via `[JSImport]`/`[JSExport]` only — no `IJSRuntime`, no
`ElementReference`, no Razor components. It builds on the **`DRAWNUI_NET`** code path (SharedNet +
Net shims, the same base used by the OpenTK target), *not* the Blazor target. A
`WebSkiaView : ISkiaDrawable` is attached to a `Canvas`, supporting both raster (CPU) and
GPU (WebGL) rendering.

## Quick start

```bash
dotnet build src/Web/DrawnUi.Web.Sample/DrawnUi.Web.Sample.csproj -c Debug
dotnet run   --project src/Web/DrawnUi.Web.Sample/DrawnUi.Web.Sample.csproj -c Debug
# → http://localhost:5000
```

Minimal host wiring (`Program.cs`):

```csharp
Super.UseDrawnUi().Build();
await JSHost.ImportAsync("drawnui-web", "/drawnui-web.js");
Super.Init();

var canvas = CreateCanvas();                 // your DrawnUI Canvas + content
var view   = new WebSkiaView("drawnui-canvas", OnRenderFrame);
view.SetDpi(dpr); view.SetSize(size);
view.OnDraw = (surface, rect) => canvas.RenderExternalSurface(surface, rect, frameNanos);

var useGpu = view.Init(preferGPU: true);     // 1. decide GPU vs raster FIRST
canvas.RenderingMode = useGpu && view.IsHardwareAccelerated   // 2. set mode BEFORE attaching
    ? RenderingModeType.Accelerated : RenderingModeType.Default;
canvas.AttachCanvasView(view);               // 3. attach
view.SetEnableRenderLoop(true);              // 4. start loop
```

> The 1→4 order is mandatory — see *Init ordering* below.

## Architecture

| Piece | Role |
|---|---|
| `WebSkiaView` | `ISkiaDrawable`; owns GL/raster surface + render loop, drawn into by the `Canvas`. |
| `SkiaHtmlCanvasInterop` | `[JSImport]`/`[JSExport]` bridge to `drawnui-web.js` (init, frame, blit). |
| `WebInput` | Routes browser pointer/wheel events into `Canvas.OnGestureEvent`. |
| `Super.Web` | `Super.Init`, DPI, frame loop entry (`OnBrowserFrame`). |
| `wwwroot/drawnui-web.js` | Canvas/GL plumbing (port of SkiaSharp.Views.Blazor `SKHtmlCanvas`). |
| `SkiaSharpInterop.js` | Emscripten JS library that exposes `GL`/`Module` (see below). |

The render loop is driven by JS `requestAnimationFrame` calling back into a C# `[JSExport]`.


### Emscripten JS

Why and how here.

#### The problem
SkiaSharp's GPU path needs the Emscripten `GL` object (it owns `GL.createContext` / `makeContextCurrent`, and the native `GRGlInterface` resolves WebGL function pointers through it). In the pure-.NET WASM runtime that `GL` object **exists inside the Emscripten module closure but is NOT exported to JS** — it's not in `EXPORTED_RUNTIME_METHODS`. Reaching for it from outside (`Module.GL`, `getDotnetRuntime().Module.GL`) doesn't just return null — it **aborts the runtime** (`'GL' was not exported`).

So normal JS can't see `GL`. We need code that runs *inside* the module closure, where `GL` and `Module` are in lexical scope.

#### The mechanism — an Emscripten JS library
`SkiaSharpInterop.js` isn't a normal script. It's an **Emscripten JS library**, merged into the module at link time:

```js
var SkiaSharpInterop = {
    InterceptBrowserObjects: function () {
        globalThis.SkiaSharpGL = GL          // GL/Module in scope HERE
        globalThis.SkiaSharpModule = Module
    }
}
mergeInto(LibraryManager.library, SkiaSharpInterop)
```

`mergeInto` makes `InterceptBrowserObjects` a **native symbol** compiled into the wasm. Its body executes inside the Emscripten closure, so it can grab `GL`/`Module` and re-expose them on `globalThis` — the one legal escape hatch.

#### How it's wired (three pieces)
1. **Link it** — `--js-library=SkiaSharpInterop.js` via `EmccExtraLDFlags` (in `buildTransitive/DrawnUi.Web.props`). Requires `WasmBuildNative=true` so emcc relinks. This is exactly how SkiaSharp.Views.Blazor does it.
2. **Call it from C#** — `[DllImport("libSkiaSharp", EntryPoint="InterceptBrowserObjects", CallingConvention=Cdecl)]`, invoked once via `EnsureBrowserObjectsIntercepted()` before GL init. (Everything links into one wasm, so the symbol resolves even though it's JS-defined.)
3. **Consume it** — `getGL()` in `drawnui-web.js` reads **only** `globalThis.SkiaSharpGL`. After the intercept call it's populated; before, it's null → raster fallback. Critically, `getGL()` must never probe `Module.GL` directly (that's the runtime-aborting path).

#### Why this approach vs alternatives
- Couldn't add `GL` to `EXPORTED_RUNTIME_METHODS` cleanly without overriding the dotnet runtime's own export list (fragile, risks breaking the runtime).
- Couldn't create a plain `canvas.getContext('webgl2')` ourselves — SkiaSharp native must share the *same* Emscripten-tracked context, or `glXXX` calls hit the wrong/no context.
- The js-library + `InterceptBrowserObjects` is the proven SkiaSharp pattern (originally from the Avalonia team, dotnet/runtime#76077), so it's the consistent, low-risk path.

### Two more GPU gotchas (besides the `GL` export)

Even once `GL` is reachable, two things silently killed the GPU path:

1. **A canvas can hold only ONE context type.** The legacy `initCanvas` used to call
   `getContext('2d')` on `drawnui-canvas`, which *permanently* blocks `getContext('webgl2')` on
   that element. `initCanvas` must NOT acquire a 2D context — the raster fallback lazily gets 2D
   inside `putImageData` only when GPU init fails.

2. **Init ordering — set `RenderingMode` BEFORE `AttachCanvasView`.** Changing `RenderingMode`
   triggers `DrawnView.OnHardwareModeChanged → CreateSkiaView → DestroySkiaView`, which **disposes
   the attached `WebSkiaView`** and removes it from the JS view map. A later `SetEnableRenderLoop`
   then no-ops (element gone) → loop never starts → blank canvas. Decide GPU/raster from `Init()`
   first, set `RenderingMode`, *then* attach, *then* start the loop.

## Input & gestures

`WebInput` forwards browser pointer events to `Canvas.OnGestureEvent` (down / move / up / cancel),
including tap synthesis. Wheel mirrors the Net `GestureRobot.WheelScroll` contract:
`TouchActionType.Wheel` + `WheelEventArgs { Delta, Scale = 1f, Center }`. Browser `deltaY > 0`
(scroll down) maps to a **negative** `Delta` so content scrolls down (DrawnUI decreases
`ViewportOffsetY` when scrolling down; `SkiaScroll.ApplyWheelScroll` uses only `Math.Sign(Delta)`).
`SkiaScroll` routes wheel to **zoom** unless `ZoomLocked = true` — set it for wheel scrolling.

## Build notes

- Sample SDK is `Microsoft.NET.Sdk.BlazorWebAssembly` with `WasmBuildNative=true` — required so
  emcc relinks the native wasm with the GPU js-library. Edits to `EmccExtraLDFlags` or native
  inputs trigger a slow emcc relink ("Linking with emcc…").
- **Emscripten console noise** (`writeI53ToI64… is a library symbol…`, `buffer is not longer
  defined`) is **non-fatal** — SkiaSharp native is linked with `ERROR_ON_UNDEFINED_SYMBOLS=0`.
  Filter it out; it is not a failure.

## Distribution

`buildTransitive/DrawnUi.Web.props` + `SkiaSharpInterop.js` are packed into the NuGet
`buildTransitive/` folder, so **package consumers get the GPU js-library flag automatically** — no
manual emcc flags. `ProjectReference` consumers do not auto-import `buildTransitive`, so the sample
imports the props explicitly. Single source of truth — do not duplicate the flag.

## Troubleshooting

| Symptom | Cause |
|---|---|
| All `_framework/*.wasm` 404, integrity `47DEQpj8…` (empty hash) | Stale `dotnet run` server or cached 404s. Kill the old server; hard-reload (Ctrl+Shift+R). `blazor.boot.json:404` is normal on .NET 10 (uses `dotnet.boot.js`). |
| `Emscripten GL module not found`, falls back to raster | js-library not linked / `InterceptBrowserObjects` not called. |
| Runtime aborts with `'GL' was not exported` | JS touched `Module.GL` directly. `getGL()` must read only `globalThis.SkiaSharpGL`. |
| GPU inits but canvas blank | Init ordering — `RenderingMode` set after `AttachCanvasView` disposed the view. |
| GPU never selected, only raster | `initCanvas` acquired a 2D context, poisoning the canvas for WebGL. |

GPU validates only in a browser with real WebGL; headless WebGL screenshots are unreliable
(`preserveDrawingBuffer:0` clears post-composite). Confirm visually, or verify the render loop with
a `requestAnimationFrame` call counter.