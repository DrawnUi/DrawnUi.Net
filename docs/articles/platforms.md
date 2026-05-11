# Platforms and Packages

DrawnUI is now an umbrella for multiple .NET targets.

Use this page first when you need to decide which package to install and which runtime model fits your use case.

## Choose by host

| Target | Main package | Use it when | Best fit |
| --- | --- | --- | --- |
| .NET MAUI | `DrawnUi.Maui` | You are building a native app for iOS, Android, MacCatalyst, or Windows | App UI, gestures, animation-heavy native experiences |
| Blazor WebAssembly | `DrawnUi.Blazor.Wasm` | DrawnUI should render locally in the browser | Canvas-like browser UI, high-fps, local responsiveness, animation-heavy, web surfaces |
| Blazor Server | `DrawnUi.Blazor.Server` | DrawnUI should be hosted in a Blazor Server or `InteractiveServer` app | Event-driven widgets, low-fps, dashboards, mixed Razor + DrawnUI pages |
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
- [Blazor Packages](blazor-packages.md)
- [Blazor FAQ](blazor-faq.md)

## DrawnUi.Net

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

## More targets coming soon

The DrawnUI umbrella is expanding. The current docs cover MAUI, Blazor, and `DrawnUi.Net` clearly first, while future platform targets can slot into the same package-and-host model.
