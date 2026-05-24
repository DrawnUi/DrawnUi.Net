# OpenTK Window Patterns

Two integration patterns depending on whether you own the render loop.

---

## 1. Fully Drawn App (`DrawnUiWindow`)

Subclass `DrawnUiWindow` and pass a configured `Canvas`. The window handles the Skia GPU surface, mouse/keyboard routing, VSync, and event-driven sleep automatically.

```csharp
var gameSettings = new GameWindowSettings { };
var nativeSettings = new NativeWindowSettings
{
    ClientSize = new Vector2i(1280, 720),
    Title = "My App",
    API = ContextAPI.OpenGL,
    APIVersion = new Version(4, 6),
    Profile = ContextProfile.Core,
};

var canvas = new Canvas
{
    BackgroundColor = Color.FromArgb("#1A1A2E"),
    RenderingMode = RenderingModeType.Accelerated,
    UpdateMode = UpdateModeType.Constant,   // or Dynamic for UI apps
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
    Content = new List<SkiaControl>
    {
        new SkiaLabel { Text = "Hello DrawnUI!", TextColor = Colors.White }
    }
};

using var window = new DrawnUiWindow(gameSettings, nativeSettings, canvas);
window.Run();
```

### UpdateMode

| `UpdateMode` | VSync | Render trigger | Use for |
|---|---|---|---|
| `Constant` | On | Every VSync frame, unconditional | Games, constant animation |
| `Dynamic` | Off | Dirty canvas only; sleeps via `GLFW.WaitEventsTimeout` | UI apps, editors |

### Mixing raw GL with DrawnUI (`RenderScene`)

Override `RenderScene()` to draw a 3D scene behind the DrawnUI canvas. The base class handles GL state restore, `GL.Clear`, `GRContext.ResetContext()`, and compositing automatically:

```csharp
class MyWindow(GameWindowSettings gs, NativeWindowSettings ns, Canvas canvas)
    : DrawnUiWindow(gs, ns, canvas)
{
    protected override void RenderScene()
    {
        GL.Enable(EnableCap.DepthTest);
        // ... your draw calls ...
        GL.Finish();
    }
}
```

---

## 2. Use DrawnUI UIs In Your Existing App (`CanvasHost`)

Use when your own `GameWindow` subclass owns the render loop and DrawnUI is composited on top as a transparent overlay, to create rich UIs with ease, dialogs, etc.

**Render order per frame:**

1. Restore GL state Skia left dirty from the previous frame
2. Render your 3D scene with raw OpenGL
3. Call `host.ResetGrContext()` — re-syncs Skia after direct GL calls
4. Call `host.Render()` — Skia composites the UI overlay on the existing framebuffer
5. `SwapBuffers()`

```csharp
class MyExistingAppWindow : GameWindow
{
    private CanvasHost? _host;

    public MyExistingAppWindow(GameWindowSettings gs, NativeWindowSettings ns)
        : base(gs, ns) { }

    protected override void OnLoad()
    {
        base.OnLoad();
        Super.Init();   // required when not using DrawnUiWindow as base
        VSync = VSyncMode.On;

        _host = new CanvasHost(new Canvas
        {
            BackgroundColor = Colors.Transparent,
            RenderingMode = RenderingModeType.AcceleratedRetained,  // preserve GL content
            UpdateMode = UpdateModeType.Constant,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new List<SkiaControl>
            {
                new SkiaLayer
                {
                    VerticalOptions = LayoutOptions.Fill,
                    Children = { /* your drawn UI controls */ }
                }
            }
        });

        _host.Initialize();
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

        // ── 1. Restore GL state Skia left dirty ────────────────────────────
        // Skia sets its own viewport, enables stencil test, and disables depth
        // writes. Without this restore, 3D geometry maps off-screen or fails
        // the stencil/depth test and becomes invisible.
        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        GL.Disable(EnableCap.StencilTest);
        GL.DepthMask(true);
        GL.ColorMask(true, true, true, true);

        // ── 2. Render your 3D scene ─────────────────────────────────────────
        GL.ClearColor(r, g, b, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        GL.Enable(EnableCap.DepthTest);
        DrawScene();
        GL.Finish();

        // ── 3. Composite DrawnUI overlay ────────────────────────────────────
        _host!.ResetGrContext();   // tell Skia its cached GL state is stale
        _host!.Render();

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
        if (e.Key == Keys.Escape) Close();
        _host?.Input.OnKeyDown(e, KeyboardState);
    }

    protected override void OnUnload()
    {
        _host?.Dispose();
        base.OnUnload();
    }
}
```

### Why `AcceleratedRetained`?

`AcceleratedRetained` skips Skia's `canvas.Clear()` before each redraw, so the GL framebuffer content (your 3D scene) is preserved when Skia composites the overlay on top. `GL.Clear` runs every frame in your own code before `DrawScene()` — that resets the framebuffer without involving Skia, so there is no alpha accumulation.

### GL state after Skia

Skia does not restore GL state after rendering. The four states it leaves dirty and why they matter:

| State | What Skia does | Effect if not restored |
|---|---|---|
| `GL_VIEWPORT` | Sets its own internal viewport | Vertices produce correct NDC but map off-screen — geometry invisible |
| `GL_STENCIL_TEST` | Enables it with its own ref/func | Stencil value is 0 everywhere after `GL.Clear`; all fragments fail → nothing draws |
| `glDepthMask` | Sets to `false` | `GL.Clear(DepthBufferBit)` has no effect; old depth corrupts depth test |
| Color mask | May be partial | Some channels may not write |

The restore block and `ResetGrContext()` are a two-way contract: restore what Skia left dirty before your GL draw, then tell Skia its cached state is stale before it draws.

---

## Related

- [OpenTK Guide](index.md)
- [Input and Window Features](gestures.md)
- [OpenTK FAQ](faq.md)
