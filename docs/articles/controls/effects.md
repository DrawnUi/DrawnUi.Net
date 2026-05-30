# Visual Effects

Every `SkiaControl` exposes a `VisualEffects` collection. Effects modify how a control is painted — drop shadows, glow, color filters, multi-pass shader chains — without changing its layout.

```xml
<draw:SkiaShape Type="Rectangle" CornerRadius="16" BackgroundColor="White">
    <draw:SkiaShape.VisualEffects>
        <draw:DropShadowEffect Blur="8" X="0" Y="4" Color="#40000000" />
    </draw:SkiaShape.VisualEffects>
</draw:SkiaShape>
```

## Effect kinds

`VisualEffects` accepts any `SkiaEffect`. The control sorts them by the interface they implement:

| Interface | Applied as | Examples |
|-----------|-----------|----------|
| `IImageEffect` | `SKImageFilter` on a `SaveLayer` | `DropShadowEffect`, `OuterGlowEffect` |
| `IColorEffect` | `SKColorFilter` on a `SaveLayer` | tint / color matrix effects |
| `IRenderEffect` | Wraps/chains the control's own paint | `ChainDropShadowsEffect` |
| `IPostRendererEffect` | Runs after the control is painted | `SkiaShaderEffect`, `SkiaBackdrop` |
| `IStateEffect` | Per-frame state update hook | animators |
| `ISkiaGestureProcessor` | Participates in gesture routing | interactive effects |

A single effect can implement several of these.

## Built-in shadow effects

- **`DropShadowEffect`** — single drop shadow. `Blur` (sigma), `X`/`Y` offset (points), `Color`.
- **`OuterGlowEffect`** — symmetric glow (drop shadow with no offset).
- **`ChainDropShadowsEffect`** — a `Shadows` collection of `SkiaShadow`, each drawn as its own pass. Use for multi-shadow elevation or neon stacks.

```xml
<draw:ChainDropShadowsEffect>
    <draw:SkiaShadow Blur="2" X="0" Y="1" Color="#30000000" />
    <draw:SkiaShadow Blur="16" X="0" Y="8" Color="#50000000" />
</draw:ChainDropShadowsEffect>
```

## Effects that paint beyond bounds (shadows, glow) + caching

This is the part most people hit. A drop shadow paints **outside** the control's `DrawingRect`. When a control is cached (`UseCache="Image"`/`GPU"`/etc.), the cache surface and the clip are normally the size of the control — so the shadow would be **clipped away**.

DrawnUI handles this automatically. Each effect declares how far it paints past the bounds, and the engine expands the cache surface, the presentation clip, and the dirty region by that amount. **You do not need `ExpandDirtyRegion` for built-in shadow/glow effects** — attach the effect and the shadow shows, cached or not.

How it works:

- `SkiaEffect.GetEffectMargin(float scale)` returns the per-side overflow in **pixels** (`Thickness`, default `Zero`).
- The control aggregates the per-side **max** across all `VisualEffects` into `EffectsMarginPixels` (cached; recomputed only when effects, their parameters, or rendering scale change).
- `GetEffectMargin` is invalidated automatically when an effect is **added, removed, replaced**, or when its parameters change — which also invalidates the cache so it rebuilds at the new size.

### The `3 * Blur` rule

Blur in these effects is a Gaussian **sigma**. The shadow margin is `3 * Blur` per side (plus the offset for `DropShadowEffect`). `3σ` is not a guess — it matches Skia's own blur image filter, which computes its output bounds as the input inflated by `ceil(3 * sigma)`. Past 3σ the shadow alpha is <0.3% (invisible). So the expanded cache equals the actual painted extent — no clipping, no wasted texture.

> SkiaSharp does not bind `SkPaint::computeFastBounds`, so DrawnUI computes the same extent in managed code from each effect's parameters. The result is identical to what `computeFastBounds` would return for Gaussian blur.

### `ExpandDirtyRegion` (manual override)

`ExpandDirtyRegion` (a `Thickness`, in points) still exists for cases the engine can't infer — custom drawing in `Paint` that bleeds past bounds, or a custom effect that doesn't report a margin. The final expansion is the per-side **max** of the auto effects margin and `ExpandDirtyRegion * scale`, so setting it never shrinks the shadow allowance.

### Performance

- Margin is cached; per-frame cost is reading a `Thickness` (no allocation, no native call).
- A cached control with a shadow uses a **larger cache texture** (a `3 * Blur` ring around it). This is unavoidable — the shadow needs pixels — and is the same cost you would pay setting `ExpandDirtyRegion` by hand.
- Animating `Blur`/offset rebuilds the cache each change (required for a correct shadow). For continuously animated shadows, consider whether that control needs to be cached at all.

## Writing a custom effect

Derive from the matching base (`BaseImageFilterEffect`, `BaseColorFilterEffect`, `BaseChainedEffect`) or `SkiaEffect` directly. **If your effect paints outside the control bounds, override `GetEffectMargin`** so the cache/clip expands to fit it:

```csharp
public class MyGlowEffect : BaseImageFilterEffect
{
    public double Radius { get; set; } = 10;

    public override SKImageFilter CreateFilter(SKRect destination) =>
        SKImageFilter.CreateBlur((float)Radius, (float)Radius);

    // Report how far the blur paints past the bounds so it is not clipped when cached.
    public override Thickness GetEffectMargin(float scale)
    {
        if (!NeedApply)
            return Thickness.Zero;

        var spread = Radius * 3.0; // 3-sigma for Gaussian blur
        return new Thickness(spread);
    }
}
```

Effects that stay inside the bounds (color filters, in-place shaders) inherit the default `Thickness.Zero` and need no override.

> Note: `Blur` in DrawnUI's shadow effects is treated as **pixels** (sigma), while offsets (`X`/`Y`) are in points and scaled by `scale`. Mirror that in custom effects: don't scale the sigma, do scale positional offsets.

## See also

- [Shapes](shapes.md) — `SkiaShape` has its own built-in `Shadows` for shape-aware shadows.
- [Drawing Pipeline](../drawing-pipeline.md) — how caching and `UseCache` work.
- [Shaders](../shaders.md) — `SkiaShaderEffect` / `SkiaBackdrop` post-render effects.
