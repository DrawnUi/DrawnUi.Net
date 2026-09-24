# Drag to Reorder a List

A templated list the user reorders by dragging a row, with the list moving in place under the pointer and the row gliding into its slot on release. Nothing in DrawnUI is a "reorderable list" control: the recipe is a `SkiaScroll` + templated `SkiaLayout`, an `ObservableCollection`, gestures on a grip, and one floating copy of the row. Full source: `src/Maui/Samples/HelloMaui/Pages/ReorderPage.cs` + `ReorderCell.cs` (the React demo has the same page).

## The idea in one picture

```
┌ page (SkiaLayer) ──────────────────────────────┐
│ ┌ SkiaScroll ─────────────────────────────────┐ │
│ │ SkiaStack ItemsSource=items (recycled cells)│ │
│ │   row 0                                     │ │
│ │   row 1                                     │ │
│ │   ░░░░░░░ ← the dragged row, drawn blank    │ │   ← the gap IS the drop slot
│ │   row 3                                     │ │
│ └─────────────────────────────────────────────┘ │
│ ┌ overlay SkiaLayer, InputTransparent ────────┐ │
│ │  [ ghost: a copy of the row under pointer ] │ │   ← follows the finger, outside the scroll's clip
│ └─────────────────────────────────────────────┘ │
└────────────────────────────────────────────────┘
```

Every step of the drag is one `ObservableCollection.Move(from, to)`. The layout applies a `Move` to the cells it already has (contexts are re-bound, templates and measurements are kept), so the list reorders live without a rebuild. The row that is being dragged draws itself with `Opacity = 0`; the hole it leaves is where it will land.

## The pieces

**The list.** A `SkiaStack` with `ItemsSource`, `ItemTemplate`, `RecyclingTemplate = Enabled` and `MeasureItemsStrategy = MeasureFirst` inside a vertical `SkiaScroll`. `MeasureFirst` (uniform rows) is what makes a `Move` a pure re-bind: no structure arithmetic at all. `MeasureVisible` takes the structure-preserving path too; `MeasureAll` rebuilds.

**The cell.** A `SkiaDynamicDrawnCell` with a grip area. `SetContent(ctx)` binds the visuals and sets `Opacity` to 0 when this item is the one being dragged, 1 otherwise. The grip gets `ConsumeGestures` and owns the whole drag:

- `Down`: remember the item's index, take the pan away from the scroll with `scroll.RespondsToGestures = false` (a vertical pan inside a vertical scroll belongs to the scroll otherwise), tell the page to lift the row, start a per-frame ticker.
- `Panning`: accumulate travel in points (`Distance.Delta.Y / RenderingScale`), `e.Consumed = true`.
- `Up` / `Tapped`: tell the page to drop, stop the ticker, give the scroll its gestures back. Released outside the viewport = cancel, move the item back to where it was picked up.

**The ticker** (a looping `SkiaValueAnimator`, runs every frame while dragging): while the pointer rests within ~44 pt of the top or bottom edge, `scroll.ScrollTo(x, y ± step, 0, true)` keeps the list moving. Travel is counted in content space: the pointer's own movement plus whatever the list scrolled underneath it (`_lastOffset - ViewportOffsetY`). Every time `|travel| >= rowHeight + spacing` the host moves the item one slot and the travel is reduced by one stride.

**The page (host).** Owns the collection, the scroll and the ghost, and exposes a small interface to the cells (`Move`, `Lift`, `Carry`, `Drop`, `Dragging`, `Spacing`, `Scroll`):

- `Lift`: find the row's `DrawingRect` with `layout.ChildrenFactory.GetCellInUseOrNull(index)` (recycled cells are NOT in `Views`, the adapter holds the realized rows; `GetCellsInUse()` lists them all), size and place the ghost over it in overlay coordinates, show it, re-apply every live cell's content so the lifted row goes blank.
- `Carry(pointerY)`: `ghost.Top = pointer - grabOffset`, `ghost.Update()`. `Left`/`Top` move a cached control without a re-layout.
- `Drop(index)`: a short eased animation (a second `SkiaValueAnimator`, ~140 ms) glides the ghost from where it is to the slot's current `DrawingRect.Top`, re-read every frame because the list is still catching up with the last `Move`. Then hide the ghost and re-apply the cells so the real row draws again.

## Why a ghost instead of dragging the cell itself

A cell lives inside the scroll, which clips and moves it, and re-binds it on recycling. A copy in an overlay `SkiaLayer` above the scroll, `InputTransparent = true` so it never eats the gesture it is showing, is free of all that: it follows the pointer in page coordinates, casts a shadow over the list, and the list stays a plain data-bound list the whole time.

## Minimal cell gesture handler

```csharp
private void OnGrip(object sender, SkiaGesturesInfo e)
{
    var type = e.Args.Type;
    if (type == TouchActionResult.Down)
    {
        _dragIndex = _host.IndexOf((ReorderItem)BindingContext);
        e.Consumed = _dragIndex >= 0;
        if (!e.Consumed) return;
        _stride = MeasuredSize.Units.Height + _host.Spacing;
        _scroll = _host.Scroll;
        _scroll.RespondsToGestures = false;            // the pan is ours until Up
        _host.Lift((ReorderItem)BindingContext, _dragIndex, e.Args.Event.Location.Y);
        _ticker = new SkiaValueAnimator(this) { mMinValue = 0, mMaxValue = 1, Speed = 1000, Repeat = -1, OnUpdated = _ => Tick() };
        _ticker.Start();
        return;
    }
    if (_dragIndex < 0) return;
    if (type == TouchActionResult.Panning)
    {
        _pointerY = e.Args.Event.Location.Y;
        _travel += e.Args.Event.Distance.Delta.Y / RenderingScale;
        e.Consumed = true;
        return;
    }
    if (type is TouchActionResult.Up or TouchActionResult.Tapped)
    {
        _host.Drop(_dragIndex);
        EndDrag();                                     // ticker off, scroll.RespondsToGestures = true
    }
}

private void Tick()
{
    _host.Carry(_pointerY);
    while (Math.Abs(_travel) >= _stride)
    {
        var step = Math.Sign(_travel);
        if (!_host.Move(_dragIndex, _dragIndex + step)) { _travel = 0; break; }
        _dragIndex += step;
        _travel -= step * _stride;
    }
}
```

`Move` on the host is just `items.Move(from, to)` with a bounds check. Edge auto-scroll and the "released outside = cancel" rule are in the sample.

## Rules that matter

- Reorder through the collection (`ObservableCollection.Move`), never by touching `Views` or `Children` of a templated layout. To reach the realized cells (refresh a look, read a rect) use `ChildrenFactory.GetCellInUseOrNull(index)` / `GetCellsInUse()`; a templated layout's `Views` is empty.
- `MeasureFirst` + `RecyclingTemplate.Enabled` for the live reorder; uniform row height is the assumption behind the stride arithmetic.
- Take the scroll's gestures only for the length of the drag (`RespondsToGestures`), and always give them back, including from `OnDisposing`.
- The cell knows nothing about the ghost; the page draws it in an `InputTransparent` overlay from the cell's `DrawingRect`.
- Buttons that reorder from code ("move 1st below 10th", "reverse") go through the same collection: a `Move` for a single item, a new collection assigned to `ItemsSource` for a bulk change.
