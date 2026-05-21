# Blazor Packages

This page explains what each DrawnUI Blazor package is for and which project should reference it.

## Package Roles

| Package | Role | App should reference directly? |
| --- | --- | --- |
| `DrawnUi.Blazor.Core` | Shared implementation foundation for the browser-side stack | Usually no |
| `DrawnUi.Blazor.Wasm` | Browser / WebAssembly DrawnUI runtime | Yes |
| `DrawnUi.Blazor.Server` | Server-rendered DrawnUI runtime for Blazor Server / `InteractiveServer` | Yes |

## Normal Install Choices

Use one of these in normal app setup:

```bash
dotnet add package DrawnUi.Blazor.Wasm
```

or:

```bash
dotnet add package DrawnUi.Blazor.Server
```

`DrawnUi.Blazor.Core` exists so the runtime-specific packages can share implementation. It is not the normal consumer-facing install target.

## Which Project References Which Package?

### Standalone WebAssembly app

- app project references `DrawnUi.Blazor.Wasm`

### Blazor Server or Blazor Web App using only server mode

- host project references `DrawnUi.Blazor.Server`

### Mixed Blazor Web App

- host project references `DrawnUi.Blazor.Server`
- `.Client` project references `DrawnUi.Blazor.Wasm`
- browser-interactive DrawnUI components live in the client-side graph

## Why The Split Exists

The split is intentional because the runtime models are materially different:

- browser/WASM keeps rendering and interaction local
- server mode renders DrawnUI on the server and returns encoded frames to the page
- mixed apps can use both, but only at explicit component boundaries

This is not cosmetic packaging. It reflects different performance envelopes, interaction behavior, and page architecture.

## Static Asset Note

The current browser-side static asset flow still serves some assets from the shared core implementation path:

- `_content/DrawnUi.Blazor.Core/*`

Treat that as an implementation detail of the current packaging stage, not as the long-term consumer mental model.

## Publishing to Linux

`DrawnUi.Blazor.Server` uses SkiaSharp on the server. Publishing to Linux requires two additional steps.

### Step 1 — Add the Linux native asset package

The SkiaSharp NuGet package does not include `linux-x64` native binaries by default. Add this to your server host project:

```xml
<PackageReference Include="SkiaSharp.NativeAssets.Linux" Version="4.147.0-preview.2.1" />
```

The correct version to use is the one from the [SkiaSharp EAP feed](https://aka.ms/skiasharp-eap/index.json) that matches your SkiaSharp version. After adding this, `dotnet publish` will include `runtimes/linux-x64/native/libSkiaSharp.so` in the output.

To verify before deploying:
```bash
ls publish-output/runtimes/linux-x64/native/
# should show: libSkiaSharp.so  libHarfBuzzSharp.so
```

### Step 2 — Install `libfontconfig1` on the server

`libSkiaSharp.so` has a runtime dependency on `libfontconfig`. Install it on the target server:

```bash
apt-get install -y libfontconfig1
```

Without this, `libSkiaSharp.so` loads but immediately fails to resolve its own dependencies, producing a `DllNotFoundException` on the first SkiaSharp type initializer (`SKTypeface`, `SKPaint`, etc.) and terminating the Blazor circuit.

Both steps are required. Missing either one causes the same visible symptom: an unhandled circuit exception in the browser console with no useful detail shown to the user.

## Publishing WASM to a standalone nginx server

Blazor WASM publishes to a static `wwwroot/` folder. When served by a plain nginx without an ASP.NET host, three things are required.

### Step 1 — Fix `base href` in source

`wwwroot/index.html` must have a literal base path, not the `%BASE_HREF%` placeholder:

```html
<base href="/" />
```

The `%BASE_HREF%` placeholder is substituted only by the ASP.NET publishing pipeline when hosting is configured. On a standalone nginx deploy it stays literal, breaking all Blazor Router links (they resolve relative to `%BASE_HREF%` instead of `/`).

### Step 2 — nginx SPA routing

```nginx
location / {
    try_files $uri $uri/ /index.html;
}
```

Without this, direct navigation to any Blazor route returns 404 from nginx.

### Step 3 — `blazor.webassembly.js` symlink

`dotnet publish` emits a fingerprinted JS file (e.g. `blazor.webassembly.abc123.js`) but `index.html` references the non-fingerprinted name `blazor.webassembly.js`. Create a symlink in `_framework/` after each upload:

```bash
cd /var/www/{domain}/www/_framework
ln -sf $(ls blazor.webassembly.*.js | head -1) blazor.webassembly.js
```

Without the symlink, nginx returns `index.html` for the JS request, causing `Unexpected token '<'` in the browser console and the app stuck at the loading spinner.

### After each rsync deploy

`rsync` from Windows (cwrsync) uploads files with the Windows UID. nginx (`www-data`) cannot read them. Fix ownership after every upload:

```bash
chown -R www-data:www-data /var/www/{domain}/www/
```

## Related Docs

- [Blazor](blazor.md)
- [Blazor WebAssembly](blazor-wasm.md)
- [Blazor Server](blazor-server.md)
- [Blazor Hybrid Web App](blazor-hybrid.md)
