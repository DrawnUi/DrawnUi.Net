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

* Create games: `DrawnUi.DrawnUi.Game`, `DrawnUi.Blazor.Game`,`DrawnUi.OpenTk.game`.
* .NET MAUI only: `DrawnUi.MauiGraphics`
* .NET MAUI only: `DrawnUi.DrawnUi.MapsUi`
* .NET MAUI only: `DrawnUi.DrawnUi.Camera` - [Separate repo](https://github.com/taublast/DrawnUi.Maui.Camera).

---

## Resources

👉 [Docs and Samples](https://drawnui.net)   
🤖 [AI skills](https://drawnui.net/llms.txt)   
🤩 [Fiddle](https://fiddle.drawnui.net)   
⛹️ [Pong in pure WASM](https://pong.appomobi.com/)

## What's New 1.10.6.16
  
  * **Behavior change** (healing): fluent `.Initialize(me => ...)` (`ExecuteAfterCreated`) now runs the moment the control gets its parent (added to `Children`/`Content`, or to the canvas for a root), right after its own initializer chain completed. It used to run at the control's first measure, so it never ran for a control created with `IsVisible = false` (nothing measures an invisible child), ran late for virtualized children outside the viewport, and ran again on every forced content re-initialization. It now runs exactly once, whatever the visibility; a control that never gets a parent runs it at its first measure as before. Inside it `Superview` may still be null (the parent gets attached later): for work that needs the live tree use `LayoutIsReady` or the `Initialized` event.
  * `SkiaScrollBar.IsDraggable`: desktop scroll bar behavior, drag the thumb or press the track to jump there (the thumb centers under the pointer). `GrabPadding` widens the hit area of a thin bar. Off by default, the bar stays display-only and every gesture passes through to the content. A grabbed auto-hidden bar shows again immediately.
  * `SkiaScrollBar.HideDurationSecs`: duration of the auto-hide fade-out (was a fixed 250 ms, default unchanged); with `AutoHide` and `HideDelaySecs` it sets how the bar fades after scrolling stops.
  * Fix an auto-hiding `SkiaScrollBar` could stay visible after a scroll ended: the scroll re-evaluates its scrolling state only while drawing and nothing drew after the last animation frame, so the bar never learned scrolling had stopped. A scroll with a bar now draws one more frame when scrolling ends.
  * Fix `SkiaScroll` detached its `ScrollBar` when `Content`, `Header` or `Footer` was set after it: the scroll bar was taken for the old content and removed as a subview. It was still drawn, but without a parent it could never request a redraw of its own, so an auto-hiding bar never faded out on screen.
  * Fix `SkiaScroll` jumping away from the end after an overscroll bounce when it sits in a `SkiaGrid` row with a `ScrollBar`: the bar squashes its thumb while bouncing, and every thumb resize re-measured the scroll and its parents each frame; a provisional grid measure then pulled the offset in by the other rows' height. Scroll bars set on a `SkiaScroll` are now `IsParentIndependent`, their look never re-measures the layout around them (fewer layout passes while bouncing too).
  * Fix `SkiaScroll` moved its offset during a provisional measure: a parent measuring it more than once with different sizes (a grid measures a star row before subtracting its Auto rows) made it clamp against transient bounds, and the offset stayed moved. The clamp for content that shrank past the offset now runs on draw, against the final bounds.
  * Fix changing `ControlStyle` after a control was measured had no effect: `SkiaButton`, `SkiaSwitch`, `SkiaCheckbox`, `SkiaRadioButton`, `SkiaSlider`, `SkiaProgress`, `SkiaPicker`, `SkiaWheelPicker` all guard their default content with "create only when empty", and the sizes pinned by the first style (`WidthRequest`/`HeightRequest`, style defaults) were never released. New `SkiaControl.RebuildDefaultContent()` drops the content the control built itself (user-provided children stay), un-pins exactly the properties the previous style set (user-set values stay), and lets the next measure build the new style; `ControlStyle` now calls it, the `SkiaSlider.EnableRange` rebuild uses it too. The `Initialized` lifecycle event fires once, not again on a rebuild. Non-MAUI heads (Blazor, WPF, OpenTK, headless) got `BindableObject.ClearValue` for this.
  * Fix `SkiaScroll` still reacting to gestures when `Orientation` is set to `ScrollOrientation.Neither` ([#347](https://github.com/taublast/DrawnUi/issues/347)): a drag rubber-banded the viewport and sprang back on release, and the mouse wheel was consumed without scrolling anything, so it never reached a parent scroll. Panning, fling and wheel are now all skipped for `Neither`.
  * Fix Android `Canvas` staying blank after its kept-alive native view was moved to another window, e.g. cached content shown again inside a new popup/dialog: a canvas detected as hidden in the first window never woke up in the next one, because its visibility listeners stayed bound to the previous window's `ViewTreeObserver`. They are now re-registered, with a visibility re-check, every time the view attaches to a window.
  * Fix memory leak: every time a kept-alive `Canvas` lost and regained its handler (a cached page pushed again after a pop) its internal Skia view leaked together with its native views. The destroyed view stayed as `Content`, MAUI mapped it again before the replacement was created, and it subscribed itself back to the static `Super.OrientationChanged` event. Replaced views are now disconnected and disposed, and a disposed view never re-subscribes.
  * Fix Android memory leak: the canvas `ViewTreeObserver` listeners (layout and pre-draw) were removed after the view had left its window, where Android returns a throw-away observer, so the window kept every listener for the app lifetime. They are now removed from the observer they were registered with, on window detach.
  * Fix Android `Super.SetWhiteTextStatusBar()` / `SetBlackTextStatusBar()` had no effect under MAUI 10: MAUI now sets the status bar appearance through the insets controller when the window is created (edge-to-edge), and the helpers were only toggling the legacy `SystemUiVisibility` flag. They now use the insets controller too.
  * Fix `DrawnUi.Maui.MapsUi` on Android failing the 16 KB page-size check (system "Android App Compatibility" dialog on Android 16+): the desktop `SQLitePCLRaw.lib.e_sqlite3` and `SkiaSharp.NativeAssets.Linux` packages were referenced for every target, so the 4 KB aligned linux `libe_sqlite3.so` was packed into the APK over the 16 KB aligned Android one. They are now referenced for desktop targets only.
  * Fix center alignment drifting half a pixel right/down: when the free space around a centered child was an odd number of pixels, no whole-pixel offset could center it and rounding always pushed it right/down (e.g. the accent dot inside a `SkiaSlider` thumb). The child now takes that odd pixel, so both gaps are equal.
  * Fix `SkiaEditor` (Windows, Android, iOS) putting typed characters in the wrong place during fast typing: the caret position read from the native text control was written back to it a moment later, after more keys had moved it, so the caret jumped back one character ("drawncamera.com" came out as "drawncameracom."). A caret position that came from the native control is no longer written back to it.
  
 ### Previously

  * Fix `SkiaViewSwitcher` traced an `ArgumentOutOfRangeException` on every root-view lookup while `SelectedIndex` was set before its children existed (the usual initializer order); the lookups now answer null quietly.
  * Fix `SkiaShell` unfrozen modal push no longer holds the navigation lock forever
  * Fix `SkiaScroll.ScrollToIndex` on a Split layout (items grid): the index is an item index and lands on that item's row; it was read as a row index, so any item past the first rows made the order silently invalid.
  * Fix `SkiaScroll` LoadMore distances (`LoadMoreOffset`, `LoadMoreTopOffset`) are points and were multiplied by the rendering scale, so on a 3x screen the bottom trigger fired at any position once re-armed, e.g. at a top overscroll right after an append. The viewport init also no longer snaps the offset to 0 while a pan, fling, bounce or refresh runs (one-frame jag when an append re-measured a Split grid mid-bounce).
  * Fix images loading: sync local loads decode inline; cancelled loads release parked requests  
   * Fix `SkiaDrawer` was not removing its previous content when `Content` was replaced or set to null: the old child stayed in `Views` and was disposed together with the drawer, so a kept modal content came back disposed on its next presentation (blur, no popup).
  * Center alignment: an overflowing box is moved back inside its parent instead of being truncated.
  * Fix `SkiaCarousel` to block gestures for nor current slides
  * Fix wheel picker to work properly when hosted inside a scroll container
  * Fix `SkiaEditor` was leeking native entry display on latest iOS version.
  * Fix `SkiaScroll` margins were applied to limit scrolling bounds
  * Fix autosized `SkiaScroll` not expanding horizontally after content changed
  * Fix centering text inside `SkiaEditor`
  * Fix `SkiaScroll` margins were applied to limit scrolling bounds
  * Fix autosized `SkiaScroll` not expanding horizontally after content changed
  * Updated docs and skills at [https://drawnui.net](https://drawnui.net)
 
---
MIT | Free to use and customize

---

DrawnUI is built and maintained by Nick Kovalsky, who is available for commercial work: full mobile and desktop app development, custom controls, performance and rendering work, Xamarin → MAUI migrations, and support contracts. Get in touch via [LinkedIn](https://www.linkedin.com/in/nick-kovalsky-92a770174/) or taublast(at)gmail.com.

