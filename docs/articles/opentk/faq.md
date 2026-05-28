# OpenTK FAQ

Common problems and fixes when using DrawnUI with OpenTK.

---

## White flash appears before the first frame

The window briefly shows white before content appears.

**Cause:** Window becomes visible before the first GL frame is rendered.

**Fix for `DrawnUiWindow`:** Handled automatically — the base class sets `StartVisible = false` internally and sets `IsVisible = true` after the first `SwapBuffers()`.

**Fix for `CanvasHost` / custom `GameWindow`:** Pass `StartVisible = false` in `NativeWindowSettings`, track a `_firstFrameDone` bool, and reveal the window after the first `SwapBuffers()`:

```csharp
private bool _firstFrameDone;

// In NativeWindowSettings:
StartVisible = false,

// In OnRenderFrame, after SwapBuffers():
SwapBuffers();
if (!_firstFrameDone)
{
    _firstFrameDone = true;
    IsVisible = true;
}
```

---

## 3D scene is invisible — geometry doesn't render

Scene is black or geometry is missing despite correct draw calls.

**Cause:** Skia leaves GL state dirty after rendering. Without restoring it, 3D draw calls fail silently.

| State | What Skia does | Effect if not restored |
|---|---|---|
| `GL_VIEWPORT` | Sets its own viewport | Vertices map off-screen — invisible |
| `GL_STENCIL_TEST` | Enables with its own ref/func | All fragments fail stencil → nothing draws |
| `glDepthMask` | Sets to `false` | Depth clear has no effect; depth test corrupts |
| Color mask | May be partial | Some color channels don't write |

**Fix:** Restore all four states before every raw GL draw, and call `ResetGrContext()` before Skia renders:

```csharp
// Before your GL draw:
GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
GL.Disable(EnableCap.StencilTest);
GL.DepthMask(true);
GL.ColorMask(true, true, true, true);

// ... your scene ...
GL.Finish();

// Before DrawnUI renders:
_host.ResetGrContext();
_host.Render();
```

See [Window Patterns](window.md) for the full frame order.

---

## 3D scene disappears when the DrawnUI overlay renders

Background goes white or the GL scene is overwritten by Skia's clear.

**Cause:** `Canvas.RenderingMode` is not `AcceleratedRetained`, or `BackgroundColor` is opaque.

**Fix:**

```csharp
new Canvas
{
    BackgroundColor = Colors.Transparent,
    RenderingMode = RenderingModeType.AcceleratedRetained,
    ...
}
```

`AcceleratedRetained` skips Skia's internal `canvas.Clear()` so the GL framebuffer is preserved when the overlay composites on top. Your own `GL.Clear(...)` still runs every frame before your scene draw — that resets the buffer without involving Skia, so there is no alpha accumulation.

This mode is **required** for any overlay that uses `SkiaBackdrop` — without it, `SkiaBackdrop` has no framebuffer content to sample and appears blank.

---

## `SkiaEditor` loses focus on mouse release

Clicking inside `SkiaEditor` focuses it on mouse down, but focus is lost immediately when the mouse button is released.

**Cause:** `Canvas.ProcessNetGestures` runs two passes for the `Up` event. The editor correctly claims `Up` in the first pass (HadInput). In the second pass, the gesture walks the control tree; the editor returns `null` (already counted via `SignalInput` dedup) and the traversal falls through to the next sibling — typically `SkiaBackdrop`, which unconditionally returns `this` from `ProcessGestures`. The canvas sees `consumed (SkiaBackdrop) != FocusedChild (SkiaEditor)` and clears the editor's focus.

**Status:** Fixed in `Canvas.Gestures.Net.cs`, `Canvas.cs` (MAUI), and `Canvas.Gestures.Blazor.cs`. The `consumed != FocusedChild` focus-reassignment guard no longer fires on `Up` events — focus ownership is established on `Down` only.

**If you write a custom control** that sits behind an editor in z-order: do not return `this` unconditionally from `ProcessGestures`. Return `null` for `Up` when the control did not capture the gesture on `Down`.

---

## Mouse clicks or drags don't reach the DrawnUI overlay

Controls don't respond to mouse input.

**Cause:** Input events not forwarded to the canvas (`CanvasHost` only — `DrawnUiWindow` wires this automatically).

**Fix:** Override all four mouse events in your `GameWindow`:

```csharp
protected override void OnMouseDown(MouseButtonEventArgs e)
{
    base.OnMouseDown(e);
    if (e.Button == MouseButton.Left)
        _canvas.HandleDesktopPointerDown(MousePosition.X, MousePosition.Y, ClientSize.X, ClientSize.Y);
}

protected override void OnMouseMove(MouseMoveEventArgs e)
{
    base.OnMouseMove(e);
    _canvas.HandleDesktopPointerMove(MousePosition.X, MousePosition.Y,
        MouseState.IsButtonDown(MouseButton.Left), ClientSize.X, ClientSize.Y);
}

protected override void OnMouseUp(MouseButtonEventArgs e)
{
    base.OnMouseUp(e);
    if (e.Button == MouseButton.Left)
        _canvas.HandleDesktopPointerUp(MousePosition.X, MousePosition.Y, ClientSize.X, ClientSize.Y);
}

protected override void OnTextInput(TextInputEventArgs e)
{
    base.OnTextInput(e);
    _canvas.HandleDesktopTextInput(e.AsString);
}
```

---

## `SkiaEditor` doesn't type — keyboard input is lost

Clicking the editor focuses it but keys produce no text and navigation keys don't work.

**Cause:** Editor-key events not forwarded (`CanvasHost` only — `DrawnUiWindow` handles this automatically).

**Fix:** Override `OnKeyDown` and dispatch to canvas editor helpers:

```csharp
protected override void OnKeyDown(KeyboardKeyEventArgs e)
{
    base.OnKeyDown(e);
    var shift = KeyboardState.IsKeyDown(Keys.LeftShift) || KeyboardState.IsKeyDown(Keys.RightShift);
    var ctrl  = KeyboardState.IsKeyDown(Keys.LeftControl) || KeyboardState.IsKeyDown(Keys.RightControl);
    switch (e.Key)
    {
        case Keys.Backspace: _canvas.DesktopEditorBackspace(); break;
        case Keys.Delete:    _canvas.DesktopEditorDelete(); break;
        case Keys.Enter:     _canvas.DesktopEditorEnter(); break;
        case Keys.Left:      _canvas.DesktopEditorMoveCursor(-1, shift); break;
        case Keys.Right:     _canvas.DesktopEditorMoveCursor(1, shift); break;
        case Keys.Home:      _canvas.DesktopEditorMoveToStart(shift); break;
        case Keys.End:       _canvas.DesktopEditorMoveToEnd(shift); break;
        case Keys.A when ctrl: _canvas.DesktopEditorSelectAll(); break;
        case Keys.Tab:       _canvas.HandleDesktopTextInput("    "); break;
    }
}
```

`OnTextInput` must also be forwarded (see [Mouse clicks don't reach the overlay](#mouse-clicks-or-drags-dont-reach-the-drawnui-overlay) above).

---

## `SkiaBackdrop` / glass effect appears blank or white

`SkiaBackdrop` renders as a solid white or transparent rectangle instead of blurring the scene behind it.

**Cause:** `RenderingMode` is not `AcceleratedRetained`. Skia clears the canvas before drawing, so `SkiaBackdrop` has no framebuffer pixels to sample.

**Fix:** Set `RenderingMode = RenderingModeType.AcceleratedRetained` and `BackgroundColor = Colors.Transparent` on the `Canvas`. Both are required for backdrop effects in an overlay.

---

## Running on Linux / WSL2

### App crashes with `EGL: Failed to create context: Arguments are inconsistent`

**Cause:** GLFW's bundled `libglfw.so.3` uses EGL; Mesa D3D12 (WSLg) cannot create a desktop OpenGL Core context via EGL.

**Fix:** Replace the bundled GLFW with the system X11/GLX build:

```bash
sudo apt install libglfw3
cd /path/to/publish
mv libglfw.so.3 libglfw.so.3.bak
ln -s /usr/lib/x86_64-linux-gnu/libglfw.so.3 libglfw.so.3
```

---

### App crashes with `GLX: Failed to create context: GLXBadFBConfig`

**Cause:** Mesa D3D12 (WSLg) does not expose an OpenGL 4.6 framebuffer config via GLX. Requesting `APIVersion = new Version(4, 6)` fails.

**Fix:** Use OpenGL 3.3 on Linux — Mesa fully supports it:

```csharp
var nativeSettings = new NativeWindowSettings
{
    API = ContextAPI.OpenGL,
    APIVersion = OperatingSystem.IsLinux() ? new Version(3, 3) : new Version(4, 6),
    Profile = ContextProfile.Core,
    WindowState = WindowState.Normal,   // required on WSLg — see below
    ...
};
```

---

### App starts fullscreen on WSLg, then crashes with `D3D12: Removing Device`

**Cause:** WSLg presents large windows fullscreen by default. The fullscreen→windowed transition triggers a D3D12 device reset in Mesa, which causes a segfault.

**Fix:** Set `WindowState = WindowState.Normal` in `NativeWindowSettings`. This prevents the mode switch and keeps the D3D12 device stable.

---

### FPS is uncapped on Linux (400+ FPS in Constant mode)

**Cause:** Mesa/WSLg ignores the OpenGL swap interval — `VSync = VSyncMode.On` has no effect. `SwapBuffers()` returns immediately, so the render loop runs at CPU speed.

**Fix:** `DrawnUiWindow` automatically applies a software frame cap at the monitor refresh rate on Linux. No app-level change needed. This is handled inside the library.

If you use a custom `GameWindow` (not `DrawnUiWindow`), add this after `VSync = VSyncMode.On`:

```csharp
if (OperatingSystem.IsLinux())
    UpdateFrequency = targetFps; // e.g. 60
```

---

### WSL2 one-time setup for DrawnUI OpenTK apps

```bash
# Install required native libs
sudo apt install libglfw3 libopenal1 libgl1 libgles2-mesa unzip

# Verify GPU/GL works
glxinfo | grep "OpenGL renderer"

# Launch with X11 display
DISPLAY=:0 ./YourApp
```

WSLg is required (included in WSL 2.x). Audio works automatically via WSLg's built-in PulseAudio.

---

## Related

- [OpenTK Guide](index.md)
