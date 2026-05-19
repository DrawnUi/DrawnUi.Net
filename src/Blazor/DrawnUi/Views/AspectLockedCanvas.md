# AspectLockedViewportHost + AspectLockedCanvas

Two cooperating components that keep a DrawnUI game canvas at a fixed logical aspect ratio regardless of browser window size, device pixel ratio, or fullscreen state.

## Problem

A game or app designed at a fixed logical resolution (e.g. 360×760 DIPs) must render correctly on any screen: desktop browser, mobile, fullscreen overlay. Naive approach — fill the browser window — distorts proportions. Skia-level translate+scale workarounds break gesture coordinate mapping.

## Solution: two layers

```
AspectLockedViewportHost  (HTML/CSS)
└── AspectLockedCanvas    (DrawnUI Canvas subclass)
    └── game content
```

### AspectLockedViewportHost (`AspectLockedViewportHost.razor`)

Pure Blazor/HTML component. Renders one `<div>` that defines how much space the game occupies in the page.

**Three CSS modes** via `HostClass`:

| Parameter | CSS class | Behaviour |
|-----------|-----------|-----------|
| default | `fit-width` | `width:100%`, CSS `aspect-ratio` drives height |
| `FitVisibleHeight=true` | `fit-height` | Width is clamped by both `100%` and `calc((100vh - offset) * aspectRatio)` so portrait hosts can shrink with narrow layouts while still respecting the visible height remaining below the page chrome |
| `IsFullscreen=true` | `fullscreen` | `position:fixed; inset:0`, covers entire viewport |

`AspectWidth` / `AspectHeight` are passed as CSS custom properties (`--host-aspect`). The browser enforces the ratio natively via `aspect-ratio`. When `FitVisibleHeight=true`, the host also measures its own top offset in the viewport and subtracts that from `100vh` by default, so callers do not need page-local observer glue. `ViewportOffsetPixels` remains available as an explicit override when a caller needs to supply a fixed custom value.

**Role after refactor**: space provider. Its div gives `AspectLockedCanvas`'s resize observer a meaningful CSS bounding box to measure. Without it, the canvas outer div has no parent-driven size and collapses to 1 px.

The `aspect-ratio` CSS is now secondary for rendering correctness (the canvas handles letterboxing), but it controls where the background color (letterbox bars) appears around the fitted canvas element.

### AspectLockedCanvas (`AspectLockedCanvas.cs`)

Extends DrawnUI `Canvas`. Handles all letterboxing logic in C# without any Skia-level translate or scale transforms.

**Sizing — `OnHostResized` override**

The JS resize observer fires with the CSS pixel dimensions of the canvas host div (which fills `AspectLockedViewportHost`). This override:

1. Computes `fitScale = min(hostW / LogicalWidth, hostH / LogicalHeight)`
2. Sets `WidthRequest = LogicalWidth * fitScale` and `HeightRequest = LogicalHeight * fitScale`

For `AspectLockedCanvas`, those requests size the inner `<canvas>` element only. The outer Blazor host still stretches to the available parent bounds so ResizeObserver continues measuring the full viewport or fullscreen container instead of collapsing to the fitted canvas width.

`Canvas.razor` then sizes the underlying `<canvas>` HTML element to exactly those fitted CSS pixels. The canvas element is centered inside the host surface div via CSS flex (`CenterSurface = true`).

**Rendering — `CreateContentContext` override**

DrawnUI's `Canvas.Draw()` calls `Arrange()` at the real device pixel ratio (DPR) — `DrawingRect` fills the fitted canvas correctly. Before passing the drawing context to children, `CreateContentContext` adjusts `context.Scale`:

```
fitScale = DrawingRect.Width / (LogicalWidth * context.Scale)
adjusted  = context.WithScale(context.Scale * fitScale)
```

Children receive a scale where `LogicalWidth × newScale = DrawingRect.Width`. Content fills the canvas exactly, no overflow, no clipping.

**Why no Skia transforms**

The previous implementation applied `canvas.Translate + canvas.Scale` inside `Draw()`. This worked visually but broke gesture coordinate mapping: touch events arrived in host-div space and had to be inverse-transformed before reaching game logic. With the sized-canvas approach the `<canvas>` HTML element IS the game area — gesture coordinates are correct natively.

## Usage

```razor
<AspectLockedViewportHost
    AspectWidth="360"
    AspectHeight="760"
    BackgroundColor="#000000"
    FitVisibleHeight="true">

    <AspectLockedCanvas
        Content="_gameRoot"
        LogicalWidth="360"
        LogicalHeight="760"
        HorizontalOptions="LayoutOptions.Fill"
        VerticalOptions="LayoutOptions.Fill"
        RenderingMode="RenderingModeType.Accelerated"
        Gestures="GesturesMode.Enabled" />

</AspectLockedViewportHost>
```

`LogicalWidth` / `LogicalHeight` must match `AspectWidth` / `AspectHeight` on the host.

## Data flow

```
Browser resize
  → JS ResizeObserver fires on canvas host div
  → OnHostResized(hostCssPx.W, hostCssPx.H)
  → fitScale = min(W/LogicalW, H/LogicalH)
  → WidthRequest = LogicalW * fitScale   (CSS px)
  → HeightRequest = LogicalH * fitScale  (CSS px)
  → StateHasChanged → <canvas> element resized
  → next draw frame
      → Arrange() fills DrawingRect = fittedPx * DPR
      → CreateContentContext adjusts context.Scale
      → children render at correct density, no overflow
```

## Changes made to Canvas (DrawnUI core)

`Canvas.razor` required three additions to support this pattern:

- `OnHostResized` made `virtual` — allows subclass to intercept resize and set `WidthRequest/HeightRequest` before the base updates `_measuredCanvasWidth/Height`.
- `CreateContentContext(DrawingContext)` virtual hook — called in `Draw()` just before passing context to children; default returns context unchanged.
- `CenterSurface` virtual bool + `.xaml-canvas-surface.centered` CSS — when true, surfaces uses `display:flex; align-items:center; justify-content:center` so a canvas element smaller than its surface div is centered (letterbox bars are parent background).
