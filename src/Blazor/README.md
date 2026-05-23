# DrawnUI for Blazor

DrawnUI for Blazor brings the DrawnUI rendering and control model to Blazor in three package identities and two runtime shapes:

- `DrawnUi.Blazor.Core` as the shared implementation foundation
- `DrawnUi.Blazor.Wasm` for browser-side / WebAssembly rendering
- `DrawnUi.Blazor.Server` for server-rendered DrawnUI surfaces inside Blazor Server / `InteractiveServer` apps

This target is under active development. The current validated slice covers startup, `Canvas` hosting, font registration, keyboard support, and a growing set of shared controls and probes.

## Packages

Install the runtime package that matches where DrawnUI should execute:

```bash
dotnet add package DrawnUi.Blazor.Wasm
```

or:

```bash
dotnet add package DrawnUi.Blazor.Server
```

`DrawnUi.Blazor.Core` is the shared foundation package behind the runtime-specific packages. In normal app setup you should usually reference `Wasm` or `Server`, not `Core` directly.

The current project target is `net10.0`.

## Docs Map

The docs site now mirrors the main runtime split and adoption guidance:

- `docs/articles/blazor/index.md` as the entry point
- `docs/articles/blazor/packages.md` for package roles and install targets
- `docs/articles/blazor/wasm.md` for browser / WebAssembly setup
- `docs/articles/blazor/server.md` for the server-backed `Canvas` and server rendering
- `docs/articles/blazor/hybrid.md` for mixed `InteractiveServer` + `InteractiveWebAssembly`
- `docs/articles/blazor/capabilities.md` for current fit and limitations
- `docs/articles/blazor/migration.md` for adoption guidance in existing apps
- `docs/articles/blazor/faq.md` for common consumer questions

## Quick Start

Initialize DrawnUI in `Program.cs`:

```csharp
using DrawnUi.Draw;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

DrawnExtensions.RegisterFont("FontText", FontWeight.Regular, "/fonts/OpenSans-Regular.ttf");

var host = await builder.UseDrawnUiAsync(new DrawnUiStartupSettings
{
    UseDesktopKeyboard = true
});

await host.RunAsync();
```

Use DrawnUI in a Razor page:

```razor
@page "/"
@using DrawnUi.Draw
@using DrawnUi.Views
@using Microsoft.Maui.Controls

<Canvas WidthRequest="400"
    HeightRequest="220"
    BackgroundColor="#F4F1E8"
    Content="@RootControl" />

@code {
    private readonly SkiaControl RootControl = new SkiaLayout()
    {
        Margin = new Thickness(16),
        Type = LayoutType.Column,
        Children =
        {
            new SkiaLabel()
            {
                Text = "Hello from DrawnUI Blazor",
                FontFamily = "FontText",
                FontSize = 24
            }
        }
    };
}
```

## Fonts

In Blazor, fonts should be registered with URL paths that are reachable as static web assets:

```csharp
DrawnExtensions.RegisterFont("FontText", FontWeight.Regular, "/fonts/OpenSans-Regular.ttf");
DrawnExtensions.RegisterFont("FontGame", FontWeight.Bold, "/fonts/Orbitron-Bold.ttf");
```

## Trimming

Blazor WebAssembly apps using DrawnUI may also need to root `SkiaSharp` for trimming:

```xml
<ItemGroup>
  <TrimmerRootAssembly Include="SkiaSharp" RootMode="EntireAssembly" />
</ItemGroup>
```

This is currently used by the Blazor sandbox sample.

## Sandbox Reference

The best working reference in this repository is:

- `src/Blazor/Samples/BlazorSandbox/`

For a server-hosted example that serves DrawnUI from an ASP.NET Core host, see:

- `src/Blazor/Samples/BlazorSandboxServer/`

For a mixed-mode Blazor Web App that hosts server and WASM DrawnUI components on the same page, see:

- `src/Blazor/Samples/BlazorSandboxHybrid/`

## Package Directions

Blazor support is intentionally split into runtime-specific packages because the rendering and input tradeoffs are different:

- `DrawnUi.Blazor.Core` is the shared foundation package
- `DrawnUi.Blazor.Wasm` is the browser-side / WebAssembly package
- `DrawnUi.Blazor.Server` is the server-side package for Blazor Server / `InteractiveServer`

This is not just packaging. The two modes have different performance envelopes, interaction characteristics, and ideal use cases.

## DrawnUi.Blazor.Wasm (WASM / Browser)

`DrawnUi.Blazor.Wasm` is the primary choice when DrawnUI is expected to behave like a local interactive rendering runtime in the browser.

Use it when you want:

- browser-side rendering with the existing `Canvas` implementation
- high-frequency redraw or animation-heavy content
- gesture-heavy controls and interaction paths
- highly interactive surfaces where latency matters
- pages that are mostly or entirely DrawnUI-driven
- the closest experience to a full DrawnUI app running locally in the browser

In practice this is the best fit for:

- highly animated scenarios
- scrolling and gesture-heavy content
- high-FPS or game-like surfaces
- fully drawn pages or apps where most of the screen is DrawnUI
- cases where input and rendering must stay local instead of paying server round trips

Why this package exists:

- rendering happens in the browser
- input stays in the browser
- state changes do not require a server-generated frame for every visual response
- this gives the best path for responsiveness, animation smoothness, and rich interaction

Current validated path:

- browser-hosted DrawnUI with the existing `Canvas` path

Current best reference:

- `src/Blazor/Samples/BlazorSandbox/`

Important nuance:

- `DrawnUi.Blazor.Wasm` is not only for animation. It is also the preferred package when the page is primarily a DrawnUI surface rather than a standard Blazor page with a few embedded DrawnUI islands.

Another nuance:

- A Blazor Server-hosted solution can still choose this package if the actual DrawnUI surface runs in a browser-side client. The deciding factor is where rendering and interaction should live, not only what kind of ASP.NET host wraps the app.

Packaging nuance:

- `DrawnUi.Blazor.Core` currently carries the shared implementation foundation used by the WASM runtime package.
- For consumers, `DrawnUi.Blazor.Wasm` should be treated as the browser install target.

## DrawnUi.Blazor.Server (Blazor Server / InteractiveServer)

`DrawnUi.Blazor.Server` is the right choice when you want DrawnUI integrated into a Blazor Server app and the DrawnUI surface is relatively low-animation, event-driven, or state-change driven rather than continuously animated.

Use it when you want:

- DrawnUI surfaces inside a classic Blazor Server / `InteractiveServer` application
- integrated drawn controls without moving the whole app to a WASM-first runtime
- server-owned rendering for lower-frequency UI updates
- screens that update on taps, form actions, navigation, selection changes, or occasional rerenders

In practice this is the best fit for:

- low-animation scenarios
- event-driven controls and dashboards
- mixed pages where standard Blazor UI and DrawnUI live together
- server-hosted apps that need drawn controls but do not need sustained high-FPS animation

The current implementation uses `DrawnUi.Net` under the hood to render a DrawnUI control tree offscreen and return encoded image frames to the page.

Today this package is best described as:

- server-side DrawnUI scene rendering
- encoded frame delivery to the page
- browser click routing back into the shared DrawnUI gesture pipeline for supported interactions
- standard Blazor Server state updates plus DrawnUI rerendering on the server

Current validated path:

- Blazor Server / `InteractiveServer` pages that render DrawnUI on the server and display the result through `Canvas`

Current best reference:

- `src/Blazor/Samples/BlazorSandboxServer/`

Important nuance:

- This package is not limited to tiny widgets. It can render larger or even fully drawn pages, but it is most appropriate when visual updates are relatively infrequent. The more a surface behaves like an animation engine or game loop, the worse the server round-trip model becomes compared to browser-side rendering.

Another nuance:

- `DrawnUi.Blazor.Server` should be presented as the recommended path for low-animation and integrated drawn controls, not as a claim of full parity with the browser `Canvas` runtime. That parity does not exist yet.

Current limitations and scope:

- the server path is not a live browser `Canvas`; it is a server-rendered surface delivered to the page
- the current validated slice covers end-to-end server rendering, shared-state updates, and clickable drawn button-style interactions in the sample
- this is not yet the right path for highly animated scenes, drag-heavy interaction, advanced pointer work, rich text editing, or other scenarios that depend on continuous local input processing
- hover, pointer-move, drag, wheel, focus, and text-input parity are not yet equivalent to the browser/WASM runtime

Recommended positioning:

- `DrawnUi.Blazor.Server` for integrated, low-animation, event-driven DrawnUI in Blazor Server
- `DrawnUi.Blazor.Wasm` for high-interactivity, high-animation, or mostly/fully drawn browser UI

### Server Setup

1. Create or use an ASP.NET Core Blazor Web App host.
2. Reference `DrawnUi.Blazor.Server`.
3. Register the server services with `AddDrawnUiBlazorServer()`.
4. Render DrawnUI content through `Canvas` on an `InteractiveServer` page.

Host setup example:

```csharp
using DrawnUi.Blazor.Server;
using BlazorSandboxServer.Components;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddDrawnUiBlazorServer();

var app = builder.Build();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
```

Host page example:

```razor
@page "/drawn"
@rendermode InteractiveServer

<Canvas Content="@BuildCanvasContent()"
    WidthRequest="400"
    HeightRequest="240"
    Alt="DrawnUI server-rendered sample" />
```

Scene builder example:

```csharp
using DrawnUi.Draw;
using DrawnUi.Views;

private SkiaControl BuildCanvasContent()
{
    return new SkiaLayout()
    {
        WidthRequest = 368,
        HeightRequest = 208,
        Type = LayoutType.Column,
        Spacing = 12,
        Children =
        {
            new SkiaLabel { Text = "DrawnUI server canvas", FontSize = 24 },
            new SkiaButton("Increment"),
            new SkiaLabel { Text = $"Clicks: {clickCount}", FontSize = 15 },
            new SkiaLabel { Text = $"Status: {statusLabel}", FontSize = 15 }
        }
    };
}
```

Operational notes:

- Build a fresh `SkiaControl` tree for each page render or each server redraw. Reusing the same control instances across disposed headless hosts is not reliable.
- For local development, `builder.WebHost.UseStaticWebAssets();` helps the host serve package static assets correctly.
- The server package should currently be treated as a DrawnUI-for-Blazor-Server integration layer optimized for lower-frequency UI updates, not as a replacement for the browser-side runtime in animation-heavy scenarios.

## Choosing Between Them

### Runtime Decision Guide

| Scenario | Package | Rendering happens | Best fit | Avoid when |
| --- | --- | --- | --- | --- |
| Blazor WebAssembly app with local interaction | `DrawnUi.Blazor.Wasm` | In the browser | Fully drawn pages, animation-heavy surfaces, gesture-heavy UI, local responsiveness | You specifically want the server to own every visual update |
| Blazor Server or `InteractiveServer` app with embedded DrawnUI islands | `DrawnUi.Blazor.Server` | On the server, delivered as image frames | Event-driven widgets, dashboards, mixed Razor + DrawnUI pages, lower-frequency UI updates | You need high-FPS animation, drag-heavy interaction, or browser-canvas parity |
| Blazor Web App using both modes | `DrawnUi.Blazor.Server` in host, `DrawnUi.Blazor.Wasm` in `.Client` | Split per component boundary | Mixed apps where some DrawnUI surfaces stay local and some stay server-owned | You expect one nested component tree to switch render modes freely |

Use `DrawnUi.Blazor.Wasm` when rendering and interaction need to stay local in the browser.

Use `DrawnUi.Blazor.Server` when DrawnUI needs to live naturally inside a Blazor Server app and the surface is mostly event-driven instead of continuously animated.

For hybrid Blazor Web Apps that use both render modes in one app:

- reference `DrawnUi.Blazor.Server` from the server host project
- reference `DrawnUi.Blazor.Wasm` from the `.Client` project
- keep shared scene/model code in runtime-agnostic code where practical
- choose runtime at the component boundary; do not try to switch a child component to a different interactive mode than its parent
- the `src/Blazor/Samples/BlazorSandboxHybrid/` sample is validated with sibling `InteractiveServer` and `InteractiveWebAssembly` DrawnUI panels on the same `/hybrid` page, including standard Blazor button updates and DrawnUI button interactions in both panels

Current incremental packaging nuance for mixed apps in this repository:

- the host may still need the shared browser asset project in its graph so `_content/DrawnUi.Blazor.Core/*` assets are served for the WASM runtime
- the dedicated `DrawnUi.Blazor.Wasm` package identity exists, but the underlying shared static asset flow is still anchored on the current core browser implementation project

If you are unsure, the fastest rule of thumb is:

- choose WASM/browser for highly animated scenarios or fully drawn pages
- choose server for integrated drawn controls and low-animation surfaces in a Blazor Server app

## Common Blazor Questions

### Which package do I install?

- install `DrawnUi.Blazor.Wasm` for browser-side / WebAssembly rendering
- install `DrawnUi.Blazor.Server` for Blazor Server / `InteractiveServer`
- in a mixed Blazor Web App, reference `DrawnUi.Blazor.Server` from the host and `DrawnUi.Blazor.Wasm` from the `.Client` project
- `DrawnUi.Blazor.Core` is the shared implementation foundation and is not the normal top-level app package choice

### Does Blazor still use different canvas names for WASM and Server?

- no; both runtimes use `Canvas` as the public component name
- `DrawnUi.Blazor.Wasm` provides the browser-side `Canvas`
- `DrawnUi.Blazor.Server` provides the server-backed `Canvas`
- in single-host apps you do not set an extra host-selection property; the runtime is implied by the package and component graph
- in mixed Blazor Web Apps, choose runtime at the component boundary and keep using `Canvas` on both sides

### Can I mix normal Razor/HTML UI with DrawnUI UI?

- yes
- this is a primary use case for `DrawnUi.Blazor.Server`
- the hybrid sample also validates that standard Blazor buttons and DrawnUI buttons can coexist on the same page while using different render modes in sibling components

### Can I use server and WASM DrawnUI in the same app?

- yes
- the supported pattern is to choose render mode at the component boundary
- the current validated sample is `src/Blazor/Samples/BlazorSandboxHybrid/`, where sibling `InteractiveServer` and `InteractiveWebAssembly` panels run on the same `/hybrid` page
- do not expect a child component to switch to a different interactive mode than an already-interactive parent

### Do normal Blazor concepts still apply?

- yes
- routing, dependency injection, layouts, render modes, component composition, and normal Blazor state patterns are still the host app model
- what changes is the rendering surface and, for server mode, the rendering ownership and interaction round-trip model

### Why do static assets still come from `DrawnUi.Blazor.Core` in mixed apps?

- because the current shared browser static asset flow is still anchored on the core browser implementation project
- this is an incremental packaging state in the repository, not the intended long-term consumer-facing mental model
- for now, mixed hosts may still need the core browser asset project in the host graph so `_content/DrawnUi.Blazor.Core/*` assets are served for WASM-side DrawnUI

### Do I need a separate `.Client` project for WebAssembly render mode in a Blazor Web App?

- yes
- this follows normal Blazor Web App mixed-render-mode rules
- any component rendered with `InteractiveWebAssembly` must come from the client-side project graph

### What is the migration path for an existing Blazor app?

- start with a normal Blazor shell and add one DrawnUI island for the part of the UI that benefits from custom rendering
- use `DrawnUi.Blazor.Server` first if the app is already server-hosted and the surface is event-driven
- use `DrawnUi.Blazor.Wasm` first if the target surface needs local responsiveness or animation-heavy rendering
- move more UI into DrawnUI only where the rendering or interaction model justifies it

## Supported Today

| Capability | `DrawnUi.Blazor.Wasm` | `DrawnUi.Blazor.Server` | Notes |
| --- | --- | --- | --- |
| Standard Razor + DrawnUI on same page | Yes | Yes | Validated in samples |
| Same-app mixed server + WASM DrawnUI | Yes | Yes | Validated in `BlazorSandboxHybrid` with sibling islands |
| Same-page sibling server + WASM DrawnUI panels | Yes | Yes | Supported at component boundaries |
| Button / tap style interactions | Yes | Yes | Validated in current samples |
| Shared-state updates reflected in DrawnUI scene | Yes | Yes | Validated in current samples |
| Fully drawn browser page | Yes | Not the target | Prefer WASM |
| Low-animation dashboard / widget use case | Yes | Yes | Server mode is designed for this slice |
| High-FPS animation | Yes | No | Server round trips are the limiter |
| Drag-heavy continuous interaction | Better fit | Not ready | Server parity is not there |
| Hover / pointer-move parity | Better fit | Not ready | Server parity is not there |
| Wheel input parity | Better fit | Not ready | Server parity is not there |
| Rich text input / focus parity | Better fit | Not ready | Server parity is not there |

## Good Fit Use Cases

### Good fit for `DrawnUi.Blazor.Wasm`

- fully drawn pages or app sections in the browser
- highly interactive surfaces that should stay local
- animation-heavy views
- gesture-heavy or high-redraw scenarios
- game-like or canvas-like UI where server round trips would be a bad fit

### Good fit for `DrawnUi.Blazor.Server`

- existing Blazor Server apps that want embedded DrawnUI widgets
- mixed Razor + DrawnUI pages
- event-driven dashboards, control surfaces, and inspector-style panels
- low-animation scenarios where server-owned rendering is acceptable
- gradual adoption inside a conventional Blazor Server app

### Good fit for hybrid Blazor Web Apps

- apps that want one server-owned DrawnUI panel and one local browser-owned DrawnUI panel in the same app
- apps that need runtime choice per feature instead of per whole application
- incremental migration where some DrawnUI surfaces can stay server-hosted while more demanding ones move to WASM

## Not Solved Well Yet

- `DrawnUi.Blazor.Server` is not a live browser canvas and should not be positioned that way
- server-side high-FPS animation is not a good fit
- server-side drag-heavy, hover-heavy, or pointer-stream-heavy interaction is not yet solved to WASM parity
- server-side rich text editing, advanced focus behavior, and browser-like text-input parity are not yet a solved story
- one-package abstraction that hides runtime choice is not the current model; developers still need to choose the runtime intentionally
- full parity with MAUI DrawnUI behavior is not available yet
- mixed-app packaging for external consumers still has an incremental static-asset nuance because the host may need the core browser implementation project in graph

## What To Expect As A Blazor Developer

- if you think in normal Blazor terms first and DrawnUI rendering terms second, you are approaching this correctly
- DrawnUI is currently best adopted as a specialized rendering surface inside a normal Blazor app architecture
- the most important decision is not "server or wasm host app" but "where should this specific DrawnUI surface render and where should its interactions be processed"
- if that answer is "local and responsive," choose `DrawnUi.Blazor.Wasm`
- if that answer is "server-owned and event-driven," choose `DrawnUi.Blazor.Server`

## Status

Blazor support is in progress, not yet full MAUI parity.

For current progress and validated slices, see:

- `docs/articles/blazor/index.md`
- `docs/shared-maui-blazor-port-status.md`