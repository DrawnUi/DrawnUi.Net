# ImageComposite Cache Notes

`SkiaCacheType.ImageComposite` (and its GPU variant `ImageCompositeGPU`) is a caching strategy that enables partial cache updates 
by erasing and redrawing only dirty regions while preserving unchanged areas. 
This provides performance for complex layouts where only specific children change 
while others remain static.

`RenderObjectPrevious` is replaced on every draw as a wrapper but it reuses same surface 
as the previous one was using, erasing/drawing only dirty areas on that surface.

When something changes inside `UseRenderingObject` we would return false, 
also we would check if `RenderObjectPreviousNeedsUpdate` then kill `RenderObjectPrevious` inside.

Upon exiting `UseRenderingObject` we create a new `RenderObject` but the used surface 
would come from `RenderObjectPrevious` if existing.

## Implicated Methods

### Invalidating

* `OnChildAdded` and `OnChildRemoved` mark the child dirty (`TrackChildAsDirty`) when the cache is composite
* `OnChildrenChanged` then calls `Invalidate()` if `NeedAutoSize`

### Rendering

* `SetupRenderingWithComposition` called from `Paint` will:
  1. Fill `DirtyChildrenInternal` with calculated dirty regions
  2. Erase dirty regions
* `PaintTintBackground` will paint background in dirty areas
* Every kind of layout type draw dirty children with its own logic when `IsRenderingWithComposition` is `true`:
  1. `Absolute` with `RenderViewsList`
  2. `Grid` with `DrawChildrenGrid`
  3. Stack-like with `DrawStack`

## Invalidation Methods

### `InvalidateCacheWithPrevious()`
```csharp
public virtual void InvalidateCacheWithPrevious()
{
    InvalidateCache();

    if (IsCacheComposite) // ImageComposite or ImageCompositeGPU
    {
        RenderObjectPreviousNeedsUpdate = true;
    }
}
```

### `DestroyRenderingObject()`
```csharp
public void DestroyRenderingObject()
{
    RenderObject = null;
    RenderObjectPreviousNeedsUpdate = true;
}
```

### Key Differences

**`InvalidateCacheWithPrevious()`:**
- Calls `InvalidateCache()` (sets `RenderObjectNeedsUpdate = true`)
- Only sets `RenderObjectPreviousNeedsUpdate = true` for composite cache types (`ImageComposite`, `ImageCompositeGPU`)
- Conditional logic based on cache type

**`DestroyRenderingObject()`:**
- Immediately destroys current `RenderObject` (sets to null)
- Always sets `RenderObjectPreviousNeedsUpdate = true`
- Unconditional destruction

Two methods are needed because:
- `InvalidateCacheWithPrevious()` = "Mark for recreation" (lazy invalidation)
- `DestroyRenderingObject()` = "Destroy immediately" (immediate cleanup)

**`InvalidateCacheWithPrevious()` is used when:**
- Measured size changes, or `NeedMeasure` is set (`InvalidateInternal`)
- Layout changes but rendering continues
- Want to preserve current cache until next draw

**`DestroyRenderingObject()` is used when:**
- Control with a GPU cache becomes invisible (`OnVisibilityChanged`)
- Measure is invalidated (`InvalidateMeasureInternal`), except for `ImageDoubleBuffered`
- Immediate memory cleanup needed
- Cache is completely invalid and shouldn't be used
