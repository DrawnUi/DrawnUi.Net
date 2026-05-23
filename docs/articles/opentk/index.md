# DrawnUI for OpenTK

Run DrawnUI canvases inside an OpenTK `GameWindow` on Windows and Linux.

## When to use

| Use case | Recommendation |
|---|---|
| Entire window is DrawnUI | `DrawnUiGameWindow` (Way 1) |
| DrawnUI overlay on your own 3D/game window | `CanvasHost` (Way 2) |

---

## Install

`DrawnUi.OpenTk.Game` is currently distributed as a project reference:

```xml
<ItemGroup>
  <ProjectReference Include="path/to/DrawnUi.OpenTk.Game/DrawnUi.OpenTk.Game.csproj" />
</ItemGroup>
```

---

## Initialization

Call `Super.UseDrawnUi().Build()` once before creating windows or canvases:

```csharp
Super.UseDrawnUi()
    .ConfigureFonts(fonts =>
    {
        fonts.AddFont("fonts/Orbitron-Regular.ttf",  "FontGame",  FontWeight.Regular);
        fonts.AddFont("fonts/NotoColorEmoji-Regular.ttf", "FontEmoji");
    })
    .Build();
```

Font files must be present next to the executable at runtime. Mark them as content with `CopyToOutputDirectory`:

```xml
<ItemGroup>
  <Content Include="fonts\**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

---

## Way 1 — Fully Drawn App (`DrawnUiGameWindow`)

Subclass `DrawnUiGameWindow` and pass a configured `Canvas`. The window handles the Skia GPU surface, mouse/keyboard routing, VSync, and event-driven sleep automatically.

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

using var window = new DrawnUiGameWindow(gameSettings, nativeSettings, canvas);
window.Run();
```

### UpdateMode

| `UpdateMode` | VSync | Render trigger | Use for |
|---|---|---|---|
| `Constant` | On | Every VSync frame, unconditional | Games, constant animation |
| `Dynamic` | Off | Dirty canvas only; sleeps via `GLFW.WaitEventsTimeout` | UI apps, editors |

---

## Way 2 — Overlay Canvas (`CanvasHost`)

Use when your own `GameWindow` subclass owns rendering and DrawnUI is an overlay.

The key insight is to render your 3D scene **inside** the Skia pass rather than before it. A thin `SkiaControl` subclass acts as the GL background layer — it is added as the first child of the canvas so it renders behind the UI. This lets the canvas use `Accelerated` mode (Skia clears the framebuffer fresh each frame), which prevents alpha accumulation when UI interactions trigger extra redraws.

**Step 1 — GL background control**

```csharp
internal sealed class GlSceneView : SkiaControl
{
    private readonly Action<DrawingContext> _renderGl;

    public GlSceneView(Action<DrawingContext> renderGl)
    {
        _renderGl = renderGl;
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
    }

    protected override void Paint(DrawingContext ctx) => _renderGl(ctx);
}
```

**Step 2 — GameWindow**

```csharp
class MyGameWindow : GameWindow
{
    private CanvasHost? _host;

    protected override void OnLoad()
    {
        base.OnLoad();
        VSync = VSyncMode.On;

        _host = new CanvasHost(new Canvas
        {
            BackgroundColor = Colors.Transparent,
            RenderingMode = RenderingModeType.Accelerated,   // canvas.Clear() each frame — no alpha accumulation
            UpdateMode = UpdateModeType.Constant,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new List<SkiaControl>
            {
                new SkiaLayer
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new GlSceneView(RenderSceneInSkiaPass),  // GL background
                        /* your UI controls here */
                    }
                }
            }
        });

        _host.Initialize(); // no wakeLoop — VSync drives the loop
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
        _host!.Render();   // GL scene + UI composited in one pass
        SwapBuffers();
    }

    // Called by GlSceneView.Paint — runs inside Skia's render pass.
    private void RenderSceneInSkiaPass(DrawingContext ctx)
    {
        // Flush pending Skia commands (e.g. canvas.Clear) to GPU before touching GL.
        ctx.Context.Canvas.Flush();

        // Restore GL state that Skia leaves dirty.
        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        GL.Disable(EnableCap.StencilTest);
        GL.DepthMask(true);
        GL.ColorMask(true, true, true, true);

        // Draw your 3D scene (overwrites the transparent clear above).
        GL.ClearColor(/* your background color */);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        GL.Enable(EnableCap.DepthTest);
        DrawScene();
        GL.Finish();

        // Skia left GL state dirty; tell it to re-sync before drawing the next sibling.
        (ctx.Context.Surface?.Context as GRContext)?.ResetContext();
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

> **Why `Accelerated` and not `AcceleratedRetained`?**  
> `AcceleratedRetained` skips `canvas.Clear()`, which would leave the GL framebuffer intact — but when UI interactions trigger extra Skia redraws within the same GL frame, semi-transparent panels composite on top of themselves and become opaque. `Accelerated` clears fresh each Skia redraw; the GL scene redraws too (inside `GlSceneView.Paint`) so both are always in sync.

---

## Game Input

For game controls driven by `KeyboardManager` (e.g., `DrawnGame` movement), route key events through `OpenTkKeyMapper`:

```csharp
protected override void OnKeyDown(KeyboardKeyEventArgs e)
{
    base.OnKeyDown(e);
    if (OpenTkKeyMapper.Map(e.Key) is { } key)
        KeyboardManager.KeyboardPressed(key);
}

protected override void OnKeyUp(KeyboardKeyEventArgs e)
{
    base.OnKeyUp(e);
    if (OpenTkKeyMapper.Map(e.Key) is { } key)
        KeyboardManager.KeyboardReleased(key);
}
```

`DrawnUiGameWindow` handles editor keys (backspace, arrows, home/end, ctrl+A, tab) automatically. Override `OnKeyDown` and call `base.OnKeyDown(e)` first, then add game key routing.

---

## Fullscreen

`DrawnUiGameWindow` supports fullscreen out of the box:

- **F11** — toggles fullscreen/windowed
- **ESC** — exits fullscreen (returns to windowed)
- **System menu** (right-click title bar or Alt+Space) — includes a "Fullscreen" item on Windows

To toggle programmatically:

```csharp
window.ToggleFullscreen();
```

---

## Window Centering

`DrawnUiGameWindow` centers on the primary monitor at startup with no visible flicker. The window starts hidden, positions itself, then becomes visible.

---

## DWM Title Bar Styling (Windows)

Override `ConfigureWindowChrome` in your `DrawnUiGameWindow` subclass to apply custom DWM colors:

```csharp
class MyWindow(GameWindowSettings gs, NativeWindowSettings ns, Canvas canvas)
    : DrawnUiGameWindow(gs, ns, canvas)
{
    [SupportedOSPlatform("windows")]
    protected override void ConfigureWindowChrome(IntPtr hwnd)
    {
        // Match your app's background color (0x1A, 0x1A, 0x2E = #1A1A2E)
        WindowChrome.SetCaptionColor(hwnd, 0x1A, 0x1A, 0x2E);
        WindowChrome.SetBorderColor(hwnd, 0x1A, 0x1A, 0x2E);
    }
}
```

`WindowChrome` helpers:

| Method | Effect | Min Windows |
|---|---|---|
| `SetCaptionColor(hwnd, r, g, b)` | Title bar background color | Win11 22000 |
| `SetBorderColor(hwnd, r, g, b)` | Window border color | Win11 22000 |
| `SetDarkMode(hwnd, bool)` | Force dark/light title text | Win10 20H1 |
| `SetRoundedCorners(hwnd, bool)` | Rounded/square corners | Win11 22000 |

> When a custom caption color is set, Windows automatically picks black or white title text based on luminance. You do not need `SetDarkMode`.

`ConfigureWindowChrome` is only called on Windows — no `OperatingSystem.IsWindows()` guard is needed inside the override.

---

## Publish — Self-Contained Single File

For a distributable release build targeting Windows x64:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>true</SelfContained>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>full</TrimMode>
  <PublishSingleFile>true</PublishSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
</PropertyGroup>
```

`TrimMode=full` will cut SkiaSharp font and image APIs unless you protect them:

```xml
<ItemGroup Condition="'$(PublishTrimmed)' == 'true'">
  <TrimmerRootAssembly Include="OpenTK.Graphics" />
  <TrimmerRootAssembly Include="OpenTK.Windowing.Desktop" />
  <TrimmerRootAssembly Include="SkiaSharp" />
  <TrimmerRootAssembly Include="HarfBuzzSharp" />
</ItemGroup>
```

To strip PDB files from the publish output:

```xml
<Target Name="ExcludePdbsFromPublish" AfterTargets="ComputeFilesToPublish">
  <ItemGroup>
    <ResolvedFileToPublish Remove="@(ResolvedFileToPublish)"
                           Condition="'%(Extension)' == '.pdb'" />
  </ItemGroup>
</Target>
```

Suppress the console window on Windows:

```xml
<OutputType>WinExe</OutputType>
```

---

## Window Icon

Set the file system icon (Explorer, taskbar) via `ApplicationIcon` and embed it as a resource for the title bar:

```xml
<PropertyGroup>
  <ApplicationIcon>icon.ico</ApplicationIcon>
</PropertyGroup>

<ItemGroup>
  <EmbeddedResource Include="icon.ico" />
</ItemGroup>
```

Load the embedded ICO at runtime and pass it to `NativeWindowSettings.Icon`:

```csharp
OpenTK.Windowing.Common.Input.WindowIcon? LoadWindowIcon()
{
    try
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("icon.ico", StringComparison.OrdinalIgnoreCase));
        if (name == null) return null;

        using var stream = asm.GetManifestResourceStream(name);
        if (stream == null) return null;

        using var bitmap = SKBitmap.Decode(stream);
        if (bitmap == null) return null;

        var resized = bitmap.Width != 32 || bitmap.Height != 32
            ? bitmap.Resize(new SKImageInfo(32, 32), SKFilterQuality.High)
            : bitmap;

        var pixels = resized.Bytes;
        // SKBitmap is BGRA; OpenTK wants RGBA
        for (int i = 0; i < pixels.Length; i += 4)
            (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);

        var image = new OpenTK.Windowing.Common.Input.Image(32, 32, pixels);
        if (!ReferenceEquals(resized, bitmap)) resized.Dispose();
        return new OpenTK.Windowing.Common.Input.WindowIcon(image);
    }
    catch { return null; }
}
```

Then:

```csharp
var nativeSettings = new NativeWindowSettings
{
    Icon = LoadWindowIcon(),
    // ...
};
```

---

## Game Timing

Frame time starts from `OnLoad()`, not system boot. `DrawnGame.LastFrameTimeNanos` begins near `0` and the first delta is a few milliseconds — matching MAUI and Blazor behavior.

---

## Samples

- **`OpenTkPong`** (`src/OpenTk/Samples/OpenTkPong/`) — Way 1 with `UpdateMode = Constant`. Fully drawn Pong game with an `AspectLayer` that scales the viewport uniformly as the window resizes.
- **`OpenTkGpuHost`** (`src/OpenTk/Samples/OpenTkGpuHost/`) — Way 1 with `UpdateMode = Dynamic`. Event-driven UI app that sleeps between frames and wakes only on canvas invalidation.
- **`OpenTkOverlay`** (`src/OpenTk/Samples/OpenTkOverlay/`) — Way 2 with `CanvasHost`. Rotating 3D cube rendered with raw OpenGL via a `GlCubeView` (first canvas child); a DrawnUI dialog panel overlaid on top with live rotation display, text input, and a Reset button. The cube renders inside the Skia pass so both scene and UI are always composited together in one `host.Render()` call.

---

## Related

- [Platforms and Packages](../platforms.md)
- [Startup Settings](../startup-settings.md)
- [Game UI and Interactive Games](../advanced/game-ui.md)
