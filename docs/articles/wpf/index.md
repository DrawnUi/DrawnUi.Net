# DrawnUI for WPF

Host DrawnUI canvases inside a WPF window. `DrawnUiElement` is a normal `FrameworkElement`: place it anywhere in your XAML, put drawn controls inside, bind them to your view model, style them with WPF styles. Rendering is SkiaSharp, on the GPU through ANGLE (Direct3D shared texture) or in software.

## When to use

| Use case | Recommendation |
|---|---|
| Add drawn, animated, GPU-rendered UI to an existing WPF app | `DrawnUiElement` inside your window, drawn tree in XAML or C# |
| Share one drawn UI between WPF, MAUI, Blazor and OpenTK | Build it in shared C#, host it with `DrawnUiElement` on WPF |
| Full-window drawn app with navigation | `DrawnUiElement` + `SkiaShell` |

---

## Install

```bash
dotnet add package DrawnUi.Wpf
```

Targets `net9.0-windows` and `net10.0-windows`. Windows only. For games add `DrawnUi.Wpf.Game`, see [Games](#games).

---

## Initialization

Register fonts once before the first element is shown, for example in `App.OnStartup`:

```csharp
Super.UseDrawnUi()
    .ConfigureFonts(fonts =>
    {
        fonts.AddFont("fonts/OpenSans-Regular.ttf", "FontText");
        fonts.AddFont("fonts/OpenSans-Semibold.ttf", "FontText", FontWeight.SemiBold);
    })
    .Build();
```

Fonts, images, Lottie and `.sksl` shader files are read from next to the executable. Mark them as content:

```xml
<ItemGroup>
  <Content Include="fonts\**;images\**;shaders\**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

### Startup settings

The same `DrawnUiStartupSettings` a MAUI app passes to `UseDrawnUi` go through `.WithSettings(...)` on this head. A full startup, as in the Pong sample:

```csharp
using DrawnUi.Draw;
using DrawnUi.Wpf;

protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    Super.UseDrawnUi()
        .ConfigureFonts(fonts => fonts
            .AddFont("fonts/Orbitron-Regular.ttf", "FontGame")
            .AddFont("fonts/Orbitron-SemiBold.ttf", "FontTextBold", FontWeight.SemiBold))
        .ConfigureStyles(styles => styles
            .AddStyle(new Style
            {
                TargetType = typeof(SkiaLabel),
                ApplyToDerivedTypes = true,
                Setters = { new Setter { Property = SkiaLabel.FontFamilyProperty, Value = "FontGame" } },
            }))
        .WithSettings(new DrawnUiStartupSettings
        {
            DesktopWindow = new WindowParameters { Width = 500, Height = 800, IsFixedSize = true },
            UseDesktopKeyboard = true,
            Logger = logger,
            Startup = services => { /* runs once, after DrawnUI is initialized */ },
        })
        .Build();
}
```

| Setting | On WPF |
|---|---|
| `ConfigureFonts` | `AddFont(path, alias, weight)`, paths relative to the executable |
| `ConfigureStyles` | `AddStyle(new Style { TargetType, ApplyToDerivedTypes, Setters })`, the drawn styles system; a `SkiaButton` needs its own style, it pushes its font onto its caption |
| `PreloadAssets` | `AddImage(alias, path)`, decoded before the first frame |
| `DesktopWindow` | Sizes the window that hosts the first `DrawnUiElement`, in device-independent pixels; `IsFixedSize` sets `ResizeMode = NoResize` |
| `UseDesktopKeyboard` | Every key pressed in that window reaches `KeyboardManager`, whatever has focus, as on MAUI Windows and Mac. Without it keys arrive only while a `DrawnUiElement` has keyboard focus |
| `Logger` | Receives what `Super.Log` writes |
| `Startup` | Runs once with `Super.Services` when the first element initializes DrawnUI |
| `MobileIsFullscreen` | No meaning on WPF, ignored |

Settings apply when the first element loads, so they need no window to exist at `OnStartup`.

---

## The element

```xml
<Window xmlns:draw="http://schemas.appomobi.com/drawnUi/2023/draw" ...>
    <draw:DrawnUiElement RenderingMode="Accelerated">
        <draw:SkiaStack Spacing="16" HorizontalOptions="Fill" VerticalOptions="Center">
            <draw:SkiaLabel Text="{Binding Greeting}" FontSize="16" TextColor="#9BE15D" />
            <draw:SkiaShape Type="Rectangle" BackgroundColor="Red" CornerRadius="16,16,4,4" Padding="18,10">
                <draw:SkiaLabel Text="{Binding Pokes}" FontSize="56" TextColor="White" />
            </draw:SkiaShape>
            <draw:SkiaButton Text="Poke the canvas" WidthRequest="220" CommandTapped="{Binding PokeCommand}" />
        </draw:SkiaStack>
    </draw:DrawnUiElement>
</Window>
```

What makes this ordinary WPF XAML:

- Every DrawnUI bindable property is mirrored by a WPF `DependencyProperty`, so `{Binding}`, `Style` setters and triggers work on drawn controls. The value still lives in DrawnUI; the dependency property is only the WPF-facing surface.
- The window's `DataContext` flows into the drawn tree.
- WPF string forms are accepted for colors, `Thickness` and `CornerRadius`.

The same tree can be built in C# with the [fluent extensions](../fluent-extensions.md) and assigned to `Content`.

### Properties

| Property | Values | Notes |
|---|---|---|
| `RenderingMode` | `Default`, `Accelerated`, `AcceleratedRetained` | `Default` paints into a `WriteableBitmap`. `Accelerated` renders through ANGLE into a Direct3D texture shown by `D3DImage`. `AcceleratedRetained` keeps the previous frame so only changed areas are redrawn. Read when the element loads; later changes are ignored |
| `Gestures` | `Enabled` (default), `Lock`, `Disabled` | See [Input](#input) |
| `Content` | drawn root | Set from XAML or C# |
| `ContentBuilder` | `Func<SkiaControl>` | Builds the root now and again after every C# Hot Reload |

A canvas of your own type (a game's `RescalingCanvas`, a subclass with an overridden draw) is hosted through the `DrawnUiElement(Func<Canvas> createCanvas)` constructor; the factory runs after DrawnUI is initialized.

A frame is produced only when the drawn tree asked for one; an idle canvas costs nothing per tick. With GPU rendering the first frame is pre-rendered on the CPU, so content appears while ANGLE is still starting.

---

## Input

Mouse buttons all reach the drawn tree with `PointerData` (button, pressed buttons, device type); right button also opens `SkiaControl.ContextMenu`. The wheel scrolls `SkiaScroll`. Keyboard goes through the shared `KeyboardManager` and into a focused `SkiaEditor`: arrows, Shift+arrows, Home/End, Up/Down, Backspace/Delete, Ctrl+A/C/X/V with the system clipboard, IME text input.

`Gestures` decides what the canvas keeps:

- `Enabled`: input goes to the drawn tree; whatever no drawn control used stays unhandled, so a wheel over non-scrolling drawn content keeps scrolling a hosting `ScrollViewer` and an unused click still bubbles.
- `Lock`: the canvas keeps every pointer event. Use inside a `ScrollViewer` whose own panning must never move the page.
- `Disabled`: the canvas takes no mouse, touch or wheel input.

Touch is handled natively, one pointer per finger, so pinch and rotate reach the drawn tree as `ManipulationInfo`.

---

## Accessibility

Every node of the engine's accessibility snapshot is exposed as a UI Automation peer under the canvas pane, with role, name, help text, Invoke and Toggle patterns and live regions. Tab and Shift+Tab walk the interactive nodes with a drawn focus ring, Enter or Space activates, Escape leaves. See [Accessibility](../advanced/accessibility.md).

---

## Hot reload

C# Hot Reload works under Visual Studio, Rider and `dotnet watch`: after an edit the head raises `Super.HotReload`, `DrawnUiElement.ContentBuilder` rebuilds its root, and `SkiaShell` re-creates the page on screen from its route while keeping the navigation stack. Page state is lost, navigation state is not.

---

## Games

```bash
dotnet add package DrawnUi.Wpf.Game
```

`DrawnUi.Wpf.Game` is the game addon for this head, the same `DrawnGame` base class as `DrawnUi.Maui.Game`, `DrawnUi.OpenTk.Game`, `DrawnUi.Blazor.Game` and `DrawnUi.Wasm.Game`: a fixed-step game loop with a frame-time interpolator, keyboard state through `KeyboardManager`, pause and resume. A game written against it is shared source between the heads.

A game usually wants a fixed logical viewport that scales with the window and keeps its aspect ratio. That is a `Canvas` subclass (`RescalingCanvas` in the Pong sample), and the element hosts one through its factory constructor:

```csharp
var element = new DrawnUiElement(() => new RescalingCanvas
{
    LogicalWidth = PongGame.WIDTH,
    LogicalHeight = PongGame.HEIGHT,
    UpdateMode = UpdateModeType.Constant,
})
{
    RenderingMode = RenderingModeType.Accelerated,
    Gestures = GesturesMode.Lock,
    Content = new PongGame(),
};
```

`Gestures = Lock` keeps every pointer event on the canvas, `UpdateMode = Constant` draws every composition tick. The window should give the element keyboard focus once loaded (`Keyboard.Focus(element)`). The complete host is the [WpfPong](samples.md#wpfpong) sample; the game design side is in [Game UI](../advanced/game-ui.md).

---

## How the head works, and what it taught us

Things that are not obvious from the API and shaped the implementation. They matter if you extend the head or debug frame pacing.

- **Frames are paced by WPF's composition tick.** `CompositionTarget.Rendering` is the pacer for both rendering modes; a frame is produced only when the drawn tree asked for one (`Update()`), so an idle canvas costs nothing. That tick runs at the display rate while WPF has something to render and drops to about 30 Hz when it does not, which is why a canvas that skipped a frame used to look like a stall.
- **Animations advance by the tick's `RenderingTime`, bounded to the wall clock.** The tick lands a couple of milliseconds early or late around the vsync while the frame is shown at the vsync itself; sampling the wall clock at the tick puts that jitter into every animated position. `RenderingTime` is the frame's presentation time, one uniform step per frame. WPF's estimate sometimes leaps a whole extra frame while the ticks keep their cadence, so the step is clamped to the wall-clock step plus or minus 4 ms, otherwise a game ball moves two frames in one displayed frame.
- **The `D3DImage` protocol is lock, draw, dirty rect, unlock.** In accelerated mode ANGLE draws into a Direct3D 11 texture that WPF reads through a Direct3D 9 share handle. WPF's render thread copies that texture whenever it is not locked, so drawing outside the lock let it copy a half-drawn frame. And a share handle carries no synchronization object between the two devices: the GL work has to be complete (`glFinish`), not merely submitted, before the dirty rect is announced, or a moving object tears. The wait is the cost of the frame itself, 1-3 ms for a typical screen.
- **The texture is a pbuffer, not a swap chain,** so it persists between frames: `AcceleratedRetained` costs nothing extra, and WPF composes the canvas like any other element. Drawn content can sit under WPF overlays, in a `ScrollViewer`, in a tab; there is no airspace problem.
- **The first GPU frame is pre-rendered on the CPU.** ANGLE start-up plus the first GPU frame block the UI thread for a few hundred milliseconds; one software frame is shown first and the GPU takes over on the next tick.
- **Assets load relative to the executable.** `SkiaFontManager` and the image loader combine the path with `AppContext.BaseDirectory`, so fonts, images, Lottie and `.sksl` files need `CopyToOutputDirectory`; a bare `Content` item in a WinExe project is not copied and the asset fails silently at runtime.
- **The head is not the MAUI Windows head.** A `net*-windows` target makes the SDK define `WINDOWS`, which shared DrawnUI code reads as "MAUI WinUI". `DrawnUi.Wpf` and `DrawnUi.Wpf.Game` strip that define (and `ONPLATFORM`) in their project files; an app project needs nothing.
- **Mouse wheel notches accumulate.** A notch arriving while the previous wheel scroll animates adds onto that scroll's destination, so a fast spin travels the full distance. This is shared engine behaviour since 1.10.6.18 and was found here, because the desktop wheel is the main way to scroll on WPF.

---

## Known limitations

- Touch and pen are implemented against WPF touch events but have not been exercised on touch hardware yet. Pen pressure is not read.
- XAML Hot Reload for drawn controls is untested (C# Hot Reload is).
- Editor Shift+arrow and Ctrl combinations were verified in code, not with physical keys.

---

## Samples

See [WPF Samples](samples.md).

---

## Related

- [WPF Samples](samples.md)
- [Platforms and Packages](../platforms.md)
- [Startup Settings](../startup-settings.md)
- [Fluent C# Extensions](../fluent-extensions.md)
- [Navigation Shell](../controls/shell.md)
- [Accessibility](../advanced/accessibility.md)
- [Game UI](../advanced/game-ui.md)
