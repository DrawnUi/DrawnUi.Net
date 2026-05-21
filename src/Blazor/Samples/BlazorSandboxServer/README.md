# BlazorSandboxServer

This sample demonstrates the current Blazor Server path for DrawnUI.

- Routes:

	- `/drawn`: the DrawnUI sample page rendered through Blazor Server

- The host app runs as classic Blazor Server / `InteractiveServer`.
- DrawnUI content is rendered on the server through `DrawnUi.Blazor.Server`.
- The current implementation returns encoded PNG frames to the page.
- Standard Blazor Server events update the DrawnUI scene and regenerate the image.

## Folder purpose

This sample is split into a wrapper folder and the actual host app project.

- `BlazorSandboxServer/`
	- The sample root folder.
	- Holds this README and the local helper scripts.

- `BlazorSandboxServer/BlazorSandboxServer/`
	- The ASP.NET Core Blazor Server host app.
	- Owns startup, routing, static assets, and the `/drawn` page.
	- References `DrawnUi.Blazor.Server` and serves the rendered DrawnUI output.

## Local scripts

Run from this folder:

```powershell
.\run-local.ps1
```

Open the sample route in the browser:

```powershell
.\open-local.ps1
```

Open the HTTPS launch profile URL instead:

```powershell
.\open-local.ps1 -UseHttps
```

Stop the local sample started on the default ports:

```powershell
.\stop-local.ps1
```

Defaults:

- HTTP sample route: `http://localhost:53078/drawn`
- HTTPS sample route: `https://localhost:53077/drawn`

Optional parameters:

```powershell
.\run-local.ps1 -Configuration Release
.\run-local.ps1 -Environment Staging
.\run-local.ps1 -DryRun
.\stop-local.ps1 -Ports 53077,53078
```
