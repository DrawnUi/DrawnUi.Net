# Blazor Capabilities

This page summarizes what the current Blazor slice does well, where each runtime fits, and which scenarios are not solved well yet.

## Runtime Fit

| Scenario | Best runtime today | Why |
| --- | --- | --- |
| Animation-heavy surface | `DrawnUi.Blazor.Wasm` | Rendering and interaction stay local |
| Gesture-heavy canvas UI | `DrawnUi.Blazor.Wasm` | Avoids server round-trips for visual response |
| Drawn widget inside a Blazor Server page | `DrawnUi.Blazor.Server` | Integrates naturally with existing server app flow |
| Event-driven dashboard or inspector | `DrawnUi.Blazor.Server` | Server-rendered frames are acceptable for lower-frequency updates |
| One app needs both models | Mixed Blazor Web App | Runtime can be chosen per component boundary |

## Strong Current Fit

- browser/WASM DrawnUI through `Canvas`
- server-rendered DrawnUI through `Canvas`
- normal Razor + DrawnUI on the same page
- same-app mixed server + WASM DrawnUI with sibling islands
- button and tap style interaction in the validated samples
- browser startup, font registration, and keyboard integration in the browser runtime

## Current Limits

### Browser/WASM runtime

The browser runtime is the stronger general-purpose option today, but parity with the MAUI target is still incomplete.

### Server runtime

The server runtime is intentionally more constrained:

- it is not a live browser canvas
- it is not the right fit for high-FPS animation
- it is not yet the right fit for drag-heavy interaction
- hover, wheel, pointer-move, focus, and text-input parity are not at browser/WASM level

## Support Boundary

Think about the server path as server-owned scene rendering with interaction callbacks, not as a direct substitute for a local canvas runtime.

Think about the browser/WASM path as the right target whenever the DrawnUI surface should feel local.

## Validated Samples

- `src/Blazor/Samples/BlazorSandbox/`
- `src/Blazor/Samples/BlazorSandboxServer/`
- `src/Blazor/Samples/BlazorSandboxHybrid/`

## Related Docs

- [Blazor](index.md)
- [Blazor Packages](packages.md)
- [Blazor Migration](migration.md)
- [Blazor FAQ](faq.md)