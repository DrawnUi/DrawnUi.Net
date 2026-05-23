# OpenTkGpuHost

## GPU-backed desktop host for DrawnUi.Net

Sample app that demonstrates `DrawnUi.OpenTk` — the OpenTK host library for DrawnUI.

The sample creates a `Canvas` with a demo scene and passes it to `DrawnUiGameWindow`, which manages the native window, OpenGL context, GPU surface, and input routing.

## How it works

```
OpenTK GameWindow → OpenGL framebuffer → GRContext → SKSurface → Canvas.RenderExternalSurface
```

## Run locally

```powershell
dotnet run --project .\src\OpenTk\Samples\OpenTkGpuHost\OpenTkGpuHost.csproj --configuration Debug
```

## Publish for Linux

```powershell
dotnet publish .\src\OpenTk\Samples\OpenTkGpuHost\OpenTkGpuHost.csproj --configuration Release -r linux-x64 --self-contained false
```

Output: `bin/Release/net10.0/linux-x64/publish/`

On Linux with the matching .NET runtime:

```bash
chmod +x ./OpenTkGpuHost
./OpenTkGpuHost
```

## Linux runtime requirements

- graphical desktop session
- working GPU or Mesa drivers
- OS OpenGL/window-system runtime libraries for your distro

## See also

Library: `src/OpenTk/DrawnUi/DrawnUi.OpenTk.csproj`
