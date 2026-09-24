# DrawnUI for WPF

`DrawnUi.Wpf` hosts the DrawnUI rendering engine inside a WPF window. One `DrawnUiElement` is a
normal `FrameworkElement`; everything inside it is drawn with SkiaSharp, on the GPU through ANGLE
or in software, and takes part in WPF XAML, `{Binding}`, styles and triggers.

Preview. Windows only, .NET 9 and .NET 10 (`net9.0-windows`, `net10.0-windows`).

## Install

```bash
dotnet add package DrawnUi.Wpf --prerelease
```

## Startup

Register fonts once, before the first element is shown (for example in `App.OnStartup`). Font
files, images, Lottie and `.sksl` shader files are read from next to the executable, so mark them
as content in the app project:

```csharp
Super.UseDrawnUi()
    .ConfigureFonts(fonts => fonts
        .AddFont("fonts/OpenSans-Regular.ttf", "FontText")
        .AddFont("fonts/OpenSans-Semibold.ttf", "FontText", FontWeight.SemiBold))
    .Build();
```

```xml
<ItemGroup>
  <Content Include="fonts\**;images\**;shaders\**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

## A drawn canvas in XAML

```xml
<Window xmlns:draw="http://schemas.appomobi.com/drawnUi/2023/draw" ...>
    <draw:DrawnUiElement RenderingMode="Accelerated">
        <draw:SkiaStack Spacing="16" HorizontalOptions="Fill" VerticalOptions="Center">
            <draw:SkiaLabel Text="{Binding Greeting}" FontSize="16" TextColor="#9BE15D" />
            <draw:SkiaButton Text="Poke" WidthRequest="220" CommandTapped="{Binding PokeCommand}" />
        </draw:SkiaStack>
    </draw:DrawnUiElement>
</Window>
```

Every DrawnUI bindable property is mirrored by a WPF dependency property, so bindings, `Style`
setters and triggers work on drawn controls. The window's `DataContext` flows into the drawn tree.
Colors, thickness and corner radius accept the WPF string forms (`"#RRGGBB"`, `"16,16,4,4"`).

Or build the tree in C# (see the fluent extensions) and assign it to `Content`; with
`ContentBuilder` the tree is rebuilt on every C# Hot Reload.

## Element properties

| Property | Values | Notes |
|---|---|---|
| `RenderingMode` | `Default`, `Accelerated`, `AcceleratedRetained` | Software (`WriteableBitmap`) or GPU through ANGLE on a shared Direct3D texture. Read when the element loads. The first frame is pre-rendered on the CPU so content shows before the GPU is up |
| `Gestures` | `Enabled` (default), `Lock`, `Disabled` | `Enabled`: input a drawn control did not use bubbles on to WPF (a wheel keeps scrolling a hosting `ScrollViewer`). `Lock`: the canvas keeps every event. `Disabled`: no input |
| `Content` / `ContentBuilder` | drawn root | `ContentBuilder` re-runs after C# Hot Reload |

Rendering is idle-gated: nothing is painted while the drawn tree is clean.

## What works on this head

- All drawn controls, layouts, caching, animations, Lottie, GIF, sprites, SVG, shaders, virtualized lists.
- Mouse (every button, with `PointerData`), wheel, context menu, keyboard through `KeyboardManager`.
- `SkiaEditor` with clipboard (Ctrl+A/C/X/V), Home/End, Up/Down, IME text input.
- Multi-touch with pinch and rotate through `MultitouchTracker` (see limits).
- Accessibility: every node of the accessibility snapshot is a UI Automation peer; Tab / Shift+Tab walk
  interactive nodes with a focus ring, Enter or Space activates, Escape leaves.
- `SkiaShell` for drawn navigation: pages, tabs, popups, modals, toasts, `IVisibilityAware` callbacks.
- C# Hot Reload under Visual Studio, Rider and `dotnet watch`.

## Preview limits

- Touch and pen were implemented against the WPF touch events but not yet exercised on touch hardware; pen pressure is not read.
- XAML Hot Reload for drawn controls is untested; C# Hot Reload is.
- Editor Shift+arrow and Ctrl combinations were tested in code only.
- No native control embedding (`SkiaMauiElement` has no WPF equivalent yet).

## Samples

In the repository, [src/Wpf/Samples](https://github.com/DrawnUi/DrawnUi.Net/tree/main/src/Wpf/Samples):

- [HelloWpf](https://github.com/DrawnUi/DrawnUi.Net/tree/main/src/Wpf/Samples/HelloWpf) — the full demo, one page per feature: cells, images, SVG, shapes, text, layouts, looks, snapping, animations, shell, editor, keyboard, scroll, shaders, sprites, transforms, reorder, accessibility.
- [WpfSandbox](https://github.com/DrawnUi/DrawnUi.Net/tree/main/src/Wpf/Samples/WpfSandbox) — a XAML window with `{Binding}` to a view model and a WPF style, referencing the head from source.
- [WpfPackageDemo](https://github.com/DrawnUi/DrawnUi.Net/tree/main/src/Wpf/Samples/WpfPackageDemo) — the same window consuming the NuGet package; copy it to start a new app.
- [WpfPong](https://github.com/DrawnUi/DrawnUi.Net/tree/main/src/Wpf/Samples/WpfPong) — the Pong game shared with the OpenTK and WebAssembly samples, hosted in a WPF window through the `DrawnUi.Wpf.Game` addon: a `DrawnGame` loop, keyboard and mouse input, a `RescalingCanvas` that keeps the game's aspect ratio when the window resizes.

Docs: https://drawnui.net/articles/wpf/
