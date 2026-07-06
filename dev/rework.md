What Telegram Android actually does (open source, verifiable): one ChatMessageCell = a single custom View that draws everything itself on canvas — no view hierarchy per message. The killer move is MessageObject: text StaticLayouts, spans, heights — ALL precomputed off-UI before the message ever reaches the list. So their recycling pool is tiny (viewport-sized), because rebind costs almost nothing — binding = swapping references to precomputed layout objects; measure = returning a precomputed number. Scrolling redraws nothing — cells sit in display lists (Android RenderNodes), the platform composites.

Where we're honestly weaker:
1. They made rebind cheap; we made rebind avoidable. Theirs is the more fundamental fix. Our retained 200-cell set, eviction rules, musical-chairs pool sizing, stale-serve — all of it is the price of dodging a rebind we never made cheap. Root: measure+bind of a DrawnUI cell re-shapes text and walks the tree; in Debug one measure > a frame.
2. Complexity budget. Their model: precompute, cheap rebind, small pool — done. Ours: prepared views + memo + context pool + band plane + double buffer + gates + gen guards. This chat's history (walls, stale trees, stuck spinners) IS the bill for that complexity.

Where we're NOT weaker: their display lists = our per-cell operation caches — same idea, we have it. The band plane goes further than they do (they don't need it — native composits cheap; our per-frame C# traversal isn't free, plane fixes that). End result measured butter on weak hardware, memory bounded. And our cells stay composable layouts, not hand-written draw code — that's a real DX advantage they gave up.

The Telegram-shaped upgrade path for us (this is the interesting one): move the precompute into the DATA layer — shaped text layouts, spans, measured heights cached per message (we memo heights already; text shaping we don't). If SetContent becomes reference-swapping precomputed layouts, rebind becomes cheap, and then RecyclingTemplate.Enabled with a viewport-sized pool becomes viable — the 225ms rebind measurement would collapse, memory drops 10×, and half the retained-cell machinery becomes optional rather than mandatory.

So: current implementation = good measured outcome, structurally more brittle than Telegram's. The precomputed-layout direction is the real next architecture step, not more plane tuning. Worth a design session if you want it.

The proper layer map:

┌──────────────────────────────────────────────────────┬─────────────────────────────────────────────────────────────┐
│                       Telegram                       │                        DrawnUI chat                         │
├──────────────────────────────────────────────────────┼─────────────────────────────────────────────────────────────┤
│ MessageObject — retained PER MESSAGE in the loaded   │ our ChatCell — retained per message in the window, holds    │
│ window, holds StaticLayouts (shaped text), computed  │ measured size, arranged subtree, ops/image cache. Never     │
│ heights, spans. Never recycled.                      │ recycled (within window).                                   │
├──────────────────────────────────────────────────────┼─────────────────────────────────────────────────────────────┤
│ ChatMessageCell — native View, recycled              │                                                             │
│ viewport-sized pool, rebind = re-point at a          │ doesn't exist — no native views, nothing to pool            │
│ MessageObject                                        │                                                             │
├──────────────────────────────────────────────────────┼─────────────────────────────────────────────────────────────┤
│ layout generation pass (background)                  │ CellPreparationService (background bind+measure+bake)       │
├──────────────────────────────────────────────────────┼─────────────────────────────────────────────────────────────┤
│ loaded message window, paged, far messages dropped   │ WindowedSource cap+trim                                     │
└──────────────────────────────────────────────────────┴─────────────────────────────────────────────────────────────┘

So Telegram does NOT recycle the thing that matters — MessageObjects live one per loaded message, exactly like our cells. Their recycling layer exists ONLY because an Android View is expensive to have; they pool the expensive shell and retain the real per-message state. We deleted the shell entirely, so our "no recycling" IS their "no recycling for MessageObject". Their famous cheap rebind is cheap precisely because it just re-points a pooled View at retained per-item state — an indirection we don't need.

So the answer to your previous question flips: the ARCHITECTURE doesn't suck — it's Telegram's architecture with the forced native layer removed. Same windowing, same background precompute, same per-item retention.

What honestly remains different, layer-for-layer:

1. Retained weight per item: their MessageObject = lean layout objects; our retained unit = full control subtree + gesture wiring + cache surfaces. Same role, fatter object. That's the real optimization axis if ever needed — slimmer retained cells, not recycling.
2. Their cell draws everything in one hand-written onDraw; ours composes controls — equal after our ops cache records, but our first bake walks a real tree. We pay it off-thread, so mostly moot.

And this reframes the article/community story nicely: not "we skip recycling", but "we retain per message exactly what Telegram retains per message — we just don't need the View pool they recycle above it".

Theory session. The retained unit today = full control subtree (~50-200 managed objects: bindables, gesture wiring, dirty tracking, parent refs) + measured rects + cache surfaces. The insight from the Telegram mapping: only the LAST TWO actually need to live per item. The tree is just the machine that produces them. Directions, ordered by how deep they cut into the engine:

1. Detached render blobs — "freeze-dried cells" (the big one). After prepare (bind+measure+bake), distill the cell into a lean retained artifact keyed by the data item: { measured size, ops picture or image, hit-rect map }. Kilobytes, mostly native memory. The live control tree stops being per-item and becomes a small hydration pool (~2 screens, Telegram's View pool reborn) used only to regenerate blobs when the window slides or an item mutates. DrawStack renders a blob at its slot with a RenderTree entry, no realized view at all. The killer fact: the engine is already 80% there without knowing it — the measure memo IS retained sizes without views; stale-serve already draws a RenderObject for a cell whose view is invalid; the skeleton path already puts slot-entries into the render tree for viewless cells; the bake already blits caches without touching live state. The missing 20% = let CachedObject ownership move from the control to the adapter (item-keyed), so pixels legally outlive their view. GC impact: 200 object trees → ~8 trees + 200 pictures the GC barely sees.

2. Hit-map gestures for blobs. Bake captures child hit rects → tap on a blob dispatches data-level (OnItemTapped(msg, region)) instead of control-level. Anything needing live state (editor focus, video) hydrates a real tree into the slot on first touch — like web hydration. We literally built the coordinate machinery for this yesterday (bake-deposited tree).

3. Shaped-text cache — the actual StaticLayout equivalent. SkiaLabel's measure cost is HarfBuzz shaping + line breaking. Global cache keyed (text, style, width) — measure becomes lookup. This is the piece that makes REBIND cheap rather than avoidable, so it helps both worlds: blob regeneration gets fast, and classic RecyclingTemplate.Enabled suddenly becomes viable for chat too. Standalone win even if nothing else ships.

4. Frozen subtrees — property-system diet. Post-prepare, freeze the cell: collapse per-property change tracking into one version stamp, compact bindable storage into an array, drop handlers. Unfreeze on mutation. WPF Freezable, but for drawn trees. Biggest surgery, least urgent — blobs (1) mostly obsolete it because frozen trees stop being retained at all.

Order I'd take: 3 → 1 → 2 → (4 probably never). Shaped-text cache is low-risk and immediately measurable; blobs ride on mechanisms that already exist and are harness-proven; hit-maps complete the story.

The honest costs: invalidation choreography (item changed → discard blob → hydrate → re-prep — same cycle cells already run, but now with ownership transfer); two rendering paths to keep correct (blob vs hydrated) — though stale-serve vs live is already that split; and debugging gets more abstract ("why does this message render" no longer answerable by inspecting a control instance).

End state if all lands: retained per message ≈ what Telegram retains (layouts + pixels + rects), live trees ≈ their View pool, and the chat window cap could grow 5-10× on the same RAM. Cells implementation stops "sucking" by your own metric — same architecture, lean layer.

Changes inside drawnui lib:

ApplySpan.cs
IDrawnTextSpan.cs
LineGlyph.cs
LineSpan.cs
SkiaLabel.EmojiData.cs
SkiaLabel.GlyphMeasurementCache.cs
SkiaLabel.Line.cs
SkiaLabel.ObjectPools.cs
SkiaLabel.SpanMeasurement.cs
SkiaLabel.cs
BuildRowLayout.cs
CellPreparationService.cs
CellWIthHeight.cs
ContentLayout.cs
DataContextIterator.cs
ElementRenderer.cs
IRenderObject.cs
InfiniteLayout.cs
LayoutStructure.cs
RenderLabel.cs
RenderObject.cs
RenderTreeRenderer.cs
SkiaCachedStack.cs
SkiaControlWithRect.cs
SkiaLayout.BuildWrapLayout.cs
SkiaLayout.ColumnRow.cs
SkiaLayout.Grid.Cell.cs
SkiaLayout.Grid.Structure.cs
SkiaLayout.Grid.cs
SkiaLayout.IList.cs
SkiaLayout.ListView.cs
SkiaLayout.Render.Shared.cs
SkiaLayout.Shared.cs
SkiaLayout.StackWrap.Shared.cs
SkiaLayout.Wrap.cs
SnappingLayout.cs
StackLayoutStructure.cs
TemplatedViewsPool.cs
ViewsAdapter.cs
ViewsIterator.cs
CachedGradient.cs
CachedObject.cs
CachedShader.cs
CachedShadow.cs
RenderOnTimer.cs
RenderingSubTree.cs

● Bash(head -60 /c/Dev/Cases/GitHub/DrawnUi/src/Shared/DrawnUi/Draw/Text/SkiaLabel.GlyphMeasurementCache.cs)
namespace DrawnUi.Draw
{
    public partial class SkiaLabel
    {
        private static class GlyphMeasurementCache
        {
            private const int MaxCacheSize = 2000;

            private struct CacheKey : IEquatable<CacheKey>
            {
                public string TypefaceFamilyName;
                public SKFontStyle TypefaceStyle;
                public bool NeedsShaping;
                public string Text;

                public bool Equals(CacheKey other) =>
                    TypefaceFamilyName == other.TypefaceFamilyName &&
                    TypefaceStyle.Equals(other.TypefaceStyle) &&
                    NeedsShaping == other.NeedsShaping &&
                    Text == other.Text;

                public override bool Equals(object obj) => obj is CacheKey ck && Equals(ck);

                public override int GetHashCode()
                {
                    unchecked
                    {
                        int hash = 17;
                        hash = hash * 23 + (TypefaceFamilyName?.GetHashCode() ?? 0);
                        hash = hash * 23 + TypefaceStyle.GetHashCode();
                        hash = hash * 23 + NeedsShaping.GetHashCode();
                        hash = hash * 23 + (Text?.GetHashCode() ?? 0);
                        return hash;
                    }
                }
            }

            private static readonly Dictionary<CacheKey, LinkedListNode<(CacheKey Key, float Width, LineGlyph[] Glyphs)>> _cache
                = new Dictionary<CacheKey, LinkedListNode<(CacheKey, float, LineGlyph[])>>();

            private static readonly LinkedList<(CacheKey Key, float Width, LineGlyph[] Glyphs)> _lruList
                = new LinkedList<(CacheKey, float, LineGlyph[])>();

            // Optional: Add a lock object for thread safety
            private static readonly object _lock = new object();

            public static bool TryGetValue(SKTypeface typeface, bool needsShaping, string text, out (float Width, LineGlyph[] Glyphs) result)
            {
                var key = new CacheKey
                {
                    TypefaceFamilyName = typeface.FamilyName,
                    TypefaceStyle = typeface.FontStyle,
                    NeedsShaping = needsShaping,
                    Text = text
                };

                lock (_lock)
                {
                    if (_cache.TryGetValue(key, out var node))
                    {

File-mapped plan, three tiers, ordered. (Notable: glyph-run cache already exists — SkiaLabel.GlyphMeasurementCache.cs, static LRU keyed typeface+text → widths/glyphs. Missing level above it = paragraph layout.)

Tier A — paragraph layout cache (Telegram StaticLayout equivalent, standalone win):
- NEW Draw/Text/SkiaLabel.LayoutCache.cs — static LRU keyed (text, font family+style+size, spans hash, maxWidth) → line-break result (Line[]/spans + measured size). Sits ABOVE the existing glyph cache: hit = whole Measure becomes a lookup.
- Draw/Text/SkiaLabel.cs + SkiaLabel.SpanMeasurement.cs — measure path consults the cache before line breaking; store on miss. Thread-safe (prep worker already measures off-thread).
- Effect: rebind/remeasure collapses → recycling mode viable, prep worker faster, memo misses cheaper.

Tier B — detached render blobs (retained pixels outlive views):
- Internals/Models/CachedObject.cs — ownership transfer: adapter-owned flag so a blob survives the control's cache disposal; dispose rules per the SIGSEGV lesson (render-thread/deferred only).
- Draw/Base/SkiaControl.Cache.cs — export API: hand the current RenderObject out (control keeps blitting it until invalidated); DestroyRenderObject paths respect detached ownership.
- Draw/Layout/ViewsAdapter.cs — the heart: item-keyed blob registry (size + CachedObject + hit map). Filled from prepare completion and HARVESTED at eviction (today an evicted tagged cell's bake is lost — instead keep the pixels, drop the tree). IsViewPrepared counts a valid blob as prepared. Pool shrinks toward hydration-pool size.
- Draw/Layout/CellPreparationService.cs — after bind+measure of pool-owned views, trigger the bake and register the blob.
- Draw/Layout/SkiaLayout.ColumnRow.cs — DrawStackVisibleChildren third branch: no realized view + blob exists → draw blob at slot + RenderTree slot entry (mechanically = stale-serve without an owner + skeleton's slot-entry; both proven). Painted-bounds tracking unchanged.
- Draw/Layout/SkiaLayout.ListView.cs — prep wants/horizon accounting over blobs (don't demand realization of blob-covered indices).
- Draw/Layout/SkiaCachedStack.cs — CellRecordReady accepts blob-backed slots (bake already draws whatever DrawStack draws).
- Draw/Layout/TemplatedViewsPool.cs — _byContext reservoir keeps only live/hydrated cells; budget rules relax.

Tier C — blob gestures + hydration:
- Internals/Models/SkiaControlWithRect.cs (or new model) — tree entry variant carrying a hit map (child rect + semantic id list, captured at bake).
- Draw/Base/SkiaControl.Shared.cs — gesture walk: entry without live control but with hit map → dispatch item-level event instead of child dispatch.
- Draw/Layout/SkiaLayout.ListView.cs — public ItemGesture event (item, region, args); hydration trigger: gesture needing live state → realize view for index via existing adapter machinery, swap into slot next frame.

Order: A alone first (measurable via harness + your 225ms rebind number re-test). Then B behind an opt-in flag (RetainRenderObjects style), C last. Each tier independently shippable; B is where the 10× RAM win lives; A is where rebind stops hurting. Biggest risk concentration: ViewsAdapter ownership/disposal in B — that's where the harness needs new repros (evict-harvest, blob invalidation on item change, window-slide reuse).

These aren't list features, they're a change of the engine's rendering model. Layer by layer:

Tier A (paragraph layout cache) is inherently global: it's a static cache under SkiaLabel.Measure. Every label everywhere — navbars, buttons, pickers — gets lookup-priced measure. Nothing about it is cells-specific; cells were just where the pain was measured.

Tier B (detached render objects) lives in SkiaControl.Cache.cs — the base class. Once pixels can be owned outside their control, the same mechanism serves: page/tab switching (blit the cached page back instead of re-rendering), carousel pages, reopened dialogs, collapsed/expanded panels, ANY subtree that comes and goes with stable identity. The general contract is "rendered output keyed by semantic identity, controls are the recipe that produces it". Which forces engine-level answers, not cells-local ones: ONE ownership protocol (who disposes, on which thread), ONE invalidation protocol (identity's data changed → blob dies), and a GLOBAL memory budget/eviction — you can't have five features independently hoarding pictures.

Tier C (hit-maps) generalizes the render tree: entries stop requiring live controls. That's interactive cached content anywhere — today a cached-and-released subtree is dead to gestures; with hit-maps it isn't.

Full picture: this walks DrawnUI toward the control-tree / render-tree separation that Flutter (Widget→RenderObject) and WPF (visual layer) made explicit. Controls = lightweight recipes; retained scene = render objects + hit maps. The pieces already exist half-formed — RenderObject, RenderTree, memo, stale-serve — the work is promoting them from cells-pipeline internals to engine citizens with contracts.

Practical consequence for the plan: design the Tier B ownership/budget API as SkiaControl-level from day one (even if cells are the first consumer), or we'd be rewriting a cells-shaped hack later. Rollout stays opt-in per feature, but the API surface must be general immediately. Cells then become just the first — and hardest — proving ground, which they've already been for everything else in this engine.