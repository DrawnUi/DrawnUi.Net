# DrawnUI for OpenTK

Run DrawnUI canvases inside an OpenTK `GameWindow` on Windows and Linux.  
Two integration paths are available depending on whether you own the window or not.

---

## Way 1 — Fully Drawn App (`DrawnUiGameWindow`)

Use this when the entire window content is DrawnUI.  
Subclass `DrawnUiGameWindow`, pass a configured `Canvas`, done.

Render behavior is controlled by `Canvas.UpdateMode`:

| `UpdateMode` | VSync | Render trigger | Use for |
|---|---|---|---|
| `Constant` | On | Every VSync frame, unconditional | Games, constant animation |
| `Dynamic` | Off | Dirty canvas only; sleeps via `GLFW.WaitEventsTimeout` | UI apps, editors |

```csharp
var gameSettings = new GameWindowSettings { UpdateFrequency = 0 };
var nativeSettings = new NativeWindowSettings
{
    ClientSize = new Vector2i(1280, 720),
    Title = "My App",
    API = ContextAPI.OpenGL,
    APIVersion = new Version(3, 3),
    Profile = ContextProfile.Core
};

var canvas = new Canvas
{
    BackgroundColor = Color.FromArgb("#1A1A2E"),
    RenderingMode = RenderingModeType.Accelerated,
    UpdateMode = UpdateModeType.Dynamic,   // or Constant for games
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
    Content = new List<SkiaControl>
    {
        new SkiaLabel { Text = "Hello DrawnUI!", TextColor = Colors.White }
    }
};

using var window = new DrawnUiGameWindow(gameSettings, nativeSettings, canvas);
window.Run();
```

`DrawnUiGameWindow` handles:
- Skia GPU surface lifecycle  
- Mouse and keyboard routing to the canvas  
- VSync and event-driven sleep — configured automatically from `Canvas.UpdateMode`  
- `Dynamic` mode: DrawnUI animation ticker wakes GLFW via `GLFW.PostEmptyEvent`  
- Display refresh rate query → `Super.MaxFps` set automatically  
- Relative frame timer (correct game/animation timing from first frame)

---

## Way 2 — Overlay Canvas in Existing App (`CanvasHost`)

Use this when you already have a `GameWindow` subclass (e.g., a 3D engine scene).  
`CanvasHost` owns the Skia surface lifecycle and exposes `Gestures` and `Input` sub-objects for routing.

```csharp
class MyGameWindow : GameWindow
{
    private readonly Canvas _canvas;
    private CanvasHost? _host;

    public MyGameWindow(GameWindowSettings gs, NativeWindowSettings ns)
        : base(gs, ns)
    {
        _canvas = new Canvas
        {
            BackgroundColor = Colors.Transparent,  // overlay over your 3D scene
            RenderingMode = RenderingModeType.AcceleratedRetained,  // skips canvas.Clear() so GL content underneath shows through
            UpdateMode = UpdateModeType.Dynamic,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new List<SkiaControl> { /* your UI */ }
        };
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        VSync = VSyncMode.On;

        _host = new CanvasHost(_canvas);

        // For dynamic/event-driven apps: pass GLFW.PostEmptyEvent to wake the loop
        // For game loop (constant render with VSync): pass nothing
        _host.Initialize(wakeLoop: GLFW.PostEmptyEvent);  // or _host.Initialize()

        _host.Resize(ClientSize.X, ClientSize.Y);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, e.Width, e.Height);
        _host?.Resize(e.Width, e.Height);
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);
        // Draw your own 3D scene here first, then overlay DrawnUI:
        _host?.Render();
        SwapBuffers();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        _host?.Gestures.OnMouseDown(e, MousePosition, ClientSize);
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        _host?.Gestures.OnMouseMove(e, MousePosition, MouseState.IsButtonDown(MouseButton.Left), ClientSize);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        _host?.Gestures.OnMouseUp(e, MousePosition, ClientSize);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        _host?.Input.OnTextInput(e);
    }

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        _host?.Input.OnKeyDown(e, KeyboardState);
        // Also route to game controls if needed:
        if (OpenTkKeyMapper.Map(e.Key) is { } key)
            KeyboardManager.KeyboardPressed(key);
    }

    protected override void OnKeyUp(KeyboardKeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (OpenTkKeyMapper.Map(e.Key) is { } key)
            KeyboardManager.KeyboardReleased(key);
    }

    protected override void OnUnload()
    {
        _host?.Dispose();
        base.OnUnload();
    }
}
```

`CanvasHost`:
- Queries the primary monitor refresh rate and sets `Super.MaxFps` automatically  
- Uses a relative timer so `DrawnGame`/animation delta starts near zero on first frame  
- `Gestures` — routes mouse down/move/up to DrawnUI hit testing  
- `Input` — routes text input and editor keyboard shortcuts (backspace, arrows, home/end, ctrl+A)  
- `wakeLoop` parameter — pass `GLFW.PostEmptyEvent` when using `WaitEvents` render mode  
  (dynamic/idle apps); omit or pass `null` when VSync drives the loop  

---

## Game Timing

Frame time is measured from `Initialize()` / `OnLoad()`, not from system boot.  
This matches MAUI and Blazor behavior — `DrawnGame.LastFrameTimeNanos` starts at `0`  
and the first delta is a few milliseconds, not hours of system uptime.

---

## FPS Cap

Both hosts query `GLFW.GetVideoMode` on the primary monitor and set `Super.MaxFps`  
to the real display refresh rate (fallback: 60).  
`DrawnUiGameWindow` sets VSync automatically based on `UpdateMode` — no manual configuration needed.

---

## Sample

`OpenTkPong` in `src/OpenTk/Samples/OpenTkPong/` demonstrates Way 1 with `UpdateMode = Constant`:  
a fully drawn Pong game inside `DrawnUiGameWindow`, with an `AspectLayer`  
that scales the game viewport uniformly as the window resizes.

`OpenTkGpuHost` in `src/OpenTk/Samples/OpenTkGpuHost/` demonstrates Way 1 with `UpdateMode = Dynamic`:  
an event-driven UI app that sleeps between frames and wakes only on canvas invalidation.

`OpenTkOverlay` in `src/OpenTk/Samples/OpenTkOverlay/` demonstrates Way 2 with `CanvasHost`:  
a rotating colored 3D cube rendered with raw OpenGL, with a DrawnUI dialog panel overlaid on top.
Gestures, text input, and live angle display are all routed through the standalone host.
