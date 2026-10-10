# DrawnUI for Rust

DrawnUI for Rust is the DrawnUI engine in Rust, drawing with [Skia](https://skia.org) through rust-skia. One Rust source runs on Windows, macOS, Linux, iOS, Android and in the browser (WebAssembly). Every control is drawn by Skia, so an app looks the same on a phone, a desktop and a web page.

It is early and under active development. The crate is published as a preview (`0.1.0-preview.N`).

- Source: [github.com/DrawnUi/DrawnUi.Rust](https://github.com/DrawnUi/DrawnUi.Rust)
- Crate: [`drawnui`](https://crates.io/crates/drawnui) · API docs: [docs.rs/drawnui](https://docs.rs/drawnui)
- Live demo: [hellorust.drawnui.net](https://hellorust.drawnui.net)
- A game made with it: [run.drawnui.net](https://run.drawnui.net) (Dungeon Run, a 3D runner drawn with SkMesh)
- Agent skill: [drawnui-rust/SKILL.md](https://hellorust.drawnui.net/skills/drawnui-rust/SKILL.md)

## Install

```bash
cargo add drawnui
```

It takes the newest preview. Or by hand, in your `Cargo.toml`:

```toml
[dependencies]
drawnui = "0.1.0-preview"
```

`"0.1.0-preview"` is the newest 0.1.0 preview; `cargo update` moves a project to a newer one.

That is the only dependency. The first build downloads Skia prebuilt for your platform from [DrawnUi/rust-skia's releases](https://github.com/DrawnUi/rust-skia/releases), so there is nothing else to install for the desktop. Use Skia's own API through `drawnui::skia`, and do not add `skia-safe` to the app: a program links one Skia.

To start a new app, copy the starter template [`templates/app`](https://github.com/DrawnUi/DrawnUi.Rust/tree/main/templates/app): an empty app with an icon, assets, a web page and build scripts for the desktop, the browser and Android. The browser and Android builds need a few link settings in `.cargo/config.toml`; the [repository README](https://github.com/DrawnUi/DrawnUi.Rust#getting-started) lists them per platform.

## Usage

```rust
use drawnui::prelude::*;

#[derive(Default)]
struct App { count: i32 }

fn build(_app: &mut App) -> Build<SkiaLayout> {
    SkiaLayout::column().spacing(16).padding(24).children((
        SkiaLabel::new("").font_size(24).text_color(Color::WHITE)
            .observe(|me, app: &App| me.set_text(format!("Count {}", app.count))),
        SkiaButton::new("Tap me").on_tapped(|_me, app: &mut App, _cx| app.count += 1),
    ))
}

fn main() {
    drawnui::run("Counter", || Box::new(Ui::new(App::default(), build).font("Default", "assets/OpenSans-Regular.ttf")));
}
```

The tree is built with builders in the style of DrawnUI's [fluent C# API](../fluent-extensions.md): compose the controls, `observe` your app state to update a control, handle a tap in one line.

## What it shares with DrawnUi.Net

The same controls, the same rules and the same measure / arrange / paint contract as DrawnUI for .NET and DrawnUI for React, with Rust names (`font_size` for `FontSize`), so the documentation transfers. [`PARITY.md`](https://github.com/DrawnUi/DrawnUi.Rust/blob/main/PARITY.md) lists every control and feature and where it differs from .NET.

What you can use today:

- **Layouts**: Absolute, Column, Row, Wrap, Grid and decorated grids; templated lists with recycling and virtualization; render transforms, opacity, clipping, z-index.
- **Controls**: shapes, labels and rich text, buttons, an editor with IME, scroll with scroll bars and pull to refresh, carousels (also with shader transitions), drawer, shell (pages, tabs, popups, modals, toasts), switch, checkbox, radio buttons, slider, progress, backdrop blur.
- **Media**: images (decoded off the frame thread), image tiles, SVG, GIF, Lottie, sprites, SkSL shader effects and transitions, SkMesh (your own vertex and fragment programs).
- **Caching**: every DrawnUI cache type: `Operations`, `OperationsFull`, `Image`, `GPU`, `ImageDoubleBuffered` (bitmaps made on background threads on the desktop) and `ImageComposite`. A lost GPU context comes back on its own.
- **Input**: tap, pan, fling, long press, hover, context menu, mouse wheel and touchpad, keyboard and focus.
- **Accessibility**: screen readers on every platform, keyboard navigation with a focus ring, values a screen reader reads and changes. See [Accessibility](#accessibility) below.
- **Animation**: value, range, spring, ping-pong, pendulum and ripple animators, timers.

## Accessibility

Drawn controls reach screen readers and the keyboard on every platform, with no work from the app for the common cases. The engine keeps one list of what is on screen: each control that has a role, with its name, its place on screen (through transforms and scrolling), its state or value, in reading order. Each platform gets it its own way:

| Platform | How | Screen readers |
|---|---|---|
| Windows | UI Automation | Narrator, NVDA |
| macOS, iOS | VoiceOver accessibility | VoiceOver |
| Linux | AT-SPI | Orca |
| Android | the activity's view | TalkBack |
| Browser | an ARIA overlay of invisible elements over the canvas | NVDA, JAWS, VoiceOver, TalkBack |

What people get:

- **Names, roles and states**: a button is read with its caption, a switch with its state. A card titled by its own text is read once, not twice. A control that cannot be used right now is read as unavailable.
- **Values**: a slider or progress bar is read as a name and a value ("Volume, 65", "Download, 65%", "Price range, 20 – 80"), and a screen reader can move a slider or set its value.
- **Actions**: a screen reader presses a control, scrolls it into view when it moves to it, and pages a scroll with its own gestures on iOS and Android. When a page closes, the reader moves to the next control instead of going silent.
- **Keyboard**: on the desktop, Tab and Shift+Tab walk the controls in reading order with a focus ring, Enter and Space press, the arrow keys move a slider or move inside a group (a list, a toolbar, a grid), Escape leaves. In the browser the page's own Tab order and focus ring apply.
- **Selectable text**: a label with `accessibility_text_selectable` can be selected and copied, with the mouse or a long press.

What an app does:

- Name every control that has no text of its own by what it controls: `.accessibility_label("Wi-Fi")` on a switch, `"Volume"` on a slider.
- Give a container a role (`Aria::LIST`, `Aria::TOOLBAR`...) when it is a group: it becomes one Tab stop and the arrow keys move inside it.
- Mark a status text with `accessibility_live("polite")` to have it read when it changes.

These are the same rules as in DrawnUI for .NET, see [Accessibility](../advanced/accessibility.md). The [Accessibility page](https://hellorust.drawnui.net/#/a11y) of the demo shows them, and [`ACCESSIBILITY.md`](https://github.com/DrawnUi/DrawnUi.Rust/blob/main/ACCESSIBILITY.md) in the repository has the details and where each platform was checked.

## Platforms

| Platform | Graphics | Skia prebuilt for |
|---|---|---|
| Windows | OpenGL | x64, ARM64 |
| macOS | Metal | Apple silicon, Intel |
| Linux | OpenGL, X11 and Wayland | x64, ARM64 |
| iOS | Metal | devices, simulator |
| Android | Vulkan, with OpenGL ES as the fallback | arm64, armv7, x86_64, x86 |
| Browser | WebGL2 (`wasm32-unknown-emscripten`) | all browsers with WebGL2 |

In the browser your code and Skia live in one WebAssembly module and call each other directly, with no JavaScript in between. The [repository README](https://github.com/DrawnUi/DrawnUi.Rust#benchmarks) has the benchmarks.

## Samples

- **The demo app**: [hellorust.drawnui.net](https://hellorust.drawnui.net), one page per feature, source in the repository.
- **A game**: [Dungeon Run](https://run.drawnui.net), source in [`examples/dungeon`](https://github.com/DrawnUi/DrawnUi.Rust/tree/main/examples/dungeon).

The demo app lives in [`examples/hellorust`](https://github.com/DrawnUi/DrawnUi.Rust/tree/main/examples/hellorust): one canvas and a `SkiaShell` whose root menu opens a page per feature. The same app runs on the desktop and in the browser. To run it locally:

```bash
git clone https://github.com/DrawnUi/DrawnUi.Rust
cd DrawnUi.Rust
cargo run --release -p hellorust
```

The repository builds against a few checkouts next to it; the [README](https://github.com/DrawnUi/DrawnUi.Rust#building-this-repository) lists them. The pages are in [`examples/hellorust/src/pages`](https://github.com/DrawnUi/DrawnUi.Rust/tree/main/examples/hellorust/src/pages). Every page is a deep link on the live site:

| Page | What it shows |
|---|---|
| [Recycled cells](https://hellorust.drawnui.net/#/cells) | 100 000 items in a `SkiaScroll`, recycled templated cells |
| [Uneven cells](https://hellorust.drawnui.net/#/uneven) | Rows of different heights, `MeasureVisible`, LoadMore at both ends |
| [Images](https://hellorust.drawnui.net/#/images) | `SkiaImage`: every `TransformAspect`, alignment, clipping |
| [SVG](https://hellorust.drawnui.net/#/svg) | `SkiaSvg`: file and inline sources, `TintColor` |
| [Shapes](https://hellorust.drawnui.net/#/shapes) | `SkiaShape`: rectangle, circle, arc, polygon, path, strokes, clipping |
| [Text](https://hellorust.drawnui.net/#/text) | `SkiaLabel`: wrapping, `MaxLines`, spans, weights, glyph fallback |
| [Layouts](https://hellorust.drawnui.net/#/layouts) | Absolute, Column, Row, Wrap and Grid layouts |
| [Common Controls](https://hellorust.drawnui.net/#/looks) | Switch, checkbox, radio, progress, slider, button in every platform look |
| [Carousel & Drawer](https://hellorust.drawnui.net/#/snapping) | `SkiaCarousel` and `SkiaDrawer` |
| [Lottie & GIF](https://hellorust.drawnui.net/#/animations) | `SkiaLottie` and `SkiaGif` |
| [Shell](https://hellorust.drawnui.net/#/shell) | `SkiaShell`: page transitions, popups, modals, toasts |
| [Editor](https://hellorust.drawnui.net/#/editor) | `SkiaEditor`: caret, selection, password, multiline |
| [Keyboard Input](https://hellorust.drawnui.net/#/keyboard) | Window-level key events |
| [SkiaScroll](https://hellorust.drawnui.net/#/scroll) | Headers, footers, scroll bars, pull to refresh, snapping |
| [Shaders](https://hellorust.drawnui.net/#/shaders) | `SkiaShaderEffect` and shader slide transitions |
| [Sprites](https://hellorust.drawnui.net/#/sprites) | `SkiaSprite` sheets and a keyboard-driven `SkiaSpriteSet` |
| [Transforms](https://hellorust.drawnui.net/#/transforms) | Rotation, scale, skew, translation, with hit-testing through them |
| [Drag to reorder](https://hellorust.drawnui.net/#/reorder) | A list row lifted and dragged, reordering live |
| [Pong](https://hellorust.drawnui.net/#/pong) | `DrawnGame`: the .NET Pong sample ([source](https://github.com/DrawnUi/DrawnUi.Rust/blob/main/examples/hellorust/src/pages/pong.rs)) |
| [Accessibility](https://hellorust.drawnui.net/#/a11y) | Roles, labels, live regions, keyboard |

The same app, with the same pages and the same look, exists for .NET and React too, so you can compare a page across platforms:

- .NET MAUI: [`src/Maui/Samples/HelloMaui`](https://github.com/DrawnUi/DrawnUi.Net/tree/main/src/Maui/Samples/HelloMaui).
- WPF: [`src/Wpf/Samples/HelloWpf`](https://github.com/DrawnUi/DrawnUi.Net/tree/main/src/Wpf/Samples/HelloWpf).
- React: [helloreact.drawnui.net](https://helloreact.drawnui.net), see [DrawnUI for React](../react/index.md).

## Games

DrawnUI for Rust has `DrawnGame`, as .NET and React do: a layout that runs your game loop, calls your code once per frame with the time that passed since the previous frame, and hands you the keys the player presses. You create your sprites once, move them on every frame, and the engine takes care of drawing, caching and input.

The demo includes Pong, ported from the .NET sample: [play it here](https://hellorust.drawnui.net/#/pong), then open [the game's code](https://github.com/DrawnUi/DrawnUi.Rust/blob/main/examples/hellorust/src/pages/pong.rs). For a bigger example, [Dungeon Run](https://run.drawnui.net) draws its 3D world with SkMesh.
