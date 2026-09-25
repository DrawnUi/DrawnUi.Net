# DrawnUI for .NET
![NuGet DrawnUi.Net](https://img.shields.io/nuget/v/DrawnUi.Net.svg)
![License](https://img.shields.io/github/license/taublast/DrawnUi.svg)
[![PRs Welcome](https://img.shields.io/badge/PRs-Welcome-brightgreen.svg?style=flat)](https://github.com/taublast/drawnui/blob/master/CONTRIBUTING.md)

👉 [Official Site](https://drawnui.net)   

DrawnUI is a rendering and UI composition engine for .NET, powered by [SkiaSharp](https://github.com/mono/SkiaSharp) with gestures, layouts, effects and animations running with hardware acceleration.

🤩 [Fiddle in browser](https://fiddle.drawnui.net) 👈

Supported hosts:

* `DrawnUi.Maui` - Android, iOS, MacCatalyst, and Windows.
* `DrawnUi.Blazor.Wasm` - browser WebAssembly rendering.
* `DrawnUi.Blazor.Server` - server-backed DrawnUI surfaces served by Blazor Server.
* `DrawnUi.Wasm` - pure browser WebAssembly, no Blazor required.
* `DrawnUi.OpenTk` - Windows and Linux desktops.
* `DrawnUi.Wpf` - drawn controls inside WPF windows.
* `DrawnUi.Net` - platform-agnostic console/server rendering scenarios.

## React?

DrawnUI for React just appeared as a standalone DrawnUI engine in TypeScript, running on [CanvasKit](https://skia.org/docs/user/modules/canvaskit/) (Skia compiled to WebAssembly) in the browser. It tends to use same API as the .NET version. 
Under active development, more info [on our site](https://drawnui.net/articles/react).

## Features 

* __Imagine your  UI__ - a toolbox for creating drawn controls
* __Harness the Canvas__ - engine handles everything
* __Port existing native to drawn__ - easy port, bindings support
* __Design in XAML, Razor + Canvas, or code-behind__
* __2D and 3D Transforms__
* __Visual effects__ for every control, filters and shaders
* __Animations__ targeting max FPS
* __Caching system__ for faster re-drawing
* __Optimized for performance__, rendering only visible elements, recycling templates etc
* __Gestures__ support for anything, panning, scrolling, zooming etc
* __Keyboard support__, track any key
* __Navigate__ on the canvas with shell-like techniques 

😎 [Blazor sample in browser](https://drawnui.net/sandbox/) 👈

## Addons

* Create games: `DrawnUi.Maui.Game`, `DrawnUi.Blazor.Game`, `DrawnUi.Wasm.Game`, `DrawnUi.OpenTk.Game`, `DrawnUi.Wpf.Game`.
* .NET MAUI only: `DrawnUi.MauiGraphics`
* .NET MAUI only: `DrawnUi.DrawnUi.MapsUi`
* .NET MAUI only: `DrawnUi.DrawnUi.Camera` - [Separate repo](https://github.com/taublast/DrawnUi.Maui.Camera).

---

## Resources

👉 [Docs and Samples](https://drawnui.net)   
🤖 [AI skills](https://drawnui.net/llms.txt)   
🤩 [Fiddle](https://fiddle.drawnui.net)   
⛹️ [Pong in pure WASM](https://pong.appomobi.com/)

## What's New 1.10.6.19

  * New `tpls/` folder with starter projects for every head: MAUI, WPF, OpenTK (Windows and Linux), Blazor WASM, pure .NET WASM and even React. Each builds and runs as-is, with the whole UI in one method to replace..
  * Emoji and symbols now draw on `DrawnUi.Web` (pure WASM): add them with `fonts.AddEmojis()` and `fonts.AddSymbols()`, the subsets DrawnUi.Blazor already ships. A browser has no system fonts, so without them those glyphs were blank.
  * Images and SVGs with a relative source (`"drawnui.svg"`) now load on `DrawnUi.Web`, and http sources now load on OpenTK. Both heads lacked an `HttpClient`.
  * Fixed `DrawnUi.Web` failing to link when referenced as a NuGet package (`undefined symbol: InterceptBrowserObjects`).
  * The mouse wheel scrolls on `DrawnUi.OpenTk`.
  * An OpenTK window now keeps drawing while you resize it.
  * Mouse wheel: a fast spin travels farther than a slow one. Each notch used to restart the scroll animation and throw away the rest of the previous notch, so spinning faster scrolled less. All heads.
  * `DrawnUi.Wpf` startup options: `Super.UseDrawnUi().WithSettings(new DrawnUiStartupSettings { ... })`, same settings class as MAUI — window size, desktop keyboard feeding `KeyboardManager`, logger, one-time startup action.
  * `DrawnUi.Wpf` accelerated rendering is smooth: no more mixed or torn frames during scrolls and animations, and animations advance by the frame's presentation time instead of a jittery clock sample.
  * New addon `DrawnUi.Wpf.Game`, so the WPF head runs `DrawnGame` like every other head, with a `WpfPong` sample.
  * New sample `MauiPong`: every head now ships the Hello + Pong pair (MAUI, WPF, OpenTK, Blazor, WASM).
  * `SkiaLabel` no longer clips the descenders (g, j, p, q, y) of its last line — glyph ink reaching past the line box is now part of the label's cached surface.
  * Fixed a `SkiaScroll` jumping to the top when a control below the fold (a slider in a scrolled list) started its own drag: a transient measure made the scroll think its content no longer overflowed.
  * `ViewsAdapter.GetCellsInUse()`: all realized cells of a templated layout, for app code that needs the live rows (drag-to-reorder, refreshing a row's look). Recycled cells never appear in `Views`.

 ### Previously

  * Fluent `.Initialize(me => ...)` runs once when the control gets its parent, not at its first measure, so it also runs for controls created invisible or outside the viewport.
  * `SkiaScrollBar.IsDraggable`: desktop behavior, drag the thumb or press the track to jump there; `HideDurationSecs` controls the auto-hide fade. Off by default, the bar stays display-only.
  * Changing `ControlStyle` after a control was measured rebuilds its default content, so switching platform looks at runtime works; sizes pinned by the previous style are released, user-set values stay.
  * Lazy observers (`ObserveProperty`, `ObserveProperties`, `Observe`) resolve their target at first measure again — they missed fields assigned with `.Assign(out ...)`, which drew slider thumbs off the track.
  * `SkiaViewSwitcher` can pop pages pushed while no tab is selected, and stays quiet when `SelectedIndex` is set before its children exist.
  * `SkiaScroll` ignores gestures when `Orientation` is `Neither`, so drags and the wheel reach the parent scroll ([#347](https://github.com/taublast/DrawnUi/issues/347)).
  * `SkiaScroll` keeps its offset through provisional measures (a star row inside a `SkiaGrid`), including after an overscroll bounce; `ScrollToIndex` on a Split layout takes an item index; LoadMore distances are points, not pixels.
  * `SkiaCheckbox` takes its own colours before the check animation shows a frame; the Windows look lost the slider thumb shadow and uses 1.5 pt borders.
  * Android: a `Canvas` kept alive and moved to another window (a cached page shown in a new dialog) no longer stays blank, and two memory leaks around canvas re-attach are fixed. `Super.SetWhiteTextStatusBar()` works under MAUI 10.
  * `DrawnUi.Maui.MapsUi` passes the Android 16 KB page-size check — desktop native packages no longer ship inside the APK.
  * `SkiaEditor` puts fast-typed characters in the right place, centers its text, and no longer leaks the native entry on iOS.
  * New samples and docs: `HelloMaui` (19 pages via `SkiaShell`), plus docs and AI skills at [https://drawnui.net](https://drawnui.net).
 
---
MIT | Free to use and customize

---

DrawnUI is built and maintained by Nick Kovalsky, who is available for commercial work: full mobile and desktop app development, custom controls, performance and rendering work, Xamarin → MAUI migrations, and support contracts. Get in touch via [LinkedIn](https://www.linkedin.com/in/nick-kovalsky-92a770174/) or taublast(at)gmail.com.

