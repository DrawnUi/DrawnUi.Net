# Blazor Hybrid Web App

This page covers the mixed Blazor Web App scenario where one app hosts both server-rendered DrawnUI and browser/WASM DrawnUI.

## What "Hybrid" Means Here

In this repository, hybrid means:

- a Blazor Web App host that supports both `InteractiveServer` and `InteractiveWebAssembly`
- a `.Client` project for the WebAssembly-side component graph
- DrawnUI runtime choice made at component boundaries

It does not mean nesting one interactive mode inside an already-interactive parent of another mode.

## Validated Pattern

The validated pattern is a static or SSR host page that mounts sibling components with different render modes.

Current working sample:

- `src/Blazor/Samples/BlazorSandboxHybrid/`

The `/hybrid` route validates:

- one sibling `InteractiveServer` DrawnUI panel
- one sibling `InteractiveWebAssembly` DrawnUI panel
- standard Blazor button updates in both panels
- DrawnUI button interactions in both panels

## Project Shape

Typical mixed-app structure:

- server host project references `DrawnUi.Blazor.Server`
- `.Client` project references `DrawnUi.Blazor.Wasm`
- components rendered with `InteractiveWebAssembly` live in the client-side graph
- server-only DrawnUI panels can live in the host or in a server-side RCL

## Host Setup

```csharp
using BlazorSandboxHybrid.Components;
using DrawnUi.Blazor.Server;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddDrawnUiBlazorServer();

var app = builder.Build();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode();

app.Run();
```

## Example Page Shape

```razor
<div class="hybrid-grid">
    <section>
        <ServerDrawnPanel />
    </section>

    <section>
        <WasmDrawnPanel @rendermode="wasmMode" />
    </section>
</div>

@code {
    private static readonly IComponentRenderMode wasmMode = new InteractiveWebAssemblyRenderMode(prerender: false);
}
```

In the current sample, the server panel carries its own `InteractiveServer` render boundary and the WASM panel is mounted as a sibling island.

## Important Rules

- choose runtime at the component boundary
- do not try to switch a child component to a different interactive mode than an already-interactive parent
- components using `InteractiveWebAssembly` must come from the client-side project graph
- mixed hosts may still need the core browser asset project in graph so `_content/DrawnUi.Blazor.Core/*` assets are served for the WASM-side DrawnUI runtime

## When To Use This Pattern

Use this pattern when:

- some DrawnUI surfaces are event-driven and acceptable as server-owned frame rendering
- other DrawnUI surfaces need local browser responsiveness
- you want gradual adoption instead of forcing the whole app into one rendering model

## Live Sample

Use the hosted sample to demonstrate the mixed server + browser runtime split:

- <a href="https://sample-blazor2.appomobi.com/" target="_blank" rel="noopener noreferrer">Open Blazor hybrid sample</a>

## Related Docs

- [Blazor](index.md)
- [Blazor WebAssembly](wasm.md)
- [Blazor Server](server.md)
- [Blazor FAQ](faq.md)