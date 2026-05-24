# Blazor

Use this section when DrawnUI is running inside a Blazor app.

DrawnUI for Blazor currently supports two runtime models:

- `DrawnUi.Blazor.Wasm` for browser-side rendering with `Canvas`
- `DrawnUi.Blazor.Server` for server-rendered DrawnUI surfaces that also use `Canvas`

`DrawnUi.Blazor.Core` is shared implementation infrastructure. It is not the normal package you install directly.

## Runtime summary

| Hosting Model | Primary package | Rendering happens | Best fit | Demo |
| --- | --- | --- | --- | --- |
| Blazor WebAssembly | `DrawnUi.Blazor.Wasm` | In the browser inside the `Canvas` component | Animation-heavy, gesture-heavy, or fully drawn pages | <a href="https://drawnui.net/sandbox/" target="_blank" rel="noopener noreferrer">Open Demo</a> |
| Blazor Server | `DrawnUi.Blazor.Server` | On the server, delivered as image frames through the `Canvas` component | Event-driven widgets, dashboards, mixed Razor + DrawnUI pages |<a href="https://sample-blazor1.appomobi.com/" target="_blank" rel="noopener noreferrer">Open Demo</a> |
| Blazor Hybrid  | `DrawnUi.Blazor.Server` in host, `DrawnUi.Blazor.Wasm` in `.Client` | Different per component | Apps that need one DrawnUI surface to stay local and another to stay server-owned | <a href="https://sample-blazor2.appomobi.com/" target="_blank" rel="noopener noreferrer">Open Demo</a> |

## Start here

- [Blazor Packages](packages.md)
- [Blazor Samples](samples.md)
- [Blazor WebAssembly](wasm.md)
- [Blazor Server](server.md)
- [Blazor Hybrid Web App](hybrid.md)
- [Blazor Capabilities](capabilities.md)
- [Blazor Migration](migration.md)
- [Blazor FAQ](faq.md)
- [Handling Gestures](../gestures.md)