# DrawnUI for WPF

Host DrawnUI canvases inside a WPF window. `DrawnUiElement` is a normal `FrameworkElement`: place it anywhere in your XAML, put drawn controls inside, bind them to your view model, style them with WPF styles. Rendering is SkiaSharp, on the GPU through ANGLE (Direct3D shared texture) or in software.

> **Preview.** `DrawnUi.Wpf` is published as a prerelease package. See [limits](#preview-limits) before shipping with it.

## When to use

| Use case | Recommendation |
|---|---|
| Add drawn, animated, GPU-rendered UI to an existing WPF app | `DrawnUiElement` inside your window, drawn tree in XAML or C# |
| Share one drawn UI between WPF, MAUI, Blazor and OpenTK | Build it in shared C#, host it with `DrawnUiElement` on WPF |
| Full-window drawn app with navigation | `DrawnUiElement` + `SkiaShell` |

---

## Install

```bash
dotnet add package DrawnUi.Wpf --prerelease
```

Targets `net9.0-windows` and `net10.0-windows`. Windows only.

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

## Preview limits

- Touch and pen are implemented against WPF touch events but have not been exercised on touch hardware yet. Pen pressure is not read.
- XAML Hot Reload for drawn controls is untested (C# Hot Reload is).
- Editor Shift+arrow and Ctrl combinations were verified in code, not with physical keys.
- No native control embedding: `SkiaMauiElement` has no WPF equivalent yet.

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
