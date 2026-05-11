# DrawnUi.Blazor.Server

This package is the starting point for first-class Blazor Server support in DrawnUI.

Initial responsibility split:

- `DrawnUi.Blazor`: shared Razor-facing API and common component model
- `DrawnUi.Blazor.Wasm`: browser renderer and WebAssembly host integration
- `DrawnUi.Blazor.Server`: server renderer, frame transport, and input/event round-tripping

The first scaffold in this folder intentionally keeps the API small:

- `AddDrawnUiBlazorServer(...)` for DI registration
- `DrawnUiBlazorServerOptions` for transport and startup policy
- `ServerCanvas` for a minimal server-rendered DrawnUI surface
- `IDrawnUiServerRenderer` for headless DrawnUI-to-frame rendering
- `IDrawnUiServerFrameEncoder` and `PngDrawnUiServerFrameEncoder` for encoded frame output
- `DrawnUiServerFrame` as the transport payload contract

Planned next layer:

- server `Canvas` host/component
- DrawnUI scene/session lifecycle management
- poster-frame prerender support
- server-frame streaming and invalidation throttling
- input event dispatch between Blazor and the DrawnUI scene