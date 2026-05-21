# BlazorSandboxHybrid

This sample demonstrates a mixed Blazor Web App that hosts both DrawnUI server rendering and DrawnUI WebAssembly rendering in the same app.

Routes:

- `/hybrid`: a single page hosting sibling DrawnUI panels with different render modes

The server host uses `DrawnUi.Blazor.Server`, while the `.Client` project uses `DrawnUi.Blazor.Wasm`.

## Folder purpose

This sample is split into three folders because each runtime belongs to a different Blazor component graph.

- `BlazorSandboxHybrid/`
	- The ASP.NET Core host app.
	- Owns the app startup, routing, static assets, and the `/hybrid` page.
	- References both the server-only component project and the WASM client project.

- `BlazorSandboxHybrid.Client/`
	- The WebAssembly client app.
	- Contains the browser-side DrawnUI panel and runs through `InteractiveWebAssembly`.
	- This is where `WasmDrawnPanel` lives.

- `BlazorSandboxHybrid.ServerComponents/`
	- A Razor class library for server-only UI components.
	- Keeps the `InteractiveServer` DrawnUI panel isolated from the WASM client graph.
	- This is where `ServerDrawnPanel` lives and where `DrawnUi.Blazor.Server` is referenced.

In other words, the host page composes two sibling components:

- one server-rendered panel from `BlazorSandboxHybrid.ServerComponents`
- one WASM-rendered panel from `BlazorSandboxHybrid.Client`

## Local scripts

Run from this folder:

```powershell
.\run-local.ps1
```

Open the sample route in the browser:

```powershell
.\open-local.ps1
```

Stop the local sample started on the default port:

```powershell
.\stop-local.ps1
```

Defaults:

- host URL: `http://localhost:5177`
- sample route: `http://localhost:5177/hybrid`

Optional parameters:

```powershell
.\run-local.ps1 -Configuration Release
.\run-local.ps1 -Environment Development -Url http://localhost:5180
.\open-local.ps1 -Url http://localhost:5180
.\stop-local.ps1 -Port 5180
```
