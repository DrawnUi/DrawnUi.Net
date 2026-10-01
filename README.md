# DrawnUI for .NET
![NuGet DrawnUi.Net](https://img.shields.io/nuget/v/DrawnUi.Net.svg)
![License](https://img.shields.io/github/license/taublast/DrawnUi.svg)
[![PRs Welcome](https://img.shields.io/badge/PRs-Welcome-brightgreen.svg?style=flat)](https://github.com/taublast/drawnui/blob/master/CONTRIBUTING.md)

👉 [Official Site](https://drawnui.net)   

DrawnUI is a rendering and UI composition engine for .NET, powered by [SkiaSharp](https://github.com/mono/SkiaSharp) with gestures, layouts, effects and animations running with hardware acceleration.

🤩 [Fiddle in browser](https://drawfiddle.com) 👈

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
🤩 [Fiddle](https://drawfiddle.com)   
⛹️ [Pong in pure WASM](https://pong.appomobi.com/)

## What's New 1.10.6.22

  * Japanese and Chinese text wraps: `WordWrap` breaks between characters and keeps closing punctuation and small kana off the start of a line, so a translated sentence no longer runs past the edge on one line. A word wider than the line, like a long URL, now breaks by characters too.
  * A symbol or emoji inside ordinary text shows up: a `SkiaLabel` draws each glyph its font does not have with the first `FontFamilyFallback` font that has it, and the rest of the text keeps its font. `FontFamilyFallback` can now list several fonts, like `"FontSymbols, FontEmoji"`. Before, such a glyph was dropped unless it had its own span.
  * Markdown in `SkiaRichLabel` understands `~~strikethrough~~`.
  * Image preloading has priorities: `PreloadImages(urls, LoadPriority.Low)` waits behind the images on screen. Network images load a few at a time (`MaxParallelLoads`), and `RunningCount` / `QueuedCount` show the line. `RemoveFromCache` drops one image. Same on every head.
  * `SkiaCarousel.ScrollTo(index, animate)` moves to a slide, and with `animate: false` it jumps there at once.
  * `SkiaLottie` and `SkiaSprite` tell you when their file is loaded (`Success`) or could not be (`Error`), like `SkiaGif`.
  * `LastCompositeRecord` shows what an `ImageComposite` cache redrew last time: only the children that changed, or everything.
  * WPF, OpenTK, WebAssembly and Blazor: changing `Rotation` at runtime redraws the control. It used to wait for something else to redraw.

 ### Previously

  * **Your app works without a mouse.** On MAUI Windows, WPF and Blazor, people can now use a drawn app from the keyboard the way they use native apps:
    * Tab and Shift+Tab move from control to control in reading order, and a focus ring shows where you are. It appears only once you press a key, never after a click, and it stays on the control while it scrolls. Escape leaves the drawn controls.
    * Enter or Space presses a button and flips a switch or a checkbox. Sliders move with the arrow keys, PageUp / PageDown and Home / End.
    * Text fields are Tab stops: Tab into a `SkiaEditor` and you can type at once. Tab again moves on, and Enter presses the next button right away.
    * Navigate inside lists with arrows. Give a list, grid or toolbar a role (`Aria.RoleList`, `RoleGrid`, `RoleToolbar`...) and it becomes a single Tab stop whose items you walk with the arrow keys, Home / End and PageUp / PageDown. This works in a recycled list of 100 000 rows too: rows that do not exist yet are scrolled in when you reach them.
    * Screen readers and the focus ring see each control where it really is on screen: inside cached and scrolled content, and moved, rotated or scaled with transforms. Rows a virtualized list is not drawing leave the accessibility tree.
    * Your own controls can take keys by overriding `OnAccessibilityKey`.
  * Apps with several canvases: each canvas now clears its own focus when nothing takes it (on Android and iOS this closes the soft keyboard). It used to be the first canvas that ever had focus.
  * Touchpads scroll smoothly on MAUI Windows, WPF, OpenTK, Blazor and WebAssembly. A short swipe used to jump 10-15 rows, because every small event a touchpad sends counted as a full mouse-wheel notch. Now each event scrolls its share of a notch; a mouse wheel still moves one line a notch, and a fast spin still adds up.
  * Blazor: the mouse wheel scrolls the drawn scroll under the mouse. It used to scroll the one you clicked last, and nothing before your first click.
  * An open `SkiaDrawer` with `AutoClose` no longer closes when the mouse only moves over the area outside its panel; a click there still closes it.
  * Auto-hiding scroll bars will now appear while the mouse is over the scroll or the keyboard is inside it (`ShowScrollBarsOnHover`, on by default; `KeepScrollBarsVisible` keeps them up). New `IsPointerOver` is true for every control under the mouse, not just the one holding hover.
  * **Potentially breaking:** templated layouts default to `MeasureItemsStrategy="MeasureAll"`, correct for rows of any height. Lists whose rows are really all the same height set `MeasureFirst` explicitly, which now measures only the first row as documented.
  * Fluent `.Initialize(me => ...)` runs once when the control gets its parent, so it also runs for controls created invisible or outside the viewport.
  * Changing `ControlStyle` at runtime rebuilds the control's default look, so platform styles can be switched live.
  * `SkiaScrollBar.IsDraggable` gives desktop scroll bars you can drag or click on the track; `HideDurationSecs` sets how they fade out.
 
---
MIT | Free to use and customize

---

DrawnUI is built and maintained by Nick Kovalsky, who is available for commercial work: full mobile and desktop app development, custom controls, performance and rendering work, Xamarin → MAUI migrations, and support contracts. Get in touch via [LinkedIn](https://www.linkedin.com/in/nick-kovalsky-92a770174/) or taublast(at)gmail.com.

