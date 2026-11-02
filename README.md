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

## MIT, everywhere

DrawnUI is MIT licensed in every language and on every platform: desktop, mobile, web and embedded devices. Ship it in commercial products with no attribution badge, no about screen and no per-device royalty.

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

Please star ⭐ if you like it!

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

## What's New 1.10.8.16

  * Tooltips: hover over any control and a short hint appears. It stays right by its control, and long text wraps nicely.
  * Icon buttons can show their accessibility name as a tooltip, so one text serves both mouse users and screen readers.
  * Menus: right-click menus, dropdown lists and desktop menu bars, like in any desktop app.
  * On Windows, tooltips and menus can go past the edge of the app window, just like native ones.
  * Menus work from the keyboard too: open them with Enter or Alt plus a letter, move with the arrows, close with Escape.
  * On Mac: right click, two-finger click and Control-click open your context menus.
  * On Mac: two-finger trackpad scrolling glides on after you lift your fingers, like in native Mac apps.
  * A fresh default look: a blue accent color, and a slightly larger switch.
  * Underlined and crossed-out text takes the color of its letters.
  * The Hello demo apps have a new Tooltips & Menus screen, and their home page cards light up under the mouse.

### Previously

  * **Layout:** a `SkiaStack` without a set width (`HorizontalOptions` Center, Start or End) is as wide as its widest child, Fill children included, up to its `MaximumWidthRequest`, as in DrawnUI for React and Rust. Stacks of cards and labels lay out about twice as fast. `SkiaMasonry` (`LayoutType.Masonry`) lays items of different heights in columns without gaps, like a photo feed, with children or `ItemsSource`, recycled cells, `LoadMore`, `ScrollToIndex`, taps and keyboard arrows. See [Layouts](https://drawnui.net/articles/layouts.html).
  * **Text:** Arabic, Hebrew and the other right-to-left scripts are shaped in every label, mixed directions read in the right order, a right-to-left paragraph starts at the right edge, and in `SkiaEditor` the caret, taps, selection and arrow keys follow the text on screen. Text is measured with exact widths, so a label wraps the same way on every platform and engine. The hook of a "j" and italic slants are drawn whole, emoji runs with `AutoFindFont` get their own room, and text after an italic span keeps normal spacing. Japanese and Chinese text wraps properly, symbols and emoji use `FontFamilyFallback`, people can select and copy a label's text (`AccessibilityTextSelectable`), and no text is cut at in-between screen scales.
  * **Lists:** templated lists of any length scroll smoothly with the wheel and the touchpad, in columns, grids (`Split`) and `SkiaWrap` grids, and a cell that changes size moves the cells below it. Recycled lists create new cells in the background while they scroll. A templated `SkiaRow` reports the items on screen in `FirstVisibleIndex` / `LastVisibleIndex`, a templated `SkiaWrap` fires `LoadMore` and reaches `ScrollToIndex`, a `SkiaScroll` with a `Header` places its `Footer` right after the content, and images keep loading after a page closes mid-fling. `ItemsSourceChangesApplied` comes after every change, and drag to reorder keeps every row drawn.
  * **Preview: DrawnUI Designer Agent.** Debug builds of your app carry the DrawnUI Designer agent: the Designer can attach to the running app, show its screens and controls, and edit them live. Release builds do not reference it. Pages built in code (`BasePageReloadable`) redraw on every hot reload, also under `dotnet watch`.
  * **Android:** hardware-accelerated canvases draw with Vulkan where available (about 11% less CPU per frame than OpenGL on a Mali-G57 phone) and fall back to OpenGL by themselves; `UseVulkan = false` keeps OpenGL. A Vulkan canvas can draw Android hardware buffers straight, for libraries such as the camera. The screen-reader bridge works with every `Xamarin.AndroidX.Core` version, and `DrawnUi.Maui` no longer brings the `AppoMobi.Maui.Native.Android` package.
  * **3D depth and glass:** `TranslationZ` moves a control toward you or away from you (closer is bigger), and with `RotationX` / `RotationY` it turns cards and panels in 3D with perspective. Cached content stays smooth under any rotated, scaled or skewed parent, and taps still reach the right control. A `SkiaBackdrop` in a transformed parent shows exactly what is behind it (set `UseCache = None` on a picture-cached `SkiaShape` around it for live glass).
  * **iOS and Mac Catalyst:** pages and popups with a `Canvas` close cleanly ([#361](https://github.com/DrawnUi/DrawnUi.Net/issues/361)), a finger tap on iOS 26 is a plain tap, and native views inside a `SkiaMauiElement` complete their taps ([#374](https://github.com/DrawnUi/DrawnUi.Net/issues/374)). A `SkiaMauiElement` that starts hidden shows its native view when it becomes visible ([#372](https://github.com/DrawnUi/DrawnUi.Net/issues/372)).
  * **Scrolls, drawers, modals and carousels:** a scroll that started panning keeps the finger until it lifts, also outside its bounds. A `SkiaCarousel` swipe lands on the next or the previous slide, and a moving `SkiaShaderCarousel` takes a second swipe. Drawers keep the gestures of scrolls inside them and finish their moves, `SnapVelocityThreshold` sets how fast a flick is, `PopModalAsync` closes any modal in the stack, and the `ModalRemoved` event comes when a modal is fully gone.
  * **Faster:** animated shader effects keep the cache of the control under them, `UseCache = GPU` uses the GPU on every head, WebAssembly and OpenTK included, and drawing a frame creates far fewer objects (69.6 KB down to 12.9 KB per frame while scrolling uncached cards). GIFs and Lotties keep their speed while a list flings, `ImageDoubleBuffered` caches render in the background, a control updating behind another screen leaves the canvas idle, a canvas hidden in the view tree starts drawing again by itself when it shows, and iOS frames show on the very next refresh.
  * **Editor:** the caret moves with each typed character in the same frame, an editor focused from code shows its caret at once, number keyboards type on Android and get a Done button on iOS, the iOS keyboard closes when you leave the page, and only one editor shows a caret when several are on screen.
  * **Accessibility, mouse and keys:** TalkBack (Android), VoiceOver (iOS, Mac Catalyst), Orca (Linux) and browser screen readers read drawn controls, next to Narrator and Blazor, and Tab, a focus ring, Enter / Space and the arrow keys work on every head. Touchpad scrolling follows your fingers on MAUI Windows, WPF and OpenTK, Mac Catalyst mouse clicks arrive as mouse clicks, a right click or the Menu key reaches `SkiaControl.ContextMenu`, and `ReceivesHover` / `.OnHovered(...)` let any control take mouse hover. Guide: [Accessibility](https://drawnui.net/articles/advanced/accessibility.html).
  * **Changed behavior:** a shader effect on a `SkiaShape` / `SkiaLabel` works on the control's own content (`UseCache = None` on the control for a see-through effect); `RespondsToGestures = false` switches input off on every interactive control (`SkiaButton.IsDisabled` is obsolete), and on .NET MAUI `IsEnabled = false` switches a drawn control and everything inside it off ([#368](https://github.com/DrawnUi/DrawnUi.Net/issues/368)); in a `SkiaWrap` a `Fill` child without `WidthRequest` takes a whole line; `SkiaStack` and `SkiaRow` honor `ZIndex`; `LockChildrenGestures` works on layouts; `TranslationZ` moves a control in depth (since 1.10.7.12; before, it only set the strength of the perspective); `SpeedRatio` means the real speed (0.5 is half); on Android with `UseDesktopKeyboard` text fields keep their keys; templated layouts default to `MeasureItemsStrategy="MeasureAll"` (set `MeasureFirst` for rows of one height).

---
MIT | Free to use and customize

---

DrawnUI is built and maintained by Nick Kovalsky, who is available for commercial work: full mobile and desktop app development, custom controls, performance and rendering work, Xamarin → MAUI migrations, and support contracts. Get in touch via [LinkedIn](https://www.linkedin.com/in/nick-kovalsky-92a770174/) or taublast(at)gmail.com.

