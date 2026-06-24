# DrawnUi.Web — pure WebAssembly (no Blazor)

DrawnUI rendering for browser WASM via `[JSImport]`/`[JSExport]` only — no `IJSRuntime`,
no `ElementReference`, no Razor. Builds on the **`DRAWNUI_NET`** path (SharedNet + Net shims,
same base as OpenTK), NOT the Blazor target. `WebSkiaView : ISkiaDrawable` is attached to a
`Canvas` via `AttachCanvasView`, enabling both `RenderingMode.Default` (raster) and
`Accelerated` (WebGL).

## Build / run

```bash
dotnet build src/Web/DrawnUi.Web.Sample/DrawnUi.Web.Sample.csproj -c Debug
dotnet run   --project src/Web/DrawnUi.Web.Sample/DrawnUi.Web.Sample.csproj -c Debug   # serves http://localhost:5000
```

Sample csproj is `Microsoft.NET.Sdk.BlazorWebAssembly` with `WasmBuildNative=true` (needed so
emcc relinks with the GPU js-library). Any edit to `EmccExtraLDFlags` or native inputs triggers
a slow emcc relink ("Linking with emcc…").

## GPU (WebGL) path — the traps that cost a day

The GPU path silently fell back to raster for three independent reasons. All fixed; do not regress.

1. **Emscripten `GL` is not reachable from JS.** It is not in `EXPORTED_RUNTIME_METHODS`, and
   touching `Module.GL` / `getDotnetRuntime().Module.GL` from JS **aborts the .NET runtime**
   (`'GL' was not exported`). Solution mirrors SkiaSharp.Views.Blazor: ship `SkiaSharpInterop.js`
   (an emcc `mergeInto` library), link it via `--js-library` (see `buildTransitive/DrawnUi.Web.props`),
   and call the native `[DllImport("libSkiaSharp")] InterceptBrowserObjects()` once before GL init
   (`SkiaHtmlCanvasInterop.EnsureBrowserObjectsIntercepted`). It runs *inside* the module closure
   and stashes `GL`/`Module` on `globalThis.SkiaSharpGL` / `SkiaSharpModule`. `getGL()` in
   `drawnui-web.js` must read ONLY `globalThis.SkiaSharpGL` — never scan `Module.GL` (crashes).

2. **A canvas can hold only ONE context type.** The legacy `initCanvas` used to grab
   `getContext('2d')` on `drawnui-canvas`, which permanently blocks `getContext('webgl2')`.
   `initCanvas` must NOT acquire a 2D context; the raster fallback lazily gets 2D in `putImageData`
   only when GPU init fails.

3. **`RenderingMode` must be set BEFORE `AttachCanvasView`.** Changing `RenderingMode` triggers
   `DrawnView.OnHardwareModeChanged → CreateSkiaView → DestroySkiaView`, which **disposes the
   attached `WebSkiaView`** and `views.delete()`s it from the JS map. A later `SetEnableRenderLoop`
   then no-ops (element gone) → render loop never starts → blank canvas. Order in `Program.cs`:
   `Init()` → set `RenderingMode` → `AttachCanvasView` → `SetEnableRenderLoop(true)`.

GPU validates only in a browser with real WebGL. Headless screenshots of WebGL are unreliable
(`preserveDrawingBuffer:0` clears post-composite); confirm visually or via a rAF-tick counter.

## Wheel / gestures

`WebInput.OnWheel(deltaX, deltaY, deltaMode, x, y)` mirrors the Net `GestureRobot.WheelScroll`
contract: emits `TouchActionType.Wheel` carrying `WheelEventArgs { Delta, Scale=1f, Center }`.
Browser `deltaY > 0` (scroll down) maps to **negative** `Delta` (DrawnUI scrolls down when
`ViewportOffsetY` decreases; `SkiaScroll.ApplyWheelScroll` uses only `Math.Sign(Delta)`).
`SkiaScroll` routes wheel to **zoom** unless `ZoomLocked=true` — set it for wheel scrolling.

## Emscripten console noise

Flood of `[error] writeI53ToI64… is a library symbol…` / `buffer is not longer defined` is
**non-fatal** — SkiaSharp native is linked with `ERROR_ON_UNDEFINED_SYMBOLS=0`. Filter it out
when reading the console; it is not a failure.

## Distribution

`buildTransitive/DrawnUi.Web.props` + `SkiaSharpInterop.js` are packed into the NuGet
`buildTransitive/` folder, so package consumers get the GPU js-library flag automatically.
`ProjectReference` consumers (the sample) do NOT auto-import buildTransitive — the sample
`<Import>`s the props explicitly. Single source of truth; don't duplicate the flag.

## Validating without Playwright MCP

When Playwright MCP isn't loaded, drive headless Chrome over the DevTools Protocol with Node 22
(global `WebSocket`): launch `chrome --headless=new --remote-debugging-port=9222
--use-gl=angle --enable-unsafe-swiftshader`, attach via `Target.attachToTarget {flatten:true}`,
`Network.setCacheDisabled` (Chrome caches stale-server 404s across runs), capture
`Runtime.consoleAPICalled` / `exceptionThrown`, dispatch `Input.dispatchMouseEvent`. Confirm the
render loop with a `requestAnimationFrame` call counter, not screenshots.
