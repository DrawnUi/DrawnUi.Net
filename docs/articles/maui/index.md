# .NET MAUI

Run DrawnUI canvases inside a .NET MAUI app on Windows, Android, iOS and MacCatalyst.

## When to use

* Use drawn controls/UIs across the app
* Draw the entire app on one DrawnUI canvas

---

## Start here

- [Installation and Setup](getting-started.md)
- [MAUI Tutorials and Host Notes](tutorials.md)
- [Startup Settings](../startup-settings.md)
- [Handling Gestures](../gestures.md)
- [Drawn Layouts](../layouts.md)
- [Porting Native to Drawn](../porting-maui.md)

## Samples

Every host in the repository ships the same two samples, Hello and Pong:

- [HelloMaui](https://github.com/DrawnUi/DrawnUi.Net/tree/main/src/Maui/Samples/HelloMaui): the full feature demo, one page per feature behind a drawn `SkiaShell` (cells, images, SVG, shapes, text, layouts, looks, carousels, Lottie, shell, editor, keyboard, scroll, shaders, sprites, transforms, drag to reorder, accessibility). Same pages as HelloWpf and the React demo.
- [MauiPong](https://github.com/DrawnUi/DrawnUi.Net/tree/main/src/Maui/Samples/MauiPong): the Pong game, a thin host over the shared `src/Shared/Samples/Pong.Shared` game with the `DrawnUi.Maui.Game` addon. A `RescalingCanvas` keeps the game's logical viewport and aspect ratio in any window, `Gestures = Lock` gives the game the whole input stream, `UseDesktopKeyboard` makes the arrow keys move the paddle on Windows and Mac. See [Game UI](../advanced/game-ui.md) for the addon per host.

Both are `dotnet build -f net10.0-windows10.0.19041.0` on Windows, or the usual MAUI targets for Android, iOS and MacCatalyst. More apps: [Sample Apps](../sample-apps.md).

## When MAUI is the right host

Choose the MAUI lane when you need:

- a native app host for mobile or desktop
- full control over gesture-heavy or animation-heavy screens
- a custom UI rendered by DrawnUI on top of MAUI app structure
- access to MAUI platform services while keeping the visible UI fully drawn

## Package

```bash
dotnet add package DrawnUi.Maui
```

## Related docs

- [Platforms and Packages](../platforms.md)
- [Tutorial Host Guide](../tutorials.md)
- [Installation and Setup](getting-started.md)
- [MAUI Tutorials and Host Notes](tutorials.md)
- [Startup Settings](../startup-settings.md)
- [Handling Gestures](../gestures.md)
- [Porting Native to Drawn](../porting-maui.md)