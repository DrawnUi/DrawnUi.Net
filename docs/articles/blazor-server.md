# Blazor Server

Use `DrawnUi.Blazor.Server` when DrawnUI should live naturally inside a Blazor Server or `InteractiveServer` app and the target surface is event-driven rather than continuously animated.

## Mental Model

The server-backed `Canvas` is not a live browser canvas.

It renders a DrawnUI control tree on the server, returns encoded image frames to the page, and routes supported interactions back to the server.

This makes it a good fit for low-animation, event-driven UI, but not for high-FPS or drag-heavy rendering.

## When To Choose It

Choose the server runtime when you need:

- DrawnUI widgets inside a conventional Blazor Server app
- mixed Razor + DrawnUI pages
- server-owned rendering for lower-frequency updates
- dashboards, inspectors, control panels, and event-driven surfaces

Avoid it when you need browser-canvas parity, heavy pointer streaming, or high-FPS animation.

## Install

```bash
dotnet add package DrawnUi.Blazor.Server
```

## Host Setup

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

`builder.WebHost.UseStaticWebAssets();` is recommended for local development so package static assets are served correctly.

## Minimal Page

```razor
@page "/drawn"
@rendermode InteractiveServer

<Canvas Content="@BuildCanvasContent()"
    WidthRequest="400"
    HeightRequest="240"
    Alt="DrawnUI server-rendered sample" />
```

`DrawnUi.Blazor.Server` exposes its `Canvas` component from `DrawnUi.Blazor.Server.Views`. In a pure server app you import that runtime package and use `Canvas` directly. No extra host-selection property is required.

## Scene Builder Example

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

Build a fresh `SkiaControl` tree for each page render or server redraw. Reusing disposed headless-host control instances is not reliable.

## Supported Slice Today

Validated today:

- server rendering of DrawnUI trees through `Canvas`
- standard Blazor Server state updates reflected in the DrawnUI scene
- clickable button-style interactions in the current samples

Not yet a strong fit for:

- high-FPS animation
- drag-heavy interaction
- hover / pointer-move parity
- wheel parity
- browser-like text-input parity

## Best Reference

The best current reference is:

- `src/Blazor/Samples/BlazorSandboxServer/`

## Live Sample

Use the hosted sample to demonstrate the current server-rendered DrawnUI stack:

- <a href="https://sample-blazor1.appomobi.com/" target="_blank" rel="noopener noreferrer">Open Blazor Server sample</a>

## Related Docs

- [Blazor](blazor.md)
- [Blazor FAQ](blazor-faq.md)
- [Blazor Hybrid Web App](blazor-hybrid.md)
