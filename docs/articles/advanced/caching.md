# Caching System

DrawnUI uses a render-object caching system to avoid re-painting controls every frame. Understanding it is essential for hitting 60fps on complex UIs.

## How It Works

Every `SkiaControl` holds a `RenderObject` — a snapshot of the last successful draw. On each frame the engine checks whether that snapshot is still valid (`CheckCachedObjectValid`). If yes, it blits the snapshot. If no, it repaints and stores a new one.

Three things can trigger re-paint:
- `InvalidateCache()` → sets `RenderObjectNeedsUpdate = true` (cache is stale)
- Size or position change that makes the snapshot the wrong dimensions
- GPU context change (app returns to foreground, display change)

`Update()` calls `InvalidateCache()` internally. `Repaint()` does **not** — it redraws the parent surface without destroying the control's own cache. Use `Repaint()` when only position or transform changed.

---

## Cache Types

Set via `UseCache` property (`SkiaCacheType` enum).

### `None`
No caching. Control repaints every frame.

Use for: `SkiaScroll`, `SkiaDrawer`, `SkiaCarousel`, `SkiaMauiElement`, and their containers — these either change every frame or contain native views that cannot be cached.

### `Operations`
Records drawing commands as a `SKPicture`. Replays fast, zero memory per pixel.

Use for: static text (`SkiaLabel`, `SkiaRichLabel`), SVG icons, simple shapes.

**Prohibitions:**
- Never on controls with GPU-surface shaders (particle systems, blur shaders, etc.) — `SKPicture` cannot replay shader programs.
- Never as the parent of children using `GPU` or `ImageCompositeGPU` — GPU-backed surfaces cannot be nested inside a picture recording.

### `OperationsFull`
Like `Operations` but ignores clipping bounds. Use only when content intentionally draws outside its layout rect.

### `Image`
Rasterizes to a CPU `SKBitmap`. Costs memory proportional to pixel area; fast to draw.

Use for: complex layouts, buttons, cards, anything with shadows or gradients.

### `ImageDoubleBuffered`
Creates two CPU surfaces. Shows previous cache while rendering new one on a background thread — zero jank during updates.

Use for: recycled list cells under `MeasureFirst`/`MeasureAll` (for even-height rows with large cells prefer `GPU` instead). Not needed under `MeasureItemsStrategy="MeasureVisible"` — measurement already runs in background there, so plain `Image` gives the same smoothness and saves the second surface. Memory cost is 2× `Image`.

### `ImageComposite` / `ImageCompositeGPU`
Maintains one surface and repaints only dirty (changed) child regions. Preserves unchanged areas.

Use for: mixed-content containers where some children are interactive (badges, counters) while others are static (avatars, titles). More detail in [ImageComposite internals](#imagecomposite-internals).

### `GPU`
Caches to a `GRBackendTexture` in GPU memory. Zero CPU readback cost; blits directly on GPU.

Use for: small, stable overlays — headers, navigation bars, toolbars.

**Caution:** GPU memory is limited. Avoid large surfaces or many simultaneous GPU-cached controls. Never for controls with GPU-surface shaders.

---

## Choosing the Right Type

| Scenario | Cache type |
|---|---|
| Static text, icon, SVG | `Operations` |
| Button, card, complex shape | `Image` |
| Recycled list cell, `MeasureVisible` strategy | `Image` |
| Recycled list cell, `MeasureFirst`/`MeasureAll`, even rows + large cells | `GPU` |
| Recycled list cell, `MeasureFirst`/`MeasureAll`, other | `ImageDoubleBuffered` |
| Container with mixed static + live children | `ImageComposite` |
| Small header/navbar overlay | `GPU` |
| Scroll view, drawer, carousel | `None` |
| Native embedded view (`SkiaMauiElement`) | `None` |
| Control with GPU-surface shader | `Image` or `ImageDoubleBuffered` |

---

## Cache Invalidation

```csharp
control.Update();            // Invalidate cache + schedule repaint
control.Repaint();           // Repaint parent surface (no cache destruction)
control.Invalidate();        // Full re-measure + repaint
control.InvalidateMeasure(); // Re-measure + repaint
```

For `ImageComposite` containers, two dedicated methods exist:

```csharp
// Lazy — marks for recreation, current snapshot stays visible until next draw
layout.InvalidateCacheWithPrevious();

// Immediate — destroys snapshot now, useful on hide/dispose
layout.DestroyRenderingObject();
```

---

## Cache Sharing

When many instances of the same control type appear on one Canvas (e.g. 50 divider lines, repeated icons), each normally allocates its own `CachedObject`. `CacheSharing=Shared` collapses all of them to a single shared entry stored in `Canvas.Cache`.

**Eligible cache types:** `Operations`, `Image`, `GPU`. (`OperationsFull` and composite types are excluded.)

### Usage

```csharp
public class DividerLine : SkiaShape
{
    public DividerLine()
    {
        Type = ShapeType.Rectangle;
        HeightRequest = 1;
        HorizontalOptions = LayoutOptions.Fill;
        BackgroundColor = Colors.Gray;
        UseCache = SkiaCacheType.Image;
        CacheSharing = CacheSharingType.Shared;
    }
}
```

First instance to render creates the `CachedObject` and stores it in `SuperView.Cache`. Every subsequent instance of the same type reads that entry — no re-render, no extra allocation.

### Invalidation in shared mode

Per-instance `InvalidateCache()` is intentionally bypassed — the shared snapshot is considered valid for all peers. To force a re-render for every instance of a type:

```csharp
myCanvas.Cache.Free<DividerLine>();        // by generic type
myCanvas.Cache.Free(typeof(DividerLine));  // by Type reference
myCanvas.Cache.Free();                     // evict everything
```

Disposing one instance does **not** clear the shared entry. The entry lives until the Canvas disposes or you call `Cache.Free(...)`.

### When to use

Good fit: controls that are visually identical across all instances and change infrequently — separators, rule lines, repeated decorative icons.

Poor fit: controls whose appearance differs per instance (different text, colors, images) — all instances would render as a clone of the first.

---

## Resource Management (DisposeManager)

Render objects must not be disposed mid-frame — GPU operations and background threads may still reference them. DrawnUI queues disposals through `DisposeManager`, which flushes them safely at the end of each frame.

Always use:

```csharp
control.DisposeObject(resource);  // safe, deferred disposal
```

Never call `.Dispose()` directly on a `CachedObject`, `SKSurface`, or `SKPicture` obtained from the rendering pipeline — let `DisposeManager` handle it.

---

## ImageComposite Internals

`ImageComposite` (and its GPU variant) maintains a single off-screen surface. On each frame it:

1. Calls `SetupRenderingWithComposition` to compute dirty child regions into `DirtyChildrenInternal`.
2. Erases only those regions on the surface.
3. Repaints background (`PaintTintBackground`) for dirty areas.
4. Redraws only dirty children via the layout-specific draw path (`DrawStack`, `DrawChildrenGrid`, `RenderViewsList`).

`RenderObjectPrevious` acts as a wrapper: each draw replaces the wrapper reference but reuses the same underlying surface, avoiding a full re-allocation.

**Child invalidation hooks:**
- `OnChildAdded` — invalidates previous cache
- `OnChildRemoved` — invalidates previous cache when `NeedAutoSize` is true

---

## Performance Tips

- Cache as high up the tree as the content stability allows. A stable header cached once at the container level costs far less than caching each child label individually.
- On a live canvas (camera preview, animated background, video), even small but visually stable overlay controls benefit from cache — the subtree is blitted instead of rebuilt every frame.
- For controls with `IsParentIndependent=true` and explicit `HeightRequest`, property changes do not force parent re-measure — useful for status labels inside auto-sized containers.
- `Repaint()` instead of `Update()` when only position or transform changed — it preserves the cache.
