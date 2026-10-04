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

  * **Rendering**
    * Android: hardware-accelerated canvases (`RenderingMode = Accelerated`) draw with Vulkan. On a phone with a Mali-G57 GPU this takes about 11% less CPU per frame than OpenGL, at the same frame rate. Devices without Vulkan 1.1 (or older than Android 7) keep OpenGL, and when Vulkan fails to start on a device, DrawnUI switches the canvas to OpenGL by itself. To always use OpenGL, set `UseVulkan = false` in the settings you pass to `UseDrawnUi`.
  * **Text**
    * Japanese and Chinese text wraps properly. Lines break between characters, and closing punctuation and small kana never start a line. A very long word, like a URL, also breaks when it is wider than the line.
    * Symbols and emoji inside normal text show up. A `SkiaLabel` draws each character its font is missing with the first `FontFamilyFallback` font that has it, and the rest of the text keeps its own font. `FontFamilyFallback` can list several fonts, for example `"FontSymbols, FontEmoji"`.
    * `SkiaRichLabel` markdown supports `~~strikethrough~~`.
    * People can select and copy text: turn it on with `AccessibilityTextSelectable` on a `SkiaLabel`. With a mouse, drag or double-click, then press Ctrl+C (Cmd+C on Mac). With a finger, long press, then tap Copy.
    * MAUI Mac Catalyst: mouse clicks reach controls as mouse clicks, so a drag over selectable text selects it instead of scrolling, and Cmd+C / Cmd+A reach the app (the Edit menu used to keep them). Add `UIApplicationSupportsIndirectInputEvents` = `true` to `Platforms/MacCatalyst/Info.plist`: without it macOS hands clicks over as finger touches. Uses AppoMobi gestures 3.11.5.
    * **Changed:** Android with `UseDesktopKeyboard`: text fields get their keys. Before, DrawnUI kept every key from the focused field, so Backspace did nothing (with keyboard suggestions it kept deleting them instead of the text) and Back did not go back. `KeyboardManager` still sees every key; Backspace now arrives as `Backspace` (it came as `Delete`), and the Back button is no longer reported as Backspace.
    * A text field outside DrawnUI keeps its keys on every platform. In the browser, a page `<input>` next to the canvas types spaces and moves its caret on WebAssembly, and on Blazor typing into it no longer also types into a focused drawn `SkiaEditor`. Ctrl+C in a text field no longer copies a drawn label's selection. `KeyboardManager` still reports every key; `KeyboardManager.IsKeyForOtherElement` tells handlers when the key belongs to such a field, so they can ignore it.
  * **Images and animations**
    * Image preloading has priorities: `PreloadImages(urls, LoadPriority.Low)` waits until the images on screen are loaded. Network images load a few at a time (`MaxParallelLoads`), and `RunningCount` / `QueuedCount` show how many are loading and waiting. `RemoveFromCache` removes one image.
    * `SkiaLottie` and `SkiaSprite` raise `Success` when their file is loaded and `Error` when it fails, like `SkiaGif`.
    * `SkiaCarousel.ScrollTo(index, animate)` goes to a slide; with `animate: false` it jumps there at once.
  * **Controls**
    * **Changed:** `LockChildrenGestures` works on layouts. Before, layouts ignored it, so taps reached their children whatever the value. Now `Enabled` and `PassNone` keep every gesture from the children, `PassTap` lets only taps through, and `PassTapAndLongPress` taps and long presses. The layout itself still gets its own `Tapped`, so "lock the children, handle the tap on the card" works. `Enabled` also keeps gestures from controls stacked under the layout, as its description says.
    * Changing `ControlStyle` while the app runs restyles the control fully. Before, a `SkiaButton` lost its caption (it showed "Test", or nothing in Material), `SkiaSwitch` and `SkiaCheckbox` kept the colors of the first style, and a `SkiaProgress` showed an empty track in Material and Material3.
  * **Layout**
    * **Changed:** in a `SkiaWrap`, a child with `HorizontalOptions = Fill` and no `WidthRequest` gets a whole line, as in DrawnUI for React and Rust. After other children it moves to a new line, and the next children start below it. Before, it was squeezed into the space left on the current line. To keep it next to the others, give it a width or use a `SkiaRow`.
    * In a `SkiaWrap`, a box with a fixed size stays on its line even when its content sticks out of it on purpose (an unclipped child with a negative margin). Before, each such box went to a line of its own, with an empty line above the first one.
    * In a `SkiaWrap`, children whose widths add up to exactly the width of the line share it at every screen scale, for example two cards that are each half the line minus the spacing. Before, the second one usually went to a new line, so a two-column list showed one column on most screens.
    * A `SkiaWrap` with recycled cells from `ItemsSource` (the default) draws its items. Before, it left their space empty.
    * In a `SkiaStack` or `SkiaRow`, a child pulled over the one before it with a negative margin (for example `AddMarginTop` equal to minus its height) is drawn. Before, it was not drawn at all.
    * **Changed:** `SkiaStack` and `SkiaRow` honor `ZIndex`, like `SkiaLayout` with `Type = Absolute`: a child with a higher `ZIndex` is drawn on top of the others, and a tap where children overlap goes to the one on top. Children with the same `ZIndex` keep their order. Before, stacks ignored `ZIndex` and drew in list order.
    * Changing `Children` while the app runs is drawn as the collection says: `Insert` puts the child at its place (it used to be drawn last), replacing a child (`Children[i] = x`) and `Move` work, and `Clear()` empties a collection the app assigned itself, like an `ObservableCollection`. Before, only adding at the end and removing worked.
    * WPF, OpenTK and WebAssembly: `ScrollToIndex` to an item far away in a big recycled list lands on that item. Before, it could stop at the first rows around it (a jump to the middle of 100 000 items showed item 49 937 instead of 50 001), until the next touch or mouse move.
  * **Scrolling with a touchpad or a mouse wheel**
    * Touchpad scrolling follows your fingers on MAUI Windows, WPF and OpenTK. Small touchpad steps move the content at once, and only a mouse-wheel notch glides. Before, every small step started a slow glide, so the content was late and bounced past the end of a swipe.
    * A fast swipe scrolls smoothly. Before, the content could stand still and then jump when wheel events came quickly.
    * A swipe's fling speed is measured with a steady clock, so a change of the computer's time during a swipe cannot change the fling, and lifting the finger no longer allocates memory.
    * WPF and OpenTK: a drag released on Linux (WSLg, X11) flings as far as on Windows. Mouse moves can arrive there in pairs a hundredth of a millisecond apart, which made every fling start at the speed limit, about twice as far. A move's speed is now measured over the last 16 ms.
    * Sideways scrolling works: on MAUI Windows, WPF and OpenTK, a sideways swipe or a tilted wheel scrolls a horizontal `SkiaScroll`, and a vertical list ignores it. Before, a diagonal swipe made a vertical list jump up and down.
    * This needs AppoMobi gestures 3.11.4 or later (`WheelEventArgs.IsHorizontal`); this version references 3.11.5.
    * WPF and OpenTK: dragging content with the mouse and letting go flings it the same distance on every system. Some systems hand the app its mouse moves in bursts (Linux under WSL sends two moves at almost the same moment); there every fling used to start at full speed and went about twice as far as on Windows.
  * **OpenTK**
    * Motion stays smooth where the graphics driver ignores vsync (for example Linux under WSL): `DrawnUiWindow` spaces frames one screen refresh apart itself. Before, a game drew hundreds of frames a second there and moved unevenly. Where vsync works, nothing changes.
    * Apps that redraw only when needed (`UpdateMode.Dynamic`) run at the screen's exact refresh rate, 59.95 Hz screens included. Before, they ran a little slower.
    * New sample: `HelloOpenTk`, the DrawnUI Hello app with its 20 screens on OpenTK, for Windows and Linux (`dev/hello-opentk-linux.ps1` runs it in WSL).
  * **Drawing and caching**
    * WPF, OpenTK, WebAssembly and Blazor: changing `Rotation` at runtime redraws the control. Before, it waited for something else to redraw.
    * A `.WhenPainted` overlay keeps drawing after its control is hidden and shown again. Before, hiding removed it for good, so a page pushed in a MAUI `SkiaShell` lost its overlays.
    * `LastCompositeRecord` shows what an `ImageComposite` cache redrew last time: only the changed children, or everything.
    * `ImageDoubleBuffered` is more reliable. A control that changes all the time still updates on screen, even when its background render takes longer than a frame; before, it kept its old look until the changes stopped. A cell shows its placeholder until its first image is ready, and never over an image it already has; before, the placeholder showed for one frame and then left a hole. Images that were replaced before they were shown go back to the pool at once, and a render that fails is not repeated forever. A GPU-cached control inside an `ImageDoubleBuffered` parent draws directly, because the GPU cannot be used from the background thread.
    * `UseCache = SkiaCacheType.Auto` works like `Image`.
    * With `Super.Multithreaded` on, an `Operations` or `OperationsFull` cache is no longer redrawn on every frame. Before, it was thrown away and drawn again each time.
    * WPF, OpenTK and WebAssembly: an animation started while a frame is being drawn starts at once. Before, it waited for the next touch or mouse move. `DrawnView.RequestNextFrame()` asks for one more frame from anywhere, also from inside a draw.
  * **Keyboard and accessibility**
    * MAUI Mac Catalyst: Tab and Shift+Tab move between the drawn controls with a focus ring, and the arrow keys move inside a group (a list, a toolbar, a grid), as on MAUI Windows and WPF.
    * WPF `SkiaShell`: the page under an opened page is hidden, as on the other heads. Before, Tab and screen readers reached its controls under the new page.
    * WPF and MAUI Windows: screen readers and Tab see the page that is on screen as soon as it settles. Before, they could keep the previous page until something on the canvas moved.
    * MAUI Windows: a right click, Shift+F10 or the Menu key reaches `SkiaControl.ContextMenu`, as on WPF and in the browser.
  * **OpenTK and Linux**
    * Smooth frames where the graphics driver ignores vsync, like Linux under WSL: a `Constant` window notices it in its first second and spaces frames one screen refresh apart, and a `Dynamic` window always does. Before, a game ran at hundreds of frames a second there and movement stuttered.
    * On Windows, frames follow the screen's exact refresh rate. A 59.95 Hz screen used to get 59 frames a second, a little behind the display.
    * `SkiaShell` and C# Hot Reload work on OpenTK too. They were in the WPF package only.
    * New sample `HelloOpenTk`: the DrawnUI Hello app, all 20 screens, on OpenTK for Windows and Linux. `dev/hello-opentk-linux.ps1` builds it for Linux on Windows and runs it in WSL.
  * **Stability**
    * Closing a canvas while one of its controls is still being rendered in the background no longer crashes: the canvas waits for that render to finish first.
    * WPF and OpenTK: registering a font while the canvas draws is safe. Before, a label could be drawn with the default font for a moment, or the app could stop with an error.
    * Headless tests (`DrawnUi.Testing`): a `GestureRobot` swipe gives the same fling however busy the machine is. Before, a slow test run could see no fling at all.
    * Blazor and WebAssembly: a GPU canvas recovers by itself when the browser loses its WebGL context (a GPU reset, a driver update, too many canvases open). Before, it stayed blank until the page was reloaded.

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

