# OpenTkGpuHost

## GPU-backed desktop host for DrawnUi.Net

`OpenTkGpuHost` is a real desktop host for `DrawnUi.Net` built on:

- `OpenTK` for the native window and OpenGL context
- `SkiaSharp` GPU APIs for `GRContext` and `SKSurface`
- `DrawnUi.Net` for layout, rendering, and input routing

The host creates a native OpenGL window, wraps the current framebuffer as a Skia GPU render target, and lets DrawnUI render directly into that GPU-backed `SKSurface`.

## Why this sample matters

This sample proves that `DrawnUi.Net` is not limited to headless rendering or framework-specific hosts.

If you can provide:

- a native window
- a current GL context
- a framebuffer-backed `SKSurface`

then DrawnUI can run there.

That includes Windows and Linux with the same host pattern.

## Project packaging

The project now declares desktop publish targets for:

- `win-x64`
- `linux-x64`
- `linux-arm64`

It also includes `SkiaSharp.NativeAssets.Linux` explicitly so Linux publish output contains the native Skia runtime needed by the GPU host.

For example, a `linux-x64` publish now emits:

- `libSkiaSharp.so`
- `libHarfBuzzSharp.so`
- `libglfw.so.3`

## Run locally

From the repo root:

```powershell
dotnet run --project .\src\Net\Samples\OpenTkGpuHost\OpenTkGpuHost.csproj --configuration Debug
```

## Publish for Linux

Framework-dependent publish:

```powershell
dotnet publish .\src\Net\Samples\OpenTkGpuHost\OpenTkGpuHost.csproj --configuration Release -r linux-x64 --self-contained false
```

The publish output is written to:

```text
src/Net/Samples/OpenTkGpuHost/bin/Release/net10.0/linux-x64/publish/
```

On a Linux machine with the matching .NET runtime installed, run:

```bash
chmod +x ./OpenTkGpuHost
./OpenTkGpuHost
```

## Linux runtime expectations

The sample ships the managed assemblies plus the key native runtime payloads from NuGet, but the target Linux machine still needs a working desktop OpenGL stack and the OS windowing libraries required by GLFW/OpenGL.

In practice that means:

- a graphical desktop session
- working GPU or Mesa drivers
- the usual Linux OpenGL/window-system runtime libraries for your distro

## Host architecture

The important path in the sample is:

1. `GameWindow` creates the native window and GL context.
2. `GRContext.CreateGl(...)` binds Skia to that GL context.
3. `GRBackendRenderTarget` wraps the active framebuffer.
4. `SKSurface.Create(...)` creates the GPU surface.
5. `Canvas.RenderExternalSurface(...)` renders DrawnUI into that surface.

That is the reusable pattern for bringing `DrawnUi.Net` to other native desktop hosts.