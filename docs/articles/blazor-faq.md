# Blazor FAQ

## Which package do I install?

- install `DrawnUi.Blazor.Wasm` for browser-side / WebAssembly rendering
- install `DrawnUi.Blazor.Server` for Blazor Server and `InteractiveServer`
- in a mixed Blazor Web App, reference `DrawnUi.Blazor.Server` from the host and `DrawnUi.Blazor.Wasm` from the `.Client` project
- `DrawnUi.Blazor.Core` is the shared implementation foundation, not the normal top-level package choice for app projects

See also [Blazor Packages](blazor-packages.md).

## What is the difference between `Canvas` and `ServerCanvas`?

- `Canvas` is the browser-side DrawnUI surface used by the WASM/browser runtime
- `ServerCanvas` is a server-rendered DrawnUI surface that returns encoded image frames to the page and routes supported interactions back to the server
- if you expect browser-canvas semantics, local redraw, and local input processing, use `Canvas`

## Can I mix normal Razor/HTML UI with DrawnUI UI?

Yes.

This is one of the main intended use cases, especially for `DrawnUi.Blazor.Server` and mixed Blazor Web Apps.

## Can I use server and WASM DrawnUI in the same app?

Yes.

The supported pattern is to choose runtime at the component boundary and use sibling render-mode islands where needed.

See [Blazor Hybrid Web App](blazor-hybrid.md).

## Do normal Blazor concepts still apply?

Yes.

Routing, dependency injection, layouts, render modes, component composition, and ordinary Blazor state patterns remain the host app model.

What changes is the rendering surface and, in server mode, where rendering and interaction processing happen.

## Why do static assets still come from `DrawnUi.Blazor.Core` in mixed apps?

Because the current shared browser static asset flow is still anchored on the core browser implementation project.

This is an incremental packaging state in the repository, not the intended long-term consumer-facing mental model.

## Do I need a separate `.Client` project for WebAssembly render mode in a Blazor Web App?

Yes.

This follows the normal Blazor Web App mixed-render-mode rules. Components rendered with `InteractiveWebAssembly` must come from the client-side graph.

## What is the migration path for an existing Blazor app?

- start with a normal Blazor shell
- add one DrawnUI island where custom rendering pays off
- use `DrawnUi.Blazor.Server` first if the app is already server-hosted and the surface is event-driven
- use `DrawnUi.Blazor.Wasm` first if the target surface needs local responsiveness or animation-heavy rendering
- move more UI into DrawnUI only where the rendering or interaction model justifies it

See also [Blazor Migration](blazor-migration.md).

## What is supported today?

### Strong current fit

- browser/WASM DrawnUI surfaces through `Canvas`
- server-rendered DrawnUI surfaces through `ServerCanvas`
- standard Razor + DrawnUI coexistence on the same page
- same-app mixed server + WASM DrawnUI with sibling islands
- button / tap style interactions in the validated samples

### Not solved well yet

- `DrawnUi.Blazor.Server` is not a live browser canvas
- server-side high-FPS animation is not a good fit
- server-side drag-heavy, hover-heavy, or pointer-stream-heavy interaction is not at WASM parity
- rich text editing, advanced focus behavior, and browser-like text-input parity are not yet a solved story on the server path
- full parity with MAUI DrawnUI behavior is not available yet

See also [Blazor Capabilities](blazor-capabilities.md).

## Why does `DrawnUi.Blazor.Server` crash on Linux with `DllNotFoundException: libSkiaSharp`?

Two things are required for SkiaSharp to load on a Linux server:

**1. The native Linux package must be in the publish output.**

`DrawnUi.Blazor.Server` depends on SkiaSharp but the SkiaSharp NuGet package does not include Linux native assets by default on all feeds. Add this to your server host project:

```xml
<PackageReference Include="SkiaSharp.NativeAssets.Linux" Version="4.147.0-preview.2.1" />
```

Use the version from the [SkiaSharp EAP feed](https://aka.ms/skiasharp-eap/index.json) that matches your SkiaSharp version. After adding this, `runtimes/linux-x64/native/libSkiaSharp.so` will be included in `dotnet publish` output.

**2. `libfontconfig1` must be installed on the server.**

`libSkiaSharp.so` depends on `libfontconfig` as a system library. Even if the `.so` file is present, it will fail to load with `DllNotFoundException` if `libfontconfig1` is missing:

```bash
apt-get install -y libfontconfig1
```

Both are required. Missing either one produces a circuit-terminating exception on the first SkiaSharp call.

## Why do navigation links point to `%BASE_HREF%` after deploying WASM to nginx?

The `%BASE_HREF%` token in `wwwroot/index.html` is substituted only when an ASP.NET host processes the publish output. On a standalone nginx static deploy the token stays literal, so `<base href="%BASE_HREF%">` becomes the actual base and all Blazor Router links resolve against it.

Fix: edit `wwwroot/index.html` in source before publishing:

```html
<base href="/" />
```

Then republish and redeploy. If users already hit the site, their service worker may have cached the broken `index.html`. They need a hard reload or service-worker unregister:

```js
(await navigator.serviceWorker.getRegistrations()).forEach(r => r.unregister());
// then hard-reload
```

## Why does my Blazor WASM app show the loading spinner forever with `Unexpected token '<'` in the console?

`dotnet publish` emits a fingerprinted JS file (`blazor.webassembly.abc123.js`) but `index.html` references the bare name `blazor.webassembly.js`. nginx has no such file and returns `index.html` instead (due to SPA fallback routing), which the browser then tries to parse as JavaScript and fails.

Fix — create a symlink in `_framework/` on the server after each deploy:

```bash
cd /var/www/{domain}/www/_framework
ln -sf $(ls blazor.webassembly.*.js | head -1) blazor.webassembly.js
```

Re-run this after every redeploy because rsync replaces real files and does not restore the symlink.

## Which runtime should I choose?

- choose `DrawnUi.Blazor.Wasm` when the DrawnUI surface should stay local and responsive
- choose `DrawnUi.Blazor.Server` when the surface should be server-owned and event-driven
- choose a mixed Blazor Web App when one app needs both kinds of DrawnUI surfaces
