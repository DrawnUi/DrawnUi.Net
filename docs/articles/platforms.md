# Platforms and Packages

DrawnUI is an umbrella for multiple .NET targets.

Use this page first when you need to decide which package to install and which runtime model fits your use case.

## Choose by host

| Target | Main package | Use it when | Best fit |
| --- | --- | --- | --- |
| MAUI (Android/iOS/MacCatalyst/Windows) | `DrawnUi.Maui` | You are building a native cross-platform app | App UI, gestures, animation-heavy native experiences |
| Blazor WebAssembly | `DrawnUi.Blazor.Wasm` | DrawnUI should render locally in the browser | Canvas-like browser UI, high-fps, local responsiveness, animation-heavy, web surfaces |
| Blazor Server | `DrawnUi.Blazor.Server` | DrawnUI should be hosted in a Blazor Server or `InteractiveServer` app | Event-driven widgets, low-fps, dashboards, mixed Razor + DrawnUI pages |
| OpenTK (Windows/Linux) | `DrawnUi.OpenTk.Game` | You need fast and small-sized desktop app/game | create from scratch or overlay drawn layouts on top of your OpenGL window |
| Platform-agnostic .NET | `DrawnUi.Net` | You need DrawnUI without a framework-specific UI host | Headless rendering, console app, server-side, image/PDF generation, harnesses, shared-logic debugging |

## .NET MAUI

Install:

```bash
dotnet add package DrawnUi.Maui
```

Use `DrawnUi.Maui` when you want a native application host with DrawnUI owning part or all of the visible UI.

Choose it for:

- mobile and desktop native apps
- rich gesture-driven UI
- animation-heavy app surfaces
- pixel-perfect custom app UI on top of MAUI app structure

Start here:

- [.NET MAUI](maui/index.md)
- [Installation and Setup](maui/getting-started.md)
- [Startup Settings](startup-settings.md)

## Blazor

Install the runtime package that matches where rendering should happen:

```bash
dotnet add package DrawnUi.Blazor.Wasm
dotnet add package DrawnUi.Blazor.Server
```

Choose `DrawnUi.Blazor.Wasm` when DrawnUI should stay local in the browser.

Choose `DrawnUi.Blazor.Server` when DrawnUI should live inside a Blazor Server app and server-rendered frames are acceptable.

Start here:

- [Blazor](blazor/index.md)
- [Blazor Packages](blazor/packages.md)
- [Blazor FAQ](blazor/faq.md)

## OpenTK (Windows / Linux)

> **Project reference** — `DrawnUi.OpenTk.Game` is currently consumed as a project reference, not a NuGet package. Add it as shown:

```xml
<ItemGroup>
  <ProjectReference Include="path/to/DrawnUi.OpenTk.Game/DrawnUi.OpenTk.Game.csproj" />
</ItemGroup>
```

Choose OpenTK when you need a native OpenGL window on Windows or Linux without a MAUI or Blazor host.

Use it for:

- 2D and 3D games with a GPU-accelerated DrawnUI canvas overlay
- standalone desktop tools that need raw OpenGL access
- embedding DrawnUI UI into an existing OpenTK engine scene

Two integration paths:

- **`DrawnUiGameWindow`** — subclass this when the entire window is DrawnUI content
- **`CanvasHost`** — use this when your own `GameWindow` subclass owns rendering and you want DrawnUI as an overlay

Start here:

- [DrawnUI for OpenTK](opentk/index.md)

## Pure .NET

Install:

```bash
dotnet add package DrawnUi.Net
```

Choose `DrawnUi.Net` when you need the DrawnUI rendering and layout model without a framework-specific app host.

Use it for:

- headless rendering
- server-side image or PDF generation
- offscreen validation of shared layout and drawing behavior
- control harnesses and repro tools
- debugging shared rendering logic before checking the same scenario on MAUI or Blazor

Start here:

- [DrawnUi.Net](net/index.md)

## More targets coming

The DrawnUI umbrella is expanding and your PRs are welcome. Current docs cover MAUI, Blazor, DrawnUi.Net, and OpenTK, while future platform targets can slot into the same package-and-host model.
