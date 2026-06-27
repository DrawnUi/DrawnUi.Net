# DrawnUI for OpenTK

Run DrawnUI canvases inside an OpenTK `GameWindow` on Windows and Linux.

## When to use

| Use case | Recommendation |
|---|---|
| Create cross-platform DrawnUI app/game, same code will run in browser etc | `DrawnUiWindow` -> `Canvas` |
| Add rich UIs overlay to your existing app scene | `CanvasHost`-> `Canvas` |

---

## Install

`DrawnUi.OpenTk` is currently distributed as a project reference:

```bash
dotnet add package DrawnUi.OpenTk
```

---

## Initialization

Call `Super.UseDrawnUi().Build()` once before creating windows or canvases:

```csharp
Super.UseDrawnUi()
    .ConfigureFonts(fonts =>
    {
        fonts.AddFont("fonts/Orbitron-Regular.ttf", "FontGame");
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

## Samples

See [OpenTK Samples](samples.md) for a fuller walkthrough of the sample projects and which one to start with.

---

## Related

- [OpenTK Samples](samples.md)
- [OpenTK Resources (Images, Lottie, Fonts)](resources.md) — raw assets need `Content` + `CopyToOutputDirectory`
- [Window Patterns](window.md) — `DrawnUiWindow` and `CanvasHost` integration, GL state, render order
- [Input and Window Features](gestures.md) — keys, fullscreen, DWM styling, icon, publish
- [OpenTK FAQ](faq.md)
- [Platforms and Packages](../platforms.md)
- [Startup Settings](../startup-settings.md)
- [Game UI and Interactive Games](../advanced/game-ui.md)
