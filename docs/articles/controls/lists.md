---
title: Scrolling Lists
description: Everything about DrawnUI templated scrolling lists — how they work, which strategy fits your content, recycling, big data sources and LoadMore.
categories: [MAUI, DrawnUI]
tags: [drawnui, skiasharp, recycled-cells, virtualization, lists, performance]
---

# Scrolling Lists

Lists are where drawn UI pays off the most: instead of hundreds of native views, a DrawnUI list is a `SkiaScroll` hosting one templated `SkiaLayout` that draws only what's on screen — and can cache, recycle and pre-prepare cells in ways native list controls can't.

This is the general guide: how the pieces fit, which setup matches your content, and how lists behave with big data sources.

## Anatomy

```xml
<draw:SkiaScroll LoadMoreCommand="{Binding CommandLoadMore}" LoadMoreOffset="200">
    <draw:SkiaLayout
        Type="Column"
        ItemsSource="{Binding Items}"
        RecyclingTemplate="Enabled"
        MeasureItemsStrategy="MeasureFirst"
        Virtualisation="Enabled">
        <draw:SkiaLayout.ItemTemplate>
            <DataTemplate>
                <views:MyCell />
            </DataTemplate>
        </draw:SkiaLayout.ItemTemplate>
    </draw:SkiaLayout>
</draw:SkiaScroll>
```

- **`SkiaScroll`** owns the viewport: offset, gestures, inertia, `LoadMoreCommand`, programmatic scrolls (`ScrollToIndex`).
- **The templated `SkiaLayout`** (usually via `SkiaStack`) owns the items: measurement, cell creation/recycling, virtualization.
- **The cell** is any control created from `ItemTemplate` — for recycling scenarios derive from `SkiaDynamicDrawnCell` (see [Recycled Cells](../advanced/recycled-cells.md)).

## The two main knobs

**`RecyclingTemplate`** — who owns cell instances:
- `Disabled`: one view per item, kept alive and bound. Fast revisits, memory grows with item count.
- `Enabled`: a small pool of cells is re-bound as items scroll in and out. Memory stays flat for infinite feeds; each appearing cell pays a re-bind.

**`MeasureItemsStrategy`** — when items get measured:
- `MeasureAll`: everything up-front. Simple and exact; startup cost grows with count.
- `MeasureFirst`: measures ONE cell, positions every row arithmetically from that size. Zero per-item measuring — but requires truly uniform row heights.
- `MeasureVisible`: measures what's on screen now, the rest in background batches. Instant startup with thousands of uneven items; content size refines as measurement progresses.

## Which setup for which content

| Your content | Recipe |
|---|---|
| **1. Small static list** — every item keeps its own view, like MAUI `BindableLayout.ItemTemplate` | `MeasureAll` + `RecyclingTemplate="Disabled"` |
| **2. Many uniform-height rows, large cells** — card feeds, few visible per screen | `RecyclingTemplate="Enabled"` + `MeasureFirst` |
| **3. Many uniform rows, small height** — phone book, dozens visible per screen | `SkiaCachedStack` |
| **4. Uneven rows, small height** | `SkiaCachedStack` |
| **5. Uneven rows, medium-large height** — social feed, chat | `MeasureVisible` + `RecyclingTemplate="Enabled"`, plain `SkiaStack` |

Why each wins:

**1 — Small static list.** Everything measured once, every item keeps its bound, cached view. No recycling churn at all — affordable exactly because the list is small.

**2 — Uniform large cells.** `MeasureFirst` turns layout into arithmetic: adds, removes and scrolling cost no measurement. Recycling keeps memory flat. Hard requirement: heights must be **truly uniform** — if any cell can differ, use `MeasureVisible`.

**3 / 4 — Many small rows.** With dozens of cells visible, even cheap per-cell draws add up every frame. `SkiaCachedStack` records the viewport (± one viewport of overscan) into a single cached plane and *blits* it while scrolling, re-recording per half-viewport of drift. Internally it runs `MeasureVisible` with the prepared-views pipeline — cells are bound and measured off the render thread, ahead of the scroll — so uneven heights (case 4) work natively.

Two knobs control that rhythm, and they work as a pair:

| Property | Default | Meaning |
|---|---|---|
| `VirtualisationInflatedRatio` | `1.0` | **Band size.** The plane covers the viewport ± this many viewports. |
| `PlaneRefreshRatio` | `0.5` | **Drift before a re-record**, as a fraction of viewport height. |

Keep `PlaneRefreshRatio` below `VirtualisationInflatedRatio`. The plane can only be blitted while it still covers the visible viewport, so a drift equal to the band ratio exhausts the coverage exactly and the frame falls back to drawing cells live. The defaults re-record after half a screen of scrolling with half a screen of coverage still in hand. Raise both together for fewer, larger records; lower them for more frequent, cheaper ones.

To check the plane is doing its job, enable the canvas debug string: it prints `plane [top..bottom] valid=True` when a plane is installed, or `plane none` when every frame is still drawing cells live. A second tell is `drawn X-Y` — blit frames never re-run the stack draw, so those indices stay frozen while the content moves. If `drawn` changes on every few pixels of scrolling, you are not blitting.

**5 — Uneven medium-large cells.** Few cells visible means per-cell caching already carries the frame; a band plane would add little. This is the [News Feed Tutorial](../news-feed-tutorial.md) configuration.

## Big data sources

You can bind a huge in-memory list directly: when a templated `ItemsSource` exceeds `SkiaLayout.WindowSourceThreshold` (300 by default), the layout materializes only a bounded window of items and slides it internally as you scroll. Measurement, cell pools and structures all scale with the window, not with your list.

`LoadMoreCommand` keeps its meaning: it fires only when the user reaches the true end of your source data — "paging within already-available data" is internal, "fetch more from the API" is yours.

## Programmatic scrolling

`SkiaScroll.ScrollToIndex(index, animate)` speaks your data's indices — with the built-in window engaged, jumping to a non-resident item rebases the window around the target automatically.

## Going deeper

- [Recycled Cells: Advanced Performance Techniques](../advanced/recycled-cells.md) — cell design, `SkiaDynamicDrawnCell`, per-layer caching inside cells.
- [SkiaScroll & Virtualization](../advanced/skiascroll.md) — the scroll side in detail.
- [News Feed Scroller Tutorial](../news-feed-tutorial.md) — full worked example for case 5.
- [Beyond RecyclerView](https://taublast.github.io/posts/RecycledCells/) — deep-dive: a windowed, inverted chat with recycled cells and a band-plane cache.
