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

## What's New 1.10.7.3

  * **Smoother scrolling on iOS and Mac Catalyst.** A frame no longer waits for the screen buffer before it starts drawing, so frames that used to show one refresh late (a short stutter while scrolling) are now on time.
  * **Grids (`Split`) add pages without rebuilding.** A LoadMore page appended to a templated grid keeps every cell it already has, also when the page does not fill whole rows: the new items fill the last row, then new rows. Before, such a page re-laid and rebound the whole grid, so each page got slower as the list grew.
  * **Fix: `MeasureVisible` grids placed rows wrong after the first screen.** Rows measured in the background could land one row too high and overlap the row above.
  * **Fix: `SkiaSpinner` and `SkiaWheelPicker` never raised `ItemsSourceChangesApplied`**, neither for a new `ItemsSource` nor when its collection changed.
  * **Fix: editor, iOS: the caret stays after what you type when the app changes the text.** A number field showing "0" turned "05" into "5" and put the caret before the 5. Now the caret keeps its distance from the end of the text, like on Android, also when a filter drops characters.

## What's New 1.10.7.2

  * **Hover for lists**
    * `ReceivesHover`, `HoverChanged` and the fluent `.OnHovered((me, on) => ...)`: any control can take mouse hover. Every hovered control under the mouse is `IsHovered`, so a card stays lit while the mouse is over the button inside it. Before, hover went to one control at a time, and a list card lost it to its own button.
    * Hover waits while a list scrolls, a carousel slides or a drawer moves, and is checked once when that stops, when a recycled cell under the mouse gets another item, and when a popup opens. Before, a card kept its hover after a wheel scroll until the mouse moved.
    * Controls hover only when they opt in. Buttons, sliders, toggles, radio buttons, carousels, drawers and pickers hover by default as before.
    * MAUI Windows: hover ends when the mouse leaves the canvas. WPF and OpenTK: hover works. Before, it never reached any control there.
  * **ImageComposite redraws a change deep inside by its area.** A card inside an uncached stack in a composite list is redrawn alone, not the whole stack.
  * **Fix: a cached container drew its content shifted when a child's glow or shadow came or went** (a button's hover glow inside a cached panel). The cache kept its surface from the old effects margin; an `ImageComposite` showed the whole list offset until its next full redraw, an `Image` cache could cut the glow.
  * **Lists: `ItemsSourceChangesApplied` comes after every change.** Adding, removing, replacing or moving items, every LoadMore page, and the moment a long list switches to its built-in window at 300 items now raise it, once per frame, after the frame that shows the change. Before, it came only for a new `ItemsSource` or a full rebuild, so a "load the next page when the last one is shown" loop could stop after the first page or at 300 items.
  * **Fewer re-measures when properties change in bursts.** When `Padding`, sizes, margins or `IsVisible` change several times before the next frame (on Android, where drawing has its own thread), every control now measures once. Before, about half of all controls measured again for every single change.
  * **Editor, Android: one caret when several editors are on screen.** Moving from a `SkiaEditor` to an editor in another canvas, or to a native entry, leaves only the new field focused, and the keyboard stays open while you switch fields. Before, both editors kept a blinking caret and switching fields could close the keyboard.

## What's New 1.10.7.1

A hotfix for `SkiaEditor` on Android and iOS, on top of 1.10.6.22.

  * **Editor**
    * Android: number keyboards type into `SkiaEditor`. Before, the keyboard opened and the field had focus, but every digit was lost.
    * iOS: the keyboard closes when you leave a page while typing. Before, it could stay over every screen until the app was closed.
    * iOS: number, decimal and phone keyboards get a Done button above them. It works like the return key and closes the keyboard of a single-line editor. Before, these keyboards could not be closed.
    * Android and iOS: when your `TextChanged` handler rejects what was typed (an input filter, a maximum length), the field shows only the text you kept. Before, the rejected characters stayed hidden in the field, the first deletes removed them and the caret jumped to the wrong place.
  * **PDF**
    * `Pdf.SplitStackToPages` breaks pages in the right places when there is padding or a margin above the content, and a row taller than a page goes on to the next page instead of being cut.

## What's New 1.10.6.22

  * **Accessibility, reworked on every platform**

    This version makes drawn apps usable with a screen reader and a keyboard wherever DrawnUI runs. Every drawn control with a role is its own item for the screen reader: people find it by touch or by swiping, hear its name, role and state, and press it or change its value. The same names, roles and hints reach every platform.

    | Platform | Screen reader |
    |---|---|
    | MAUI Windows, WPF, OpenTK on Windows | Narrator (UI Automation) |
    | MAUI Android | TalkBack, **new** |
    | MAUI iOS and Mac Catalyst | VoiceOver, **new** |
    | OpenTK on Linux | Orca (AT-SPI), **new** |
    | Blazor | the browser's screen readers (ARIA) |
    | WebAssembly (`DrawnUi.Web`) | the browser's screen readers (ARIA), **new** |

    Keyboard navigation (Tab, a focus ring, Enter and Space, the arrow keys) now also works on Mac Catalyst and OpenTK, next to MAUI Windows, WPF, Blazor and WebAssembly.

    * MAUI Android: TalkBack reads drawn controls. Each control with an accessibility role is its own item: touch it to hear it, swipe right or left to move between items in reading order, double tap to press it. Before, TalkBack saw the whole canvas as one empty view. TalkBack reads the same labels, roles and hints as Narrator on MAUI Windows, and nothing runs while no screen reader is on.
    * MAUI Mac Catalyst: Tab and Shift+Tab move between the drawn controls with a focus ring, and the arrow keys move inside a group (a list, a toolbar, a grid), as on MAUI Windows and WPF.
    * MAUI iOS and Mac Catalyst: VoiceOver reads drawn controls. Double tap presses, swiping up or down moves a slider, a three-finger swipe scrolls.
    * OpenTK on Linux: Orca reads drawn controls. It works without any app code once a screen reader is running.
    * OpenTK: Tab and Shift+Tab move between the drawn controls with a focus ring, Enter and Space press, the arrow keys move a slider or inside a group, Escape leaves, as on WPF. Tab moves on from a text field instead of typing four spaces.
    * WebAssembly (`DrawnUi.Web`): screen readers read drawn controls and Tab moves between them, as on Blazor. Before, a pure WebAssembly app was silent for screen readers.
    * Sliders and progress bars: screen readers say the name and the value separately ("Volume, 65", "Download, 65%", "Price range, 20 – 80") and can move a slider or set its value. Before, the value was read as the name. Give each one a name with `AccessibilityLabel`.
    * Screen readers scroll a control into view when they move to it, and TalkBack and VoiceOver can scroll a page with their own gestures.
    * When a page closes under a screen reader, it moves to the next control instead of going silent.
    * A card whose title repeats its name is read once, not twice. A button, switch or slider that cannot be used right now reads as unavailable on every platform.
    * Blazor: switches, checkboxes and radio buttons read their real state. Before, browsers read them as unchecked.
    * MAUI Windows and OpenTK: Narrator presses drawn buttons with its default action. Before, the press failed and Narrator could only read them.
    * WPF `SkiaShell`: the page under an opened page is hidden, as on the other heads. Before, Tab and screen readers reached its controls under the new page.
    * WPF and MAUI Windows: screen readers and Tab see the page that is on screen as soon as it settles. Before, they could keep the previous page until something on the canvas moved.
    * People can select and copy a label's text: `AccessibilityTextSelectable`, see Text below.
    * **What your app does:**
      * Name every control that has no text of its own with `AccessibilityLabel`, by what it controls ("Volume", "Wi-Fi"), not by what it is.
      * Give a list, grid or toolbar a role (`Aria.RoleList`, `RoleGrid`, `RoleToolbar`...): it becomes one Tab stop, and the arrow keys move inside it.
      * Set `AccessibilityLive` on a status text to have it read when it changes.
      * Your own range control reports its value with `GetAccessibilityValue()` and takes one from the screen reader with `OnAccessibilitySetValue()`; your own control takes keys with `OnAccessibilityKey`.
      * `SkiaLabel.DefaultAccessibilityRole` and `SkiaButton.DefaultAccessibilityRole` give every label or button a role in one line.
      * The whole guide: [Accessibility](https://drawnui.net/articles/advanced/accessibility.html).
  * **Rendering**
    * Android: hardware-accelerated canvases (`RenderingMode = Accelerated`) draw with Vulkan. On a phone with a Mali-G57 GPU this takes about 11% less CPU per frame than OpenGL, at the same frame rate. Devices without Vulkan 1.1 (or older than Android 7) keep OpenGL, and so do Android emulators and devices whose system draws its own UI with OpenGL (the emulator's Vulkan cannot draw Skia: images crashed the app or the emulator). When Vulkan fails to start on a device, DrawnUI switches the canvas to OpenGL by itself. To always use OpenGL, set `UseVulkan = false` in the settings you pass to `UseDrawnUi`.

      Measured on a Blackview BV8800 (Mali-G57 GPU, 90 Hz screen), Release build, flinging a list of 100 000 recycled cells, two runs per API:

      | Per frame | Vulkan | OpenGL ES | Vulkan better by |
      |---|---|---|---|
      | App CPU | 16.6 ms | 18.7 ms | 11% less CPU |
      | Render thread CPU | 9.7 ms | 11.1 ms | 13% less CPU |
      | Frame rate | 70.5 FPS | 69 FPS | the same: both keep up with the screen |
    * Android Release builds with LLVM (`EnableLLVM`): controls inside a `SkiaRow` are drawn again. On arm64, .NET 10's LLVM build passed one of the Row's measuring rectangles wrongly, so some Row children got no width and disappeared (a title, icons on cards, wheel pickers). Libraries built against an older DrawnUI that call `ContractPixelsRect`, `ContractPixelsRectForContent` or `ExpandPixelsRect` need a rebuild.
  * **Text**
    * Japanese and Chinese text wraps properly. Lines break between characters, and closing punctuation and small kana never start a line. A very long word, like a URL, also breaks when it is wider than the line.
    * Symbols and emoji inside normal text show up. A `SkiaLabel` draws each character its font is missing with the first `FontFamilyFallback` font that has it, and the rest of the text keeps its own font. `FontFamilyFallback` can list several fonts, for example `"FontSymbols, FontEmoji"`.
    * `SkiaRichLabel` markdown supports `~~strikethrough~~`.
    * `AutoSize = FitHorizontal` works: the font gets smaller until the text fits the width on one line, and grows back to `FontSize` when the text gets shorter or the label wider. Before, it stayed at the smallest size it ever reached, and with `LineBreakMode = NoWrap` the text still ran past the edge. `FitVertical` grows back too now.
    * A label with `AutoSize = FitFillVertical` and `MaxLines = 1` no longer freezes the app. Before, it never finished measuring, and every other label stopped drawing with it.
    * People can select and copy text: turn it on with `AccessibilityTextSelectable` on a `SkiaLabel`. With a mouse, drag or double-click, then press Ctrl+C (Cmd+C on Mac). With a finger, long press, then tap Copy.
    * MAUI Mac Catalyst: mouse clicks reach controls as mouse clicks, so a drag over selectable text selects it instead of scrolling, and Cmd+C / Cmd+A reach the app (the Edit menu used to keep them). Add `UIApplicationSupportsIndirectInputEvents` = `true` to `Platforms/MacCatalyst/Info.plist`: without it macOS hands clicks over as finger touches. Uses AppoMobi gestures 3.11.5.
    * **Changed:** Android with `UseDesktopKeyboard`: text fields get their keys. Before, DrawnUI kept every key from the focused field, so Backspace did nothing (with keyboard suggestions it kept deleting them instead of the text) and Back did not go back. `KeyboardManager` still sees every key; Backspace now arrives as `Backspace` (it came as `Delete`), and the Back button is no longer reported as Backspace.
    * A text field outside DrawnUI keeps its keys on every platform. In the browser, a page `<input>` next to the canvas types spaces and moves its caret on WebAssembly, and on Blazor typing into it no longer also types into a focused drawn `SkiaEditor`. Ctrl+C in a text field no longer copies a drawn label's selection. `KeyboardManager` still reports every key; `KeyboardManager.IsKeyForOtherElement` tells handlers when the key belongs to such a field, so they can ignore it.
  * **Images and animations**
    * Image preloading has priorities: `PreloadImages(urls, LoadPriority.Low)` waits until the images on screen are loaded. Network images load a few at a time (`MaxParallelLoads`), and `RunningCount` / `QueuedCount` show how many are loading and waiting. `RemoveFromCache` removes one image.
    * `SkiaLottie` and `SkiaSprite` raise `Success` when their file is loaded and `Error` when it fails, like `SkiaGif`.
    * `SkiaCarousel.ScrollTo(index, animate)` goes to a slide; with `animate: false` it jumps there at once.
    * `SkiaImage` options that used to do nothing now work. `Aspect = Tile` repeats the image at its natural size, starting from the copy placed by the alignment. `SpriteWidth`, `SpriteHeight` and `SpriteIndex` show one cell of a sprite sheet. `UseGradient` with `StartColor` and `EndColor` paints a top-to-bottom gradient through the image. The `Grayscale` effect turns the image gray, and `UseAssembly` loads the `Source` from that assembly's embedded resources.
    * `SkiaSvg`: `Aspect = Tile` repeats the picture, and `IconFilePath` loads its file the same way `Source` does.
    * An image that loads in the background no longer reports an error right after it loaded. Before, `Error` came after `Success` and `HasError` stayed true.
    * **Changed:** `SpeedRatio` on `SkiaSprite`, `SkiaGif` and `SkiaLottie` means what it says: 0.5 plays at half speed, 2 at double speed. Before, slow values played too fast (0.5 ran at about two thirds of the speed). `FrameSequence` shows each of its frames once, `DefaultFrame` is a frame number (-1 is the last frame) and `CurrentFrame` shows the frame you set.
  * **Controls**
    * MAUI Windows: a right click, Shift+F10 or the Menu key reaches `SkiaControl.ContextMenu`, as on WPF and in the browser.
    * **Changed:** `LockChildrenGestures` works on layouts. Before, layouts ignored it, so taps reached their children whatever the value. Now `Enabled` and `PassNone` keep every gesture from the children, `PassTap` lets only taps through, and `PassTapAndLongPress` taps and long presses. The layout itself still gets its own `Tapped`, so "lock the children, handle the tap on the card" works. `Enabled` also keeps gestures from controls stacked under the layout, as its description says.
    * `SkiaWheelPicker` and `SkiaSpinner` raise `SelectedIndexChanged` when the user turns the wheel. Before, it fired only when code set the index. A spinner set from code shows the right item (it showed the one on the opposite side), and a wheel picker raises the event once when its first item gets selected.
    * `SkiaPicker` has a Material 3 look (`ControlStyle = Material3`): an outlined field whose placeholder moves up into the outline as a label once something is picked.
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
  * **Drawing and caching**
    * WPF, OpenTK, WebAssembly and Blazor: changing `Rotation` at runtime redraws the control. Before, it waited for something else to redraw.
    * A `.WhenPainted` overlay keeps drawing after its control is hidden and shown again. Before, hiding removed it for good, so a page pushed in a MAUI `SkiaShell` lost its overlays.
    * `LastCompositeRecord` shows what an `ImageComposite` cache redrew last time: only the changed children, or everything.
    * `ImageDoubleBuffered` is more reliable. A control that changes all the time still updates on screen, even when its background render takes longer than a frame; before, it kept its old look until the changes stopped. A cell shows its placeholder until its first image is ready, and never over an image it already has; before, the placeholder showed for one frame and then left a hole. Images that were replaced before they were shown go back to the pool at once, and a render that fails is not repeated forever. A GPU-cached control inside an `ImageDoubleBuffered` parent draws directly, because the GPU cannot be used from the background thread.
    * `UseCache = SkiaCacheType.Auto` works like `Image`.
    * Shader files load on every platform. `ShaderSource`, `ShaderTemplate`, a `SkiaShaderCarousel`'s `TransitionShader` and the textures of a two-texture effect are read from the app's folder on OpenTK and WPF and from the site in the browser, as MAUI reads them from the app package. Before, OpenTK and pure WebAssembly could not open them at all, and WPF read every shader file next to the app at startup, used or not.
    * With `Super.Multithreaded` on, an `Operations` or `OperationsFull` cache is no longer redrawn on every frame. Before, it was thrown away and drawn again each time.
    * WPF, OpenTK and WebAssembly: an animation started while a frame is being drawn starts at once. Before, it waited for the next touch or mouse move. `DrawnView.RequestNextFrame()` asks for one more frame from anywhere, also from inside a draw.
  * **OpenTK and Linux**
    * Smooth frames where the graphics driver ignores vsync, like Linux under WSL: a `Constant` window notices it in its first second and spaces frames one screen refresh apart, and a `Dynamic` window always does. Before, a game ran at hundreds of frames a second there and movement stuttered.
    * On Windows, frames follow the screen's exact refresh rate. A 59.95 Hz screen used to get 59 frames a second, a little behind the display.
    * `SkiaShell` and C# Hot Reload work on OpenTK too. They were in the WPF package only.
    * OpenTK: a right click, the Menu key or Shift+F10 reaches `SkiaControl.ContextMenu`, as on WPF and MAUI Windows.
    * New sample `HelloOpenTk`: the DrawnUI Hello app, all 20 screens, on OpenTK for Windows and Linux. `dev/hello-opentk-linux.ps1` builds it for Linux on Windows and runs it in WSL.
  * **Stability**
    * Closing a canvas while one of its controls is still being rendered in the background no longer crashes: the canvas waits for that render to finish first.
    * WPF and OpenTK: registering a font while the canvas draws is safe. Before, a label could be drawn with the default font for a moment, or the app could stop with an error.
    * Headless tests (`DrawnUi.Testing`): a `GestureRobot` swipe gives the same fling however busy the machine is. Before, a slow test run could see no fling at all.
    * Blazor and WebAssembly: a GPU canvas recovers by itself when the browser loses its WebGL context (a GPU reset, a driver update, too many canvases open). Before, it stayed blank until the page was reloaded.
    * WebAssembly (`DrawnUi.Web`): an app published under a sub-path of a site (like `/myapp/`) starts. Before, it looked for its script at the root of the site.
    * Blazor Server: frames are drawn at their real size. Before, the drawing came out 2% too large and lost its right and bottom edges. Headless tests (`HeadlessCanvasHost`) had the same 2% and are exact now.

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

