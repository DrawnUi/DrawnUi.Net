# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

## Working with Claude

- Explain fixes and solutions BEFORE implementing — wait for approval.
- No summaries or explanations AFTER approved changes are made. Make the changes and stay silent.

---

## Project Overview

DrawnUI is a cross-platform rich UI rendering engine that draws controls with SkiaSharp instead of native widgets. It targets:

| Target | Implementation | Platforms |
|---|---|---|
| .NET MAUI | `src/Maui/` | iOS, Android, Windows, MacCatalyst |
| Blazor | `src/Blazor/` | Browser (WASM), Server, Hybrid |
| OpenTK | `src/OpenTk/` | Windows, Linux |
| Native Windows | `src/Native/` | WinUI |

**Technology stack:**
- .NET 9 and .NET 10 (both supported; MAUI targets multi-TFM)
- SkiaSharp v3
- Hardware-accelerated rendering via Skia GPU canvas

---

## Project Structure

```
src/
  Shared/           # Cross-platform Skia rendering core (shared by all targets)
  SharedNet/        # Shared .NET (non-MAUI) code — used by OpenTK and Net
  SharedGame/       # Shared game loop and input
  Maui/
    DrawnUi/        # Main MAUI library
    Addons/         # Camera, Game, MapsUi, Rive, MauiGraphics addons
    Samples/        # Sandbox, Tutorials, GameTemplate, Player, FastRepro
  Net/
    DrawnUi/        # .NET desktop base (used by OpenTK)
    Samples/        # SkiaEditorHarness
  OpenTk/
    DrawnUi/        # OpenTK integration: DrawnUiWindow, CanvasHost, GpuDrawable
    Addons/         # DrawnUi.OpenTk.Game
    Samples/        # OpenTkPong, OpenTkGpuHost, OpenTkOverlay
  Blazor/
    DrawnUi/        # Blazor component implementation
    DrawnUi.Server/ # Server-side host
    DrawnUi.Wasm/   # WASM-specific
    Addons/         # DrawnUi.Blazor.Game
    Samples/        # BlazorSandbox, BlazorSandboxHybrid, BlazorSandboxServer
  Native/
    DrawnUi.Native.Windows/  # WinUI native
  Tests/            # UnitTests, SomeBenchmarks
dev/                # Build/pack scripts and dev solution files
nugets/             # NuGet pack scripts
docs/               # Documentation (articles, api)
```

**Key architectural components:**
- `SkiaControl` — base class for all drawn controls
- `Canvas` — hosts drawn controls; wraps the Skia surface
- `SkiaShell` — navigation system for drawn apps
- Caching system: Operations, Image, ImageDoubleBuffered, GPU, ImageComposite
- Gesture handling system for touch and pointer input
- Layout system: Column, Row, Grid, Wrap, Absolute

---

## Build Commands

**Main solution (all targets):**
```bash
dotnet build src/DrawnUi.sln
```

**MAUI library only:**
```bash
dotnet build src/Maui/DrawnUi/DrawnUi.Maui.csproj --configuration Debug
```

**MAUI Camera addon:**
```bash
dotnet build src/Maui/Addons/DrawnUi.Maui.Camera/DrawnUi.Maui.Camera.csproj --configuration Debug
```

**OpenTK library:**
```bash
dotnet build src/OpenTk/DrawnUi/DrawnUi.OpenTk.csproj --configuration Debug
```

**OpenTK sample (OpenTkOverlay):**
```bash
dotnet build src/OpenTk/Samples/OpenTkOverlay/OpenTkOverlay.csproj --configuration Debug
dotnet run --project src/OpenTk/Samples/OpenTkOverlay/OpenTkOverlay.csproj
```

**NuGet packages:**
```bash
cd nugets
./makenugets.bat   # Windows
```

**Tests:**
```bash
dotnet test src/Tests/UnitTests/UnitTests.csproj
```

**Clean build artifacts:**
```powershell
# From src/ directory
./DeleteBinObj.ps1
```

---

## MAUI Initialization

```csharp
builder.UseDrawnUi(new()
{
    UseDesktopKeyboard = true,
    DesktopWindow = new()
    {
        Width = 375,
        Height = 800
    }
});
```

**Platform requirements:**
- Resources in `Resources/Raw` — MAUI requires lowercase filenames (uppercase may fail on iOS)
- Minimum OS versions defined in `src/Maui/Directory.Build.props`

---

## OpenTK Initialization

```csharp
Super.UseDrawnUi()
    .ConfigureFonts(fonts =>
    {
        fonts.AddFont("fonts/MyFont.ttf", "MyFont");
    })
    .Build();
```

Call `Super.Init()` inside `OnLoad()` when subclassing `GameWindow` directly (not required when using `DrawnUiWindow`).

### Two patterns

**`DrawnUiWindow`** — base class for fully drawn apps/games. Handles surface, vsync, input.  
Override `RenderScene()` to draw a 3D scene before the DrawnUI canvas:

```csharp
protected override void RenderScene()
{
    GL.Enable(EnableCap.DepthTest);
    DrawMyScene();
    GL.Finish();
}
```

**`CanvasHost`** — standalone host when your own `GameWindow` owns the render loop and DrawnUI is an overlay. Correct render order per frame:

```csharp
// 1. Restore GL state Skia left dirty
GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
GL.Disable(EnableCap.StencilTest);
GL.DepthMask(true);
GL.ColorMask(true, true, true, true);
// 2. Your GL scene
GL.Clear(...); DrawScene(); GL.Finish();
// 3. DrawnUI overlay
_host!.ResetGrContext();
_host!.Render();
SwapBuffers();
```

**Canvas must use `AcceleratedRetained` + transparent background** for overlays — this skips Skia's canvas clear so GL content underneath is preserved.

**GL state note:** Skia leaves viewport, stencil test, depth mask, and color mask dirty after rendering. The 4-line restore block above is required before every raw GL draw or geometry will be invisible.

---

## Important Development Notes

**AI assistance
- when working on shared specific test yourself using .NET harness

**SkiaSharp version:**
- All targets use SkiaSharp v4
- .NET 8 and 1.x are no longer supported

**Caching strategy:**
- `Operations` — shapes, SVG, text (SKPicture-based). **Never** use inside GPU-cached parent or for controls with GPU-surface shaders. 
- `Image` — simple bitmap cache, works at any size
- `ImageDoubleBuffered` — best for animations; doubles memory
- `GPU` — hardware-accelerated; for graphics memory caching. **Never** nest inside `Operations` parent.
- `ImageComposite` / `ImageCompositeGPU` — composite modes for containers with children that change dynamically. When optimzing memory use prefer `Operations` for parent container in such case.
- Never cache: `SkiaScroll`, `SkiaDrawer`, `SkiaCarousel`, `SkiaMauiElement` and derived
- GPU memory is very limited, best for small overlays. 
- Decide caching from redraw source and subtree cost. Over live surfaces that repaint every frame, even small stable overlays benefit from top-level cache.

**Layout optimizations:**
- `IsParentIndependent` - when using dunamically changing controls (status labels etc) inside auto-sized layouts prefer specifying labels heights explicitely and set `IsParentIndependent=true` to avoid making them force parent remeasuring upon text value change. Not needed for parent layouts which has Fill or explicit size requests.


**Layout differences from standard MAUI:**
- Default `HorizontalOptions` / `VerticalOptions` are `Start`, not `Fill`
- Grid default spacing is 1, not 8

---

## Code-Behind UI — Critical Patterns

### Inline children (mandatory)

Never declare a local variable for a control and reference it in a children list. Always construct inline:

```csharp
// WRONG
var label = new SkiaLabel { Text = "hi" };
Children = new List<SkiaControl> { label };

// RIGHT
Children = new List<SkiaControl>
{
    new SkiaLabel { Text = "hi" },
};
```

Use `.Assign(out _field)` to capture a reference:

```csharp
Children = new List<SkiaControl>
{
    new SkiaLabel { Text = "0°" }.Assign(out _angleLabel),
};
```

### Event / gesture wiring (mandatory)

Always use fluent extensions — never `+=` or command binding:

| Task | Use |
|---|---|
| Tap | `.OnTapped(me => { ... })` |
| Text changed | `.OnTextChanged(text => { ... })` |
| Arbitrary setup | `.Adapt(me => { ... })` |
| Paint hook | `.WhenPaint((me, ctx) => { ... })` |
| Observe property | `.ObserveProperty(source, nameof(Prop), me => { ... })` |

### Dynamic children

Use `AddSubView()` instead of `Children.Add()`. For clearing use `ClearChildren()`.

### Recycled / reusable cells

Pre-create ALL UI elements in the constructor. At runtime only update properties — never add, remove, or clear children:

```csharp
public class MyCell : SkiaDynamicDrawnCell
{
    private List<SkiaLayout> _slots;

    public MyCell()
    {
        _slots = Enumerable.Range(0, MaxSlots)
            .Select(_ => CreateSlot())
            .ToList();
        foreach (var s in _slots) AddSubView(s);
    }

    protected override void SetContent(object ctx)
    {
        // Only set IsVisible, Text, Color — never AddSubView/RemoveSubView
    }
}
```

### Other rules

- No MAUI bindings — use `.ObserveProperty()` / `.ObserveProperties()`
- No `MainThread.BeginInvokeOnMainThread` — DrawnUI doesn't need it
- `Content` property type is `SkiaControl`, not `View`

---

## Layout Types

`SkiaLayout` with `Type`:
- `LayoutType.Column` — vertical stack
- `LayoutType.Row` — horizontal stack
- `LayoutType.Grid` — grid; use `ColumnDefinitions="35,*,100"` string format
- `LayoutType.Wrap` — flex/wrap
- `LayoutType.Absolute` — absolute positioning (prefer over Grid where possible)

`SkiaLayer` = `SkiaLayout` with `Type=Absolute`, shorthand for layered content.

---

## XAML Usage

Controls must be wrapped in `<draw:Canvas>`. Set `Gestures="Enabled"` (or `SoftLock` for panning). Set explicit size or Fill to avoid auto-size recalculations.

```xml
<draw:Canvas HorizontalOptions="Fill" VerticalOptions="Start"
             RenderingMode="Default" Gestures="Enabled">
    <draw:SkiaLayout Type="Column">
        <draw:SkiaLabel Text="Hello" />
    </draw:SkiaLayout>
</draw:Canvas>
```

Grid in XAML (string definitions):
```xml
<draw:SkiaLayout Type="Grid"
    ColumnDefinitions="35,*,100"
    RowDefinitions="Auto,*,50">
    <draw:SkiaSvg Grid.Column="0" Grid.Row="0" Source="icon.svg" />
</draw:SkiaLayout>
```

Rich text:
```xml
<draw:SkiaLabel>
    <draw:TextSpan Text="Normal " />
    <draw:TextSpan Text="Bold" IsBold="True" TextColor="Red" />
    <draw:TextSpan Text="&#10;" />
</draw:SkiaLabel>
```

Control mappings: `StackLayout`→`Column`, `Grid`→`Grid`, `FlexLayout`→`Wrap`, `Label`→`SkiaLabel`, `Span`→`TextSpan`.

See `docs/articles/fluent-extensions.md` and `docs/articles/porting-maui.md` for more.

---

## Resource Loading

- Web URLs — loaded from web
- `file://` prefix — loaded from native file system
- Otherwise — loaded from `Resources/Raw` bundle folder

---

## Core Engineering Strategy

This is a rendering engine. Avoid allocations during frame processing to minimize GC pressure and maintain max FPS.

When modifying any method:
- Find ALL call sites with Grep before changing shared code
- Verify changes don't break other execution paths
- Add parameters or create specialized versions when a fix is context-specific
- Never disable or remove a feature to fix an issue
- Never make one area worse to fix another
