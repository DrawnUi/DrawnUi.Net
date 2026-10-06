# OpenTK Samples

These samples show the main ways to host DrawnUI in an OpenTK application on Windows and Linux.

Use them as executable reference projects when you want to see how the OpenTK host is wired in a real app.

## HelloOpenTk

Path: `src/OpenTk/Samples/HelloOpenTk/`

`HelloOpenTk` is the DrawnUI Hello app on OpenTK, for Windows and Linux.

- Uses `DrawnUiWindow`
- Uses `UpdateMode = Dynamic`
- Hosts 20 sample pages in a `SkiaShell`: cells, images, shapes, text, layouts, animations, editor, keyboard, accessibility and more
- `dev/hello-opentk-linux.ps1` builds it for Linux on Windows and runs it in WSL

This is the sample to open when you want to see most DrawnUI controls running on OpenTK.

## OpenTkPong

Path: `src/OpenTk/Samples/OpenTkPong/`

`OpenTkPong` is the clearest example of a fully drawn game window.

- Uses `DrawnUiWindow`
- Uses `UpdateMode = Constant`
- Renders the whole game through DrawnUI instead of mixing a separate 3D scene underneath
- Uses a `RescalingCanvas` (from the shared Pong code) so the playable area keeps the same proportions while the desktop window is resized

This is the sample to open first if you want to understand the simplest game-style DrawnUI host on OpenTK.

## OpenTkGpuHost

Path: `src/OpenTk/Samples/OpenTkGpuHost/`

`OpenTkGpuHost` shows the event-driven desktop app path rather than the always-redrawing game path.

- Uses `DrawnUiWindow`
- Uses `UpdateMode = Dynamic`
- Sleeps between frames and wakes only when the canvas becomes dirty
- Demonstrates the lower-power, UI-application style of hosting DrawnUI in OpenTK

This is the best sample for editors, tools, launchers, and other desktop UI surfaces that do not need constant frame updates.

## OpenTkOverlay

Path: `src/OpenTk/Samples/OpenTkOverlay/`

`OpenTkOverlay` demonstrates the mixed-host case: raw OpenGL scene first, DrawnUI overlay second.

- Uses `CanvasHost` instead of `DrawnUiWindow` as the primary app base
- Renders a rotating 3D cube and lit environment with raw OpenGL
- Composites a semi-transparent DrawnUI control panel on top of the existing framebuffer
- Demonstrates the required GL-state restore block before raw GL drawing and `ResetGrContext()` before DrawnUI rendering

The overlay panel is useful because it shows several real integration points at once:

- `SkiaBackdrop` and `GlassBackdropEffect` for a live glass-like panel over 3D content
- labels updated from the render loop while staying friendly to layout performance
- `SkiaEditor` input and keyboard routing inside the overlay
- gesture and focus routing in a layered host
- `AcceleratedRetained` canvas mode to preserve the underlying GL framebuffer

This is the sample to study when you already have an OpenGL scene and want DrawnUI as a HUD, overlay, tools panel, or in-game desktop UI.

## Which Sample To Start With

Choose the sample that matches your host model:

- Start with `HelloOpenTk` for a tour of the controls in a shell app
- Start with `OpenTkPong` if you want a fully drawn app or game
- Start with `OpenTkGpuHost` if you want a desktop UI app with event-driven rendering
- Start with `OpenTkOverlay` if you already own the OpenGL render loop and want DrawnUI on top

- **Snippets**: more DrawnUI snippets you can run and edit right in the browser at [drawfiddle.com](https://drawfiddle.com).

## Related

- [DrawnUI for OpenTK](index.md)
- [Window Patterns](window.md)
- [Input and Window Features](gestures.md)
- [OpenTK FAQ](faq.md)