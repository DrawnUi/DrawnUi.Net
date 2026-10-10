# DrawnUI for .NET
![NuGet DrawnUi.Net](https://img.shields.io/nuget/v/DrawnUi.Net.svg)
![License](https://img.shields.io/github/license/DrawnUi/DrawnUi.Net.svg)
[![PRs Welcome](https://img.shields.io/badge/PRs-Welcome-brightgreen.svg?style=flat)](https://github.com/DrawnUi/DrawnUi.Net/blob/main/CONTRIBUTING.md)

👉 [Official Site](https://drawnui.net)   

DrawnUI is a rendering and UI composition engine for .NET, powered by [SkiaSharp](https://github.com/mono/SkiaSharp) with gestures, layouts, effects and animations running with hardware acceleration.

🤩 [Fiddle in browser](https://drawfiddle.com) 👈

Supported hosts:

* `DrawnUi.Maui` - Android, iOS, MacCatalyst, and Windows.
* `DrawnUi.Blazor.Wasm` - browser WebAssembly rendering.
* `DrawnUi.Blazor.Server` - server-backed DrawnUI surfaces served by Blazor Server.
* `DrawnUi.Web` - pure browser WebAssembly, no Blazor required.
* `DrawnUi.OpenTk` - Windows and Linux desktops.
* `DrawnUi.Wpf` - drawn controls inside WPF windows.
* `DrawnUi.Net` - platform-agnostic console/server rendering scenarios.

## React?

DrawnUI for React just appeared as a standalone DrawnUI engine in TypeScript, running on [CanvasKit](https://skia.org/docs/user/modules/canvaskit/) (Skia compiled to WebAssembly) in the browser. It tends to use same API as the .NET version. 
Under active development, more info [on our site](https://drawnui.net/articles/react).

## How About Rust?

DrawnUI for Rust is the same engine in Rust, drawing with Skia on Windows, macOS, Linux, iOS, Android and in the browser. Same controls and rules as the .NET version, one crate to add (`drawnui` on crates.io), in preview. Try the demo at [hellorust.drawnui.net](https://hellorust.drawnui.net), more info [on our site](https://drawnui.net/articles/rust).

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
* __Accessibility__: screen readers (Narrator, TalkBack, VoiceOver, Orca, browser readers) and keyboard navigation on every platform
* __Navigate__ on the canvas with shell-like techniques 

😎 [Blazor sample in browser](https://drawnui.net/sandbox/) 👈

## Addons

* Create games: `DrawnUi.Maui.Game`, `DrawnUi.Blazor.Game`, `DrawnUi.Web.Game`, `DrawnUi.OpenTk.Game`, `DrawnUi.Wpf.Game`.
* .NET MAUI only: `DrawnUi.MauiGraphics`
* .NET MAUI only: `DrawnUi.Maui.MapsUi`
* .NET MAUI only: `DrawnUi.Maui.Camera` - [Separate repo](https://github.com/taublast/DrawnUi.Maui.Camera).

---

## Resources

👉 [Docs and Samples](https://drawnui.net)   
🤖 [AI skills](https://drawnui.net/llms.txt)   
🤩 [Fiddle](https://drawfiddle.com)   
⛹️ [Pong in pure WASM](https://pong.appomobi.com/)

## What's New 1.10.7.6

  * **Fix: on iOS 26 every tap was also a mouse right click.** The gestures library's right-click recognizer fired on every plain finger tap, so each tap also sent `Pointer` events typed `Mouse`; a mouse context menu or hover effect could react to every tap. With AppoMobi.Gestures 3.11.6 a finger tap is only a tap, and hover comes only from a mouse or an Apple Pencil.

### Previously

  * **1.10.7.5, hotfix for iOS and Mac Catalyst:** versions 1.10.7.1 to 1.10.7.4 crashed the app when a page or popup with a `Canvas` closed ([#361](https://github.com/DrawnUi/DrawnUi.Net/issues/361)). Update if you ship to Apple platforms.
  * **Drawers:** a drawer no longer takes over the gesture of a scroll inside it, and a short drag held still before lifting goes back instead of closing. `SnapVelocityThreshold` sets how fast a release must be to count as a flick, for drawers and carousels.
  * **Text in grids:** a label in a star column next to an `Auto` column wraps and its row grows, instead of being cut to one line. Text with `CharacterSpacing` stays inside its label when it is cut to fit.
  * **Lists:** `ItemsSourceChangesApplied` comes after every change (add, remove, move, every LoadMore page). Templated grids (`Split`) add a LoadMore page without rebuilding the cells they have, and `MeasureVisible` grids place their rows right after the first screen.
  * **Hover for lists:** `ReceivesHover`, `HoverChanged` and `.OnHovered((me, on) => ...)` let any control take mouse hover, and every hovered control under the mouse stays lit (a card and the button inside it). Hover waits while a list scrolls and is checked again when it stops.
  * **Faster:** a control that keeps updating behind another screen (a camera preview under a pushed page) no longer redraws the canvas, iOS frames no longer show one refresh late, controls measure once when properties change in bursts, and an `ImageComposite` cache redraws only the area of a deep change.
  * **Editor:** number keyboards type on Android, iOS number keyboards get a Done button, the iOS keyboard closes when you leave the page, the caret stays after what you type when the app rewrites the text, and only one editor shows a caret when several are on screen.
  * **Accessibility on every platform:** TalkBack (Android), VoiceOver (iOS, Mac Catalyst), Orca (Linux) and browser screen readers (WebAssembly) read drawn controls, next to Narrator and Blazor. Tab, a focus ring, Enter / Space and the arrow keys work on Mac Catalyst and OpenTK too. Guide: [Accessibility](https://drawnui.net/articles/advanced/accessibility.html).
  * **Android draws with Vulkan:** hardware-accelerated canvases use Vulkan where available, about 11% less CPU per frame than OpenGL on a Mali-G57 phone, and fall back to OpenGL by themselves. `UseVulkan = false` keeps OpenGL.
  * **Text:** Japanese and Chinese text wraps properly, symbols and emoji inside normal text use `FontFamilyFallback` (one font or a list), people can select and copy a label's text (`AccessibilityTextSelectable`), and `AutoSize = FitHorizontal` grows back when there is room.
  * **Mouse, touchpad and keys:** touchpad scrolling follows your fingers and sideways swipes scroll horizontal lists on MAUI Windows, WPF and OpenTK. Mac Catalyst mouse clicks arrive as mouse clicks (add `UIApplicationSupportsIndirectInputEvents` = `true` to `Info.plist`). A right click or the Menu key reaches `SkiaControl.ContextMenu` on MAUI Windows and OpenTK.
  * **Changed behavior:** in a `SkiaWrap` a `Fill` child without `WidthRequest` takes a whole line; `SkiaStack` and `SkiaRow` honor `ZIndex`; `LockChildrenGestures` works on layouts; `SpeedRatio` means the real speed (0.5 is half); on Android with `UseDesktopKeyboard` text fields keep their keys; templated layouts default to `MeasureItemsStrategy="MeasureAll"` (set `MeasureFirst` for rows of one height).
 
---
MIT | Free to use and customize

---

DrawnUI is built and maintained by Nick Kovalsky, who is available for commercial work: full mobile and desktop app development, custom controls, performance and rendering work, Xamarin → MAUI migrations, and support contracts. Get in touch via [LinkedIn](https://www.linkedin.com/in/nick-kovalsky-92a770174/) or taublast(at)gmail.com.

