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

## What's New 1.10.8.13

  * **Stacks fit their content.** A `SkiaStack` without a set width (`HorizontalOptions` Center, Start or End) is now as wide as its widest child, Fill children included, up to its `MaximumWidthRequest`. Cards and labels inside get their full text width instead of the width of the narrowest sibling, and wrapped text keeps the spacing below it. The same rule applies in DrawnUI for React and Rust.
  * **Faster layout.** Stacks of cards and labels lay out about twice as fast: labels reuse their text layout when their width allows it, and stacks no longer measure their children twice. In our tests a page of cards went from 1.04 ms to 0.53 ms per layout.
  * **Right-to-left text.** Arabic, Hebrew and the other right-to-left scripts are shaped in every label, and a line that mixes directions reads in the right order: in "Hi مرحبا 123" the digits stay with the Arabic. In `SkiaEditor` the caret, taps, selection and the Left / Right keys follow the text on screen, as in DrawnUI for React and Rust. A right-to-left paragraph starts at the right edge of its label: `HorizontalTextAlignment` Start and End follow the reading direction of each paragraph.
  * **Same text width everywhere.** Text is measured with exact widths, so a label wraps the same way on every platform and in DrawnUI for React and Rust.
  * **Letters keep their edges.** The hook of a "j" and the slant of italic letters at the start or end of a line are drawn whole in every label, cached or not.
  * **Masonry layout.** `SkiaMasonry` (`LayoutType.Masonry`) lays items of different heights in columns without gaps: every item goes to the bottom of the shortest column, like a photo feed. Set `Split` for the number of columns. It works with children and with `ItemsSource`: recycled cells, `LoadMore` pages that measure only the new items, items that grow after they are shown (an image arrives), `ScrollToIndex`, taps, and keyboard arrows that move to the tile on screen that way. See [Layouts](https://drawnui.net/articles/layouts.html).
  * **Long lists scroll smoothly all the way.** Templated lists of any length follow the mouse wheel and the touchpad row by row, in columns, grids (`Split`) and `SkiaWrap` grids, down and back up. A cell that changes size after it is shown, any number of times, moves the cells below it.
  * **Horizontal lists and wraps.** A templated `SkiaRow` reports the items really on screen in `FirstVisibleIndex` / `LastVisibleIndex` and lays out with every `MeasureItemsStrategy`, `MeasureVisible` included. A templated `SkiaWrap` inside a scroll fires `LoadMore` at its end and arrives at `ScrollToIndex`.
  * **Steady animations and background caches.** A GIF or Lottie playing while a list flings keeps its speed and the fling keeps its own, and `ImageDoubleBuffered` caches render in the background without touching the frame being drawn.
  * **One slide per swipe.** A `SkiaCarousel` swipe lands on the next or the previous slide, however far the finger drags and however fast it flicks; `ScrollTo` and `SelectedIndex` still go anywhere.
  * **Images load after a fast fling.** A page closed while its list still flings fast leaves image loading running for the rest of the app.
  * **Caret at once.** An editor focused from code shows its caret at once, at the end of its text.
  * **Emoji inside text.** A span with `AutoFindFont` takes the width of the font it draws with, so an emoji run sits in its own room, also inside a scroll.
  * **Italic spacing.** Text after an italic span keeps normal word spacing, and an italic letter at the end of a line keeps room for its slant.
  * **Screen readers on every AndroidX version.** On Android, the screen-reader bridge works with every `Xamarin.AndroidX.Core` version an app resolves, including 1.19.
  * **Leaner Android package.** On Android, `DrawnUi.Maui` no longer brings the `AppoMobi.Maui.Native.Android` package; an app that uses its types references it directly.

### Previously

  * **3D depth and glass:** `TranslationZ` moves a control toward you or away from you (closer is bigger), also when it is not turned, and with `RotationX` / `RotationY` it turns cards and panels in 3D with perspective. Cached content stays smooth in 3D and under any rotated, scaled or skewed parent, and taps still reach the right control. A `SkiaBackdrop` in a scaled, moved, rotated or 3D-turned parent shows exactly what is behind it, and the log tells you once when it sits in a picture-cached `SkiaShape` (set `UseCache = None` on that shape for live glass). **Changed behavior** in 1.10.7.12: before, `TranslationZ` only changed the strength of the perspective.
  * **iOS and Mac Catalyst:** pages and popups with a `Canvas` close cleanly ([#361](https://github.com/DrawnUi/DrawnUi.Net/issues/361)); a finger tap on iOS 26 is a plain tap; native views inside a `SkiaMauiElement` (a MAUI `Button`, a `TapGestureRecognizer`, any `UIView`) complete their taps ([#374](https://github.com/DrawnUi/DrawnUi.Net/issues/374)). On Mac Catalyst an app that sets a desktop window size and declares a scene manifest becomes active smoothly. On every platform a `SkiaMauiElement` that starts hidden shows its native view when it becomes visible ([#372](https://github.com/DrawnUi/DrawnUi.Net/issues/372)).
  * **Scrolls, drawers, modals and carousels:** a scroll that started panning keeps the finger until it lifts, also outside its bounds, and a control behind it never takes the gesture over. A second swipe on a moving `SkiaShaderCarousel` moves one more slide and `Stopped` comes once, when it really stops; after a tap on a `SkiaCarousel`, `IsUserFocused` clears when the finger lifts. A scroll inside a drawer keeps its gesture, a short drag held still before lifting goes back, a drawer opened in the same moment it becomes visible stays open, and a drawer tapped while it moves finishes its move instead of stopping halfway. `SnapVelocityThreshold` sets how fast a release must be to count as a flick, for drawers and carousels. `PopModalAsync` closes any modal in the stack, also one hidden under another modal, and modals hidden under a modal that froze the background come back when it closes. A modal swiped away leaves the stack when the finger lifts, and the new `ModalRemoved` event comes when a modal is fully gone.
  * **Faster:** animated shader effects and uniform changes keep the cache of the control under them (`ShouldInvalidateParentCache = true` rebuilds it on every change), `UseCache = GPU` uses the GPU on every head, WebAssembly and OpenTK included, drawing a frame creates far fewer objects (69.6 KB down to 12.9 KB per frame while scrolling uncached cards), so the garbage collector runs less often; a control that keeps updating behind another screen (a camera preview under a pushed page) leaves the canvas idle, iOS frames show on the very next refresh, controls measure once when properties change in bursts, an `ImageComposite` cache redraws only the area of a deep change, and `SafeAction` runs on time under a hidden parent.
  * **WPF, OpenTK and WebAssembly:** a size or `Padding` change made while a control's cache is drawn in the background is applied before the next frame, as on MAUI, so a `SkiaSlider` track reaches its thumb.
  * **Lists:** `ItemsSourceChangesApplied` comes after every change (add, remove, move, every LoadMore page). Templated grids (`Split`) add a LoadMore page without rebuilding the cells they have, and `MeasureVisible` grids place their rows right after the first screen. Drag to reorder keeps every row drawn in `MeasureAll` lists.
  * **Editor:** the caret moves with each typed character in the same frame, also when the keyboard moves it (autocorrect, an input method); number keyboards type on Android, iOS number keyboards get a Done button, the iOS keyboard closes when you leave the page, the caret stays after what you type when the app rewrites the text, and only one editor shows a caret when several are on screen, also on iOS with fields in different canvases, where the keyboard now stays open while you switch fields.
  * **Accessibility on every platform:** TalkBack (Android), VoiceOver (iOS, Mac Catalyst), Orca (Linux) and browser screen readers (WebAssembly) read drawn controls, next to Narrator and Blazor. Tab, a focus ring, Enter / Space and the arrow keys work on Mac Catalyst and OpenTK too. Guide: [Accessibility](https://drawnui.net/articles/advanced/accessibility.html).
  * **Android draws with Vulkan:** hardware-accelerated canvases use Vulkan where available, about 11% less CPU per frame than OpenGL on a Mali-G57 phone, and fall back to OpenGL by themselves. `UseVulkan = false` keeps OpenGL.
  * **Text:** a label in a star column next to an `Auto` column wraps and its row grows. Text with `CharacterSpacing` stays inside its label when it is cut to fit, and no text is cut at in-between screen scales (a margin of 18 at scale 1.25). Japanese and Chinese text wraps properly, symbols and emoji inside normal text use `FontFamilyFallback` (one font or a list), people can select and copy a label's text (`AccessibilityTextSelectable`), and `AutoSize = FitHorizontal` grows back when there is room. On .NET MAUI Windows lines have the same height as on the other platforms, so all-caps text sits centered.
  * **Mouse, touchpad and keys:** touchpad scrolling follows your fingers and sideways swipes scroll horizontal lists on MAUI Windows, WPF and OpenTK. Mac Catalyst mouse clicks arrive as mouse clicks (add `UIApplicationSupportsIndirectInputEvents` = `true` to `Info.plist`). A right click or the Menu key reaches `SkiaControl.ContextMenu` on MAUI Windows and OpenTK. Hover: `ReceivesHover`, `HoverChanged` and `.OnHovered((me, on) => ...)` let any control take mouse hover, and every hovered control under the mouse stays lit (a card and the button inside it). Hover waits while a list scrolls and is checked again when it stops.
  * **Changed behavior:** a shader effect on a `SkiaShape` / `SkiaLabel` works on the control's own content (`UseCache = None` on the control for a see-through effect); `RespondsToGestures = false` switches input off on every interactive control (`SkiaButton.IsDisabled` is obsolete), and on .NET MAUI `IsEnabled = false` switches a drawn control and everything inside it off ([#368](https://github.com/DrawnUi/DrawnUi.Net/issues/368)); in a `SkiaWrap` a `Fill` child without `WidthRequest` takes a whole line; `SkiaStack` and `SkiaRow` honor `ZIndex`; `LockChildrenGestures` works on layouts; `SpeedRatio` means the real speed (0.5 is half); on Android with `UseDesktopKeyboard` text fields keep their keys; templated layouts default to `MeasureItemsStrategy="MeasureAll"` (set `MeasureFirst` for rows of one height).

---
MIT | Free to use and customize

---

DrawnUI is built and maintained by Nick Kovalsky, who is available for commercial work: full mobile and desktop app development, custom controls, performance and rendering work, Xamarin → MAUI migrations, and support contracts. Get in touch via [LinkedIn](https://www.linkedin.com/in/nick-kovalsky-92a770174/) or taublast(at)gmail.com.

