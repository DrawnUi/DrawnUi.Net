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

## What's New 1.10.7.15

  * **Frosted glass in zooming and rotating popups.** A `SkiaBackdrop` inside a parent that is scaled, moved, rotated or turned in 3D (a popup that zooms in) copies the screen area it really covers and draws it back exactly there, so the glass lines up with what is behind it; the blur stays the same on screen. Before, it copied its layout rectangle and showed a shifted, wrongly scaled part of the screen.
  * **Backdrops in cached shapes.** A backdrop inside a `SkiaShape` left at its default picture cache (`Operations`) freezes: the picture keeps its first copy of what is behind. DrawnUI now logs this once and names the fix, `UseCache = None` on that shape. See [SkiaBackdrop](https://drawnui.net/articles/controls/layouts.html#skiabackdrop).

### Previously

  * **3D depth:** `TranslationZ` moves a control toward you or away from you (closer is bigger), also when it is not turned, and with `RotationX` / `RotationY` it turns cards and panels in 3D with perspective. Cached content stays smooth in 3D and under any rotated, scaled or skewed parent, and taps still reach the right control. **Changed behavior** in 1.10.7.12: before, `TranslationZ` only changed the strength of the perspective.
  * **iOS and Mac Catalyst:** versions 1.10.7.1 to 1.10.7.4 crashed the app when a page or popup with a `Canvas` closed ([#361](https://github.com/DrawnUi/DrawnUi.Net/issues/361)); on iOS 26 every finger tap was also a mouse right click; native views inside a `SkiaMauiElement` (a MAUI `Button`, a `TapGestureRecognizer`, any `UIView`) got the press but never the release ([#374](https://github.com/DrawnUi/DrawnUi.Net/issues/374)). On Mac Catalyst, an app that sets a desktop window size no longer crashes when it becomes active if its `Info.plist` declares a scene manifest. On every platform a `SkiaMauiElement` that starts hidden shows its native view when it becomes visible ([#372](https://github.com/DrawnUi/DrawnUi.Net/issues/372)).
  * **Scrolls, drawers, modals and carousels:** a scroll that started panning keeps the finger until it lifts, also outside its bounds, so a control behind it no longer takes the gesture over. A second swipe on a moving `SkiaShaderCarousel` moves one more slide and `Stopped` comes once, when it really stops; after a tap on a `SkiaCarousel`, `IsUserFocused` clears when the finger lifts. A drawer no longer takes over the gesture of a scroll inside it, a short drag held still before lifting goes back instead of closing, a drawer opened in the same moment it becomes visible stays open, and a drawer tapped while it moves finishes its move instead of stopping halfway. `SnapVelocityThreshold` sets how fast a release must be to count as a flick, for drawers and carousels. `PopModalAsync` closes any modal in the stack, also one hidden under another modal, and modals hidden under a modal that froze the background come back when it closes. A modal swiped away leaves the stack when the finger lifts, and the new `ModalRemoved` event comes when a modal is fully gone.
  * **Faster:** animated shader effects and uniform changes keep the cache of the control under them (1.10.7.14; `ShouldInvalidateParentCache = true` restores the rebuild), `UseCache = GPU` really uses the GPU on WebAssembly and OpenTK (it made CPU surfaces before 1.10.7.13), drawing a frame creates far fewer objects (69.6 KB down to 12.9 KB per frame while scrolling uncached cards), so the garbage collector runs less often; a control that keeps updating behind another screen (a camera preview under a pushed page) no longer redraws the canvas, iOS frames no longer show one refresh late, controls measure once when properties change in bursts, an `ImageComposite` cache redraws only the area of a deep change, and `SafeAction` runs on time under a hidden parent.
  * **WPF, OpenTK and WebAssembly:** a size or `Padding` change made while a control's cache is drawn in the background is no longer lost, as on MAUI, so a `SkiaSlider` track reaches its thumb.
  * **Lists:** `ItemsSourceChangesApplied` comes after every change (add, remove, move, every LoadMore page). Templated grids (`Split`) add a LoadMore page without rebuilding the cells they have, and `MeasureVisible` grids place their rows right after the first screen. Drag to reorder no longer leaves blank rows in `MeasureAll` lists.
  * **Editor:** the caret moves with each typed character in the same frame, also when the keyboard moves it (autocorrect, an input method); number keyboards type on Android, iOS number keyboards get a Done button, the iOS keyboard closes when you leave the page, the caret stays after what you type when the app rewrites the text, and only one editor shows a caret when several are on screen, also on iOS with fields in different canvases, where the keyboard now stays open while you switch fields.
  * **Accessibility on every platform:** TalkBack (Android), VoiceOver (iOS, Mac Catalyst), Orca (Linux) and browser screen readers (WebAssembly) read drawn controls, next to Narrator and Blazor. Tab, a focus ring, Enter / Space and the arrow keys work on Mac Catalyst and OpenTK too. Guide: [Accessibility](https://drawnui.net/articles/advanced/accessibility.html).
  * **Android draws with Vulkan:** hardware-accelerated canvases use Vulkan where available, about 11% less CPU per frame than OpenGL on a Mali-G57 phone, and fall back to OpenGL by themselves. `UseVulkan = false` keeps OpenGL.
  * **Text:** a label in a star column next to an `Auto` column wraps and its row grows, instead of being cut to one line. Text with `CharacterSpacing` stays inside its label when it is cut to fit, and no text is cut at in-between screen scales (a margin of 18 at scale 1.25). Japanese and Chinese text wraps properly, symbols and emoji inside normal text use `FontFamilyFallback` (one font or a list), people can select and copy a label's text (`AccessibilityTextSelectable`), and `AutoSize = FitHorizontal` grows back when there is room. On .NET MAUI Windows lines have the same height as on the other platforms, so all-caps text no longer sits low.
  * **Mouse, touchpad and keys:** touchpad scrolling follows your fingers and sideways swipes scroll horizontal lists on MAUI Windows, WPF and OpenTK. Mac Catalyst mouse clicks arrive as mouse clicks (add `UIApplicationSupportsIndirectInputEvents` = `true` to `Info.plist`). A right click or the Menu key reaches `SkiaControl.ContextMenu` on MAUI Windows and OpenTK. Hover: `ReceivesHover`, `HoverChanged` and `.OnHovered((me, on) => ...)` let any control take mouse hover, and every hovered control under the mouse stays lit (a card and the button inside it). Hover waits while a list scrolls and is checked again when it stops.
  * **Changed behavior:** a shader effect on a `SkiaShape` / `SkiaLabel` (picture cache) works on the control's own content, not on what is behind it (1.10.7.14; `UseCache = None` on the control for a see-through effect); `RespondsToGestures = false` switches input off on every interactive control (`SkiaButton.IsDisabled` is obsolete), and on .NET MAUI `IsEnabled = false` switches a drawn control and everything inside it off ([#368](https://github.com/DrawnUi/DrawnUi.Net/issues/368)); in a `SkiaWrap` a `Fill` child without `WidthRequest` takes a whole line; `SkiaStack` and `SkiaRow` honor `ZIndex`; `LockChildrenGestures` works on layouts; `SpeedRatio` means the real speed (0.5 is half); on Android with `UseDesktopKeyboard` text fields keep their keys; templated layouts default to `MeasureItemsStrategy="MeasureAll"` (set `MeasureFirst` for rows of one height).

---
MIT | Free to use and customize

---

DrawnUI is built and maintained by Nick Kovalsky, who is available for commercial work: full mobile and desktop app development, custom controls, performance and rendering work, Xamarin → MAUI migrations, and support contracts. Get in touch via [LinkedIn](https://www.linkedin.com/in/nick-kovalsky-92a770174/) or taublast(at)gmail.com.

