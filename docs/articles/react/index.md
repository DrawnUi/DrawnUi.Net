# DrawnUI for React

DrawnUI for React is the DrawnUI engine in TypeScript, running on [CanvasKit](https://skia.org/docs/user/modules/canvaskit/) (Skia compiled to WebAssembly) in the browser. React composes the control tree through a custom `react-reconciler` renderer. React never touches the canvas: it creates, updates and removes engine controls, and the engine measures, arranges and paints them.

It is early and under active development. The package is published under the `preview` tag.

- Source: [github.com/DrawnUi/DrawnUi.React](https://github.com/DrawnUi/DrawnUi.React)
- Live demo: [helloreact.drawnui.net](https://helloreact.drawnui.net)
- Agent skill: [drawnui-react/SKILL.md](https://helloreact.drawnui.net/skills/drawnui-react/SKILL.md)

## Install

```bash
npm i drawnui-react@preview react react-dom
```

`drawnui-react` gives you the React tags plus the engine types, `drawnui-react/core` the engine alone. CanvasKit's `.wasm` is referenced with a `?url` import, so a bundler that understands it (Vite) is required.

## Usage

```tsx
await Super.UseDrawnUi()
  .ConfigureFonts((fonts) => fonts.AddFont("fonts/OpenSans-Regular.ttf", "FontText"))
  .BuildAsync();

<Canvas BackgroundColor={Colors.DarkSlateBlue} RenderingMode="Accelerated" Gestures="Enabled">
  <SkiaStack Spacing={8} Padding={new Thickness(16)} VerticalOptions="Center">
    <SkiaLabel Text="Hello World" FontSize={32} TextColor={Colors.White} HorizontalOptions="Center" />
    <SkiaButton Text="Tap me" ApplyEffect="Ripple" HorizontalOptions="Center" Tapped={() => setCount((c) => c + 1)} />
  </SkiaStack>
</Canvas>
```

## What it shares with DrawnUi.Net

The goal is the same API surface as the .NET version: same control names, same PascalCase property names, same measure/arrange/paint contract, so documentation transfers.

Ported so far: `SkiaLayout` in Absolute, Column and Row (plus the `SkiaStack` / `SkiaRow` / `SkiaLayer` aliases) with templated recycling cells in Column mode, `SkiaScroll`, `SkiaLabel` and `SkiaRichLabel`, `SkiaShape`, `SkiaImage`, `SkiaSvg`, `SkiaButton`, `SkiaSwitch`, `SkiaCheckbox`, `SkiaRadioButton`, `SkiaSlider`, `SkiaProgress`, `SkiaCarousel`, `SkiaDrawer`, `SkiaEditor`, `SkiaLottie`, `SkiaGif`, `SkiaSprite`, `SkiaBackdrop`, `DrawnGame`, shader effects, gradients, transforms, animators, the tap and pan gesture pipeline, and the accessibility overlay model used by DrawnUi.Blazor.

Caching follows the .NET model: `UseCache` takes the same values. `Operations` records an `SkPicture` and replays it, `Image` snapshots an offscreen surface, `ImageDoubleBuffered` keeps the last cache while a new one is produced, and `ImageComposite` keeps its offscreen surface between records and repaints only the children that changed plus the siblings they overlap.

## Samples

- **The demo app**: [helloreact.drawnui.net](https://helloreact.drawnui.net), one page per feature, source in the repository.
- **Snippets**: more DrawnUI snippets you can run and edit right in the browser at [drawfiddle.com](https://drawfiddle.com).

The demo app lives in [`samples/demo`](https://github.com/DrawnUi/DrawnUi.React/tree/master/samples/demo): one React app with one `<Canvas>`, and a `SkiaShell` whose root menu opens a page per feature. To run it locally:

```bash
git clone https://github.com/DrawnUi/DrawnUi.React
cd DrawnUi.React
npm install
npm run dev   # http://localhost:5173
```

Startup, fonts and routes are in [`samples/demo/main.tsx`](https://github.com/DrawnUi/DrawnUi.React/blob/master/samples/demo/main.tsx), the pages in [`samples/demo/pages`](https://github.com/DrawnUi/DrawnUi.React/tree/master/samples/demo/pages). Every page is a deep link on the live site:

| Page | What it shows |
|---|---|
| [Recycled cells](https://helloreact.drawnui.net/#/cells) | 100 000 items in a `SkiaScroll`, recycled templated cells |
| [Uneven cells](https://helloreact.drawnui.net/#/uneven) | Rows of different heights, `MeasureVisible`, LoadMore at both ends |
| [Images](https://helloreact.drawnui.net/#/images) | `SkiaImage`: every `TransformAspect`, alignment, clipping |
| [SVG](https://helloreact.drawnui.net/#/svg) | `SkiaSvg`: file and inline sources, `TintColor` |
| [Shapes](https://helloreact.drawnui.net/#/shapes) | `SkiaShape`: rectangle, circle, arc, polygon, path, strokes, clipping |
| [Text](https://helloreact.drawnui.net/#/text) | `SkiaLabel`: wrapping, `MaxLines`, spans, weights, glyph fallback |
| [Layouts](https://helloreact.drawnui.net/#/layouts) | Absolute, Column, Row, Wrap and Grid layouts |
| [Common Controls](https://helloreact.drawnui.net/#/looks) | Switch, checkbox, radio, progress, slider, button in every platform look |
| [Carousel & Drawer](https://helloreact.drawnui.net/#/snapping) | `SkiaCarousel` and `SkiaDrawer` |
| [Lottie & GIF](https://helloreact.drawnui.net/#/animations) | `SkiaLottie` and `SkiaGif` |
| [Shell](https://helloreact.drawnui.net/#/shell) | `SkiaShell`: page transitions, popups, modals, toasts |
| [Editor](https://helloreact.drawnui.net/#/editor) | `SkiaEditor`: caret, selection, password, multiline |
| [Keyboard Input](https://helloreact.drawnui.net/#/keyboard) | `KeyboardManager` key events |
| [SkiaScroll](https://helloreact.drawnui.net/#/scroll) | Headers, footers, scroll bars, pull to refresh, snapping |
| [Shaders](https://helloreact.drawnui.net/#/shaders) | `SkiaShaderEffect` and shader slide transitions |
| [Sprites](https://helloreact.drawnui.net/#/sprites) | `SkiaSprite` sheets and a keyboard-driven `SkiaSpriteSet` |
| [Transforms](https://helloreact.drawnui.net/#/transforms) | Rotation, scale, skew, translation, with hit-testing through them |
| [Drag to reorder](https://helloreact.drawnui.net/#/reorder) | A list row lifted and dragged, reordering live |
| [Pong](https://helloreact.drawnui.net/#/pong) | `DrawnGame`: the .NET Pong sample ([source](https://github.com/DrawnUi/DrawnUi.React/tree/master/samples/demo/pages/pong)) |
| [Accessibility](https://helloreact.drawnui.net/#/a11y) | The ARIA overlay: roles, labels, live regions, keyboard |

The pages describe DrawnUi features rather than React ones, and most are ports of the .NET samples, so the same code reads the same on both sides.

The same app, with the same pages, the same look and the same structure (a catalog, a root menu and one class per page), exists for .NET too, so you can compare a page line by line across platforms:

- .NET MAUI: [`src/Maui/Samples/HelloMaui`](https://github.com/DrawnUi/DrawnUi.Net/tree/main/src/Maui/Samples/HelloMaui), navigated by DrawnUi.Maui's own `SkiaShell`.
- WPF: [`src/Wpf/Samples/HelloWpf`](https://github.com/DrawnUi/DrawnUi.Net/tree/main/src/Wpf/Samples/HelloWpf).

## Games

You can write games with DrawnUI for React the same way you would in .NET. DrawnUI gives you a convenient base for it, `DrawnGame`: a layout that runs your game loop, calls your code once per frame with the time that passed since the previous frame, and hands you the keys the player presses. You create your sprites once, move them on every frame, and the engine takes care of drawing, caching and input.

Want to see it in action? The demo includes Pong, ported line by line from the .NET sample. [Play it here](https://helloreact.drawnui.net/#/pong), then open [the game's code](https://github.com/DrawnUi/DrawnUi.React/tree/master/samples/demo/pages/pong) and [the page that puts it on screen](https://github.com/DrawnUi/DrawnUi.React/blob/master/samples/demo/pages/PongPage.tsx). The engine side is [`DrawnGame.ts`](https://github.com/DrawnUi/DrawnUi.React/blob/master/src/controls/DrawnGame.ts).

## Still in progress

Work continues control by control against the .NET sources. On the list today:

- A dedicated GPU cache path. `GPU` currently resolves to `Image` and `ImageCompositeGPU` to `ImageComposite`.
- Long press, hover and multi-touch pinch. They are declared for parity and not produced yet. Right click: every mouse button taps, with the button in `e.Parameters.Event.Pointer`, and `ContextMenu` on any control (and on `<Canvas>`) takes the browser menu request, the same handler shape as on the .NET web heads.

## Known limitations

Everything runs on the browser's main thread. DrawnUI for .NET can record caches on background threads while the screen keeps drawing; here a cache is recorded right inside the frame that needs it.

This is not a React thing, and not a big deal: DrawnUi.Blazor and DrawnUi.Web (pure WebAssembly) work exactly the same way, because WebAssembly in the browser runs on a single thread. A cache is recorded once, when its content changes, and then simply replayed on every following frame, so the extra work lands on the frames where something actually changed.
