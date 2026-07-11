using System.Collections.Specialized;

namespace DrawnUi.Draw;

/// <summary>
/// SkiaStack with a BAND-PLANE cache for smooth scrolling of templated lists: the visible viewport
/// (± one viewport of overscan) is recorded once into a cached plane and BLITTED on subsequent frames
/// instead of re-drawing every cell; the plane refreshes per half-viewport of scroll drift or on
/// invalidation. A plain stack pays a full per-cell paint EVERY frame. Trim re-anchoring keeps blits
/// pixel-stable across windowed-source mutations (LoadMore head/tail trims).
///
/// With <see cref="UseDoubleBuffering"/> the NEXT plane is recorded on a background worker while the
/// current one keeps blitting, so scroll frames never pay a record at all. The bake is a PURE-READ pass
/// (see SkiaLayout.IsPlaneBakePass) over a deep-frozen geometry snapshot, gated so it only runs when the
/// band's cells are fully prepared — pair with <see cref="SkiaLayout.UsePreparedViews"/> (required on
/// MAUI heads, where BindableObject storage is not thread-safe).
/// </summary>
public class SkiaCachedStack : SkiaStack
{
    /// <summary>
    /// FALSE: ONE foreground plane — the band is recorded synchronously on the render thread once per
    /// half-viewport of drift or per invalidation, and blitted in between.
    /// TRUE: TWO planes — the next plane is recorded OFF-thread while the current one keeps blitting, so
    /// scrolling never pays a render-thread record (the LoadMore/scroll record spike disappears). On MAUI
    /// heads this requires <see cref="SkiaLayout.UsePreparedViews"/> (the bake must be a pure cache-blit
    /// pass: no binds, no measures); without it the control silently behaves as single-plane there.
    /// </summary>
    public bool UseDoubleBuffering = true;

    public override bool UsePreparedViews
    {
        get
        {
#if BROWSER || WEB
            return true;
#else
            return true;
#endif
        }
    }


    public SkiaCachedStack()
    {
        UseCache = SkiaCacheType.None; // we own DrawDirectInternal + our own plane cache
        FastMeasurement = true; //one layout pass, not accounting for deeper in the tree Fill inside Auto size scenarios.

        // MeasureVisible activates the prepared-views pipeline (SkiaLayout.UsePreparedViews): cells are
        // bound+measured off-thread ahead of scrolling; the render thread NEVER measures a cell (kills
        // fling spikes), unprepared cells show their skeleton for a frame or two instead.
        MeasureItemsStrategy = MeasuringStrategy.MeasureVisible;

        // Overscan: record ± one viewport so the plane can be REUSED (blitted, not re-recorded) while
        // scrolling within the margin = smooth. Without it the plane covers exactly the viewport and every
        // scroll frame re-records (jerky). The completeness gates + coverage clamp keep the wider records
        // hole-free.
        VirtualisationInflatedRatio = 1.0;
    }

    /// <summary>Last scroll offset reported by the parent viewport (see OnViewportWasChanged).</summary>
    protected ScaledPoint ScrollOffset;

    public override void OnViewportWasChanged(ScaledRect viewport, ScaledPoint offset)
    {
        base.OnViewportWasChanged(viewport, offset);

        ScrollOffset = offset;
    }

    // Content-space Y range the current plane actually covers (verified span, see coverage clamp). Once the
    // viewport scrolls beyond this range the plane can't fill it -> re-record instead of blitting a hole.
    private float _foregroundCoveredTop, _foregroundCoveredBot;

    // Operations (SKPicture). A GPU surface plane was tried 2026-07-05 on a weak device (OnePlus 3):
    // constant 100-220ms frames (surface recreation + sync/readback per record) — far worse than the
    // picture replay. The off-thread compositor (UseDoubleBuffering=true) is the weak-device answer.
    private SkiaCacheType PlaneCacheType = SkiaCacheType.Operations;

    private SKPictureRecorder _recorder; // reused across record frames
    private bool _cacheValid;

    private float _recordOffsetY; // context.Destination.Top at the last record (band coverage origin)

    // invalidate the picture whenever the windowed ItemsSource changes (LoadMore/trim shifts content) — a
    // trim keeps count stable with stable indices, so a count check can't see it; the collection event can.
    protected volatile bool _contentChanged = true;

    // settled-frame probe for the skeleton heal (see DrawDirectInternal): same viewport offset for
    // consecutive frames = the scroll is at rest and unprepared visible cells will never self-heal.
    private float _settleProbeOffsetY = float.MinValue;
    private int _settledFrames;

    // The plane may hold cells baked as SKELETONS; set by the prep worker when a cell becomes ready.
    // The blit path then re-records (async: kicks a bake and KEEPS blitting — no live frame, no wait)
    // instead of serving the baked skeleton until the next half-viewport drift.
    private volatile bool _planeStale;

    // Did the CURRENT plane's record paint any skeletons? Captured at record/publish from the pass's
    // thread-static. When false, prep-completion staleness is meaningless (nothing to heal) and the
    // re-record is skipped — protects RecyclingTemplate.Disabled (frontier preps fire constantly but
    // its planes are always real content) from useless background bakes on weak devices.
    private volatile bool _planeHasSkeletons;

    /// <summary>
    /// Prep worker signal (see <see cref="SkiaLayout.OnCellPreparedOffthread"/>): a cell the current
    /// plane may hold as a baked skeleton is now ready — mark the plane stale so the next blit frame
    /// re-records the band with real content.
    /// </summary>
    protected override void OnCellPreparedOffthread()
    {
        _planeStale = true;
    }

    // Set by OnContentTranslatedVertically, consumed by the next draw: marks the ONE frame where cells are
    // translated but the parent scroll compensates only next frame — the only frame where a live draw shows
    // desynced content and the re-anchored previous plane must serve instead.
    private bool _translatedThisFrame;

    /// <summary>Mutation hook (LoadMore/trim/add/remove on the same collection) — invalidate the plane.</summary>
    protected override void OnItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
    {
        // JUMP transition: a full-collection Replace (windowed rebase) rebuilds the whole slice —
        // freeze the last presented plane before base processes it (see the hold in DrawDirectInternal)
        if (ForegroundPlane != null && IsFullCollectionReplace(args))
        {
            _transitionHold = true;
            _holdFrames = 0;
            _holdScreenTop = _lastPresentedTop;
        }

        IncrementStructureGen();
        _contentChanged = true; // invalidate the static blit so collection changes (insert/add/remove) apply

        base.OnItemsSourceCollectionChanged(sender, args);
    }

    public override void OnStructureChanged()
    {
        IncrementStructureGen();
        _contentChanged = true; // head-insert/commit & other structure rebuilds invalidate the cache

        base.OnStructureChanged();
    }

    protected override void ApplyBackgroundMeasurementChange(StructureChange change)
    {
        base.ApplyBackgroundMeasurementChange(change); // base fires OnAdded

        IncrementStructureGen();
        _contentChanged = true;
    }

    protected override void OnHeadInsertCommitted()
    {
        base.OnHeadInsertCommitted(); // base fires OnAdded

        IncrementStructureGen();
        _contentChanged = true;
    }

    public override void OnTemplatesAvailable()
    {
        base.OnTemplatesAvailable();

        _contentChanged = true;
    }

    public override void UpdateByChild(SkiaControl child)
    {
        base.UpdateByChild(child);

        //image loaded or something else.. existing cell wanted an update..
        TrackChildAsDirty(child);

        Repaint();
    }

    // ---- JUMP TRANSITION HOLD ("previous + spinner") -------------------------------------------------
    // A windowed JUMP (rebase = full-collection Replace + ordered scroll in flight) rebuilds the whole
    // slice: live frames during the re-measure paint MIXED GENERATIONS (stale pooled cells over freshly
    // estimated ones) — "cells over cells" corruption. The jump is EXPLICIT (no heuristics): on the
    // full-replace we freeze the LAST PRESENTED plane and overdraw every frame with it, SCREEN-PINNED,
    // while the live pipeline keeps draining/measuring underneath. Released when the ordered scroll has
    // landed AND the viewport band passes the coherence gates (tiled, realized) — one-frame swap.
    private bool _transitionHold;
    private float _holdScreenTop;   // context.Destination.Top of the last presented frame (screen-pin)
    private int _holdFrames;        // safety cap
    private const int MaxHoldFrames = 240;
    private float _lastPresentedTop;

    public override void DrawDirectInternal(DrawingContext context, SKRect drawingRect)
    {
        if (drawingRect.Height == 0 || drawingRect.Width == 0 || IsDisposed || IsDisposing)
            return;

        var destination = context.Destination;
        float vpH = (float)ParentViewport.Pixels.Height;

        _lastPresentedTop = destination.Top;

        if (_transitionHold)
        {
            // run the normal pipeline UNDERNEATH with an EMPTY CLIP: all logic executes (drains
            // structure changes, advances measurement — the exit condition depends on that progress)
            // but paints ZERO pixels (the frozen plane has transparent gaps between cells, a plain
            // overdraw would leak the transient through them).
            var canvas = context.Context.Canvas;
            int save = canvas.Save();
            canvas.ClipRect(SKRect.Empty);
            try
            {
                DrawDirectCore(context, drawingRect, destination, vpH);
            }
            finally
            {
                canvas.RestoreToCount(save);
            }

            bool orderedDone = !(Parent is SkiaScroll os && (os.OrderedScrollToIndexIsSet || os.OrderedScrollTo.IsValid));
            bool coherent = false;
            if (orderedDone)
            {
                var live = base.GetStackStructure();
                float gTol = Math.Max(8f, (float)(Spacing * RenderingScale) + 2f);
                float bareTop = -destination.Top;
                coherent = live != null
                           && SnapshotFillsViewport(live, bareTop, bareTop + vpH, gTol)
                           && ViewportViewsRealized(live, bareTop, bareTop + vpH);
            }

            if (coherent || ++_holdFrames > MaxHoldFrames)
            {
                _transitionHold = false; // swap to the (now coherent) live content next frame
                _contentChanged = true;  // and force a fresh plane record off it
                Repaint();
            }
            else if (ForegroundPlane != null)
            {
                // screen-pinned blit of the frozen plane: identical pixels to the last presented
                // frame regardless of how the offset moves during the rebase/settle
                var frozen = context.WithDestination(new SKRect(destination.Left, _holdScreenTop,
                    destination.Right, _holdScreenTop + drawingRect.Height));
                IsCaching = true;
                DrawCache(frozen, frozen.Destination);
                Repaint(); // keep frames coming: exit conditions are re-checked per frame
                return;
            }

            return;
        }

        DrawDirectCore(context, drawingRect, destination, vpH);
    }

    private void DrawDirectCore(DrawingContext context, SKRect drawingRect, SKRect destination, float vpH)
    {
        // Consume a plane the off-thread bake just finished (no-op in single-plane mode).
        UpdatePlanes();

        // Blit frames never reach the live DrawStack, whose "start background measurement if needed" check
        // is the ONLY restart site — once frames go fully-blit the measurement frontier stalls and the
        // content extent stops growing: the scroll walls at the frontier and LoadMore never re-arms
        // (device: stuck at "measured 150, Data: 200"). Kick it here every frame — a cheap no-op while
        // measurement runs or everything is measured.
        if (IsTemplated && MeasureItemsStrategy == MeasuringStrategy.MeasureVisible)
        {
            KickBackgroundMeasurement();
        }

        var hadPending = HasPendingStructureChanges;
        if (hadPending)
            _contentChanged = true;
        bool dirty = !DirtyChildrenTracker.IsEmpty;

        // ordered ScrollToIndex (jump-to-message) must live-paint each frame to track cells toward the target.
        bool orderedScroll = Parent is SkiaScroll os &&
                             (os.OrderedScrollToIndexIsSet || os.OrderedScrollTo.IsValid);

        // COVERAGE IS THE SIGNAL. Does the current plane actually cover the visible viewport? Not the scroll
        // velocity — coverage. Covered -> blit (smooth). Not covered = a gap -> re-record / draw live below.
        // No global "everything measured" gate here: the completeness gates + coverage clamp in CreateCache
        // verify the ACTUAL viewport band at record time, and a coverage miss falls back safely — a global
        // flag would block blitting for the whole tail-measure of every LoadMore batch (pacing jerk).
        bool covered = PlaneCoversViewport(destination, vpH);

        // SETTLED-SKELETON HEAL (blit-starves-live #4): cells that enter the viewport during the final
        // deceleration BLIT frames never meet a live DrawStack — no gap-rescue, no prep want-list post —
        // and the plane then blits their skeletons/foreign caches FOREVER at rest (device: empty bubbles
        // at 70fps). Gated to SETTLED frames only: during motion transient skeletons are legal and heal
        // via drift re-records; checking mid-fling would force live frames and cost the blit pacing.
        if (destination.Top == _settleProbeOffsetY)
        {
            if (_settledFrames < 3)
                _settledFrames++;
        }
        else
        {
            _settledFrames = 0;
            _settleProbeOffsetY = destination.Top;
        }

        if (_settledFrames >= 2 && !_contentChanged && AnyVisibleCellUnprepared())
        {
            _contentChanged = true; // live frame below: gap-rescue heals inline, next record re-bakes the band
        }

        if (covered && !orderedScroll && _cacheValid && !dirty && !_contentChanged)
        {
            // Refresh the plane once the viewport drifts half a viewport, so coverage never runs out
            // mid-scroll (async: kicks a background bake and keeps blitting; sync: one record here).
            // CreateCache commits _recordOffsetY/_contentChanged itself when the gates pass; a gate-reject
            // leaves them untouched so we retry next frame instead of waiting another 0.5vpH.
            // _planeStale: the prep worker readied cells this plane holds as baked SKELETONS — re-record
            // now (async keeps blitting the current plane meanwhile) instead of serving skeletons until
            // the drift threshold (device: "placeholders while scrolling not so fast"). Honored ONLY when
            // this plane actually painted skeletons — skeletonless planes ignore prep chatter (Disabled
            // mode preps at the frontier constantly; re-baking clean planes is pure CPU waste).
            if (_planeStale && !_planeHasSkeletons)
            {
                _planeStale = false;
            }

            if (!_bakeInFlight &&
                (_planeStale || Math.Abs(destination.Top - _recordOffsetY) >= vpH * 0.5f))
            {
                _planeStale = false;
                CreateCache(context, drawingRect);
            }

            IsCaching = true;
            DrawCache(context, destination);
            return;
        }

        IsCaching = false;
        var hadValidPlane = _cacheValid; // before clearing: was the previous plane still trustworthy?
        _cacheValid = false;
        _contentChanged = true;

        // Re-establish the plane NOW, moving or settled, and blit it in the same frame — one paint, the same
        // cost as the direct draw it replaces. Waiting for a settle here left every post-invalidation scroll
        // stretch (each LoadMore commit sets _contentChanged) on per-frame direct draw until the next drag
        // re-grab: hundreds-of-frames direct/blit blocks alternating = pacing jerk in both directions.
        // The completeness gates inside CreateCache reject transient structures (hole/gap/overlap) leaving
        // the flags set, so we fall through and retry next frame.
        if (!orderedScroll)
        {
            // Never record over an UNDRAINED structure: the gates would validate (and the kick would clear
            // _contentChanged for) a structure that still mutates when the pending batches integrate — and
            // since blit frames don't drain, that loops record-per-frame forever. Let the LIVE frame below
            // drain first; the next frame records clean.
            if (!_bakeInFlight && !hadPending)
            {
                CreateCache(context, drawingRect); // sync-records, or hands the record to the async worker
            }

            if (_cacheValid && !_contentChanged && PlaneCoversViewport(destination, vpH))
            {
                IsCaching = true;
                DrawCache(context, destination);
                return;
            }

            // TRIM-COMMIT DESYNC FRAME ONLY: cells were just translated and the scroll compensation lands
            // next frame — a live draw NOW paints desynced content (up to a fully EMPTY frame). The previous
            // plane was RE-ANCHORED for exactly this commit, so it is still pixel-stable: serve it for this
            // one frame. Any OTHER invalidated frame must fall through to a LIVE draw — the live pipeline is
            // what integrates staged batches, restacks, and grows the content extent (serving a stale plane
            // there starved LoadMore re-arming: the "wall at message 176" bug). In prepared mode that live
            // frame is pure cache blits — one record-priced frame per content change, not a scroll spike.
            if (_translatedThisFrame && hadValidPlane && PlaneCoversViewport(destination, vpH))
            {
                _translatedThisFrame = false;
                IsCaching = true;
                DrawCache(context, destination);
                return;
            }

            _translatedThisFrame = false;
        }

        // Need LIVE cells this frame. An async bake may still be running — give it a moment to hand us
        // a covering plane (one frame budget, not more: waiting longer IS the jank).
        if (_bakeInFlight)
        {
            _bakeDone.Wait(16);
            UpdatePlanes();

            if (_cacheValid && PlaneCoversViewport(destination, vpH))
            {
                IsCaching = true;
                DrawCache(context, destination); // the bake landed a covering plane while we waited
                return;
            }

            if (_bakeInFlight && ForegroundPlane != null && PlaneCoversViewport(destination, vpH))
            {
                // bake still running after the wait (rare) — the current plane still covers: blit it.
                IsCaching = true;
                DrawCache(context, destination);
                return;
            }

            // Bake still running AND the plane does NOT cover the viewport: blitting it paints an EMPTY
            // BAND (device: visible gap on fast backward flings that outrun the bake). Fall through to
            // DIRECT DRAW — always current, no hole. Safe alongside the bake: the bake is a pure-read
            // pass over a deep-frozen geometry snapshot, it does not touch the live draw state.
        }

        // DIRECT DRAW live cells — fills the gap, always current (no blank).
        base.DrawDirectInternal(context, drawingRect);
    }

    /// <summary>Does the current ForegroundPlane's recorded coverage span the whole visible viewport?</summary>
    private bool PlaneCoversViewport(SKRect destination, float vpH)
    {
        if (ForegroundPlane == null)
            return false;
        float vpTop = -destination.Top;
        float vpBot = vpTop + vpH;
        return vpTop >= _foregroundCoveredTop - 1f && vpBot <= _foregroundCoveredBot + 1f;
    }

    /// <summary>
    /// Does the structure's measured cells TILE the content band [top..bot] with no gap and no overlap,
    /// reaching its bottom? A transient structure (mid grow/trim/remeasure reposition) leaves a hole or an
    /// overlap the record would freeze into the plane for its whole service life; rejecting here keeps the
    /// caller on live draw (which heals next frame). Cells are in index order.
    /// <para><paramref name="tol"/> is the gap tolerance in PIXELS — must be >= the layout inter-cell spacing
    /// (Spacing * RenderingScale), else the legitimate spacing between every cell reads as a hole and the gate
    /// rejects every valid record (the Android blank-plane bug: 12px spacing vs a hardcoded 8px tol).</para>
    /// </summary>
    static bool SnapshotFillsViewport(LayoutStructure s, float top, float bot, float tol)
    {
        if (s == null) return false;
        float cursor = top;
        bool started = false;
        foreach (var c in s.GetChildren())
        {
            if (c == null || !c.WasMeasured) continue;
            float ct = c.Destination.Top, cb = c.Destination.Bottom;
            if (cb <= top) continue;            // entirely above the band
            if (ct >= bot) break;               // entirely below (index-ordered) -> done scanning
            if (!started)
            {
                // Empty space ABOVE the first cell in the band (viewport overscrolled past content top, e.g.
                // the inverted start-anchor putting bareTop negative) is NOT a hole — start covering at the
                // first cell instead of demanding tiling from the (out-of-content) band top.
                cursor = ct;
                started = true;
            }
            else if (ct > cursor + tol)
            {
                return false;                   // real gap between cells (> spacing)
            }
            else if (ct < cursor - tol)
            {
                // OVERLAP: a transient mid-mutation state (e.g. a single-item height delta landed while a
                // background batch was positioned against the old bottoms). Never record it — a plane would
                // serve the overlap for its whole life; a live draw heals next frame. (Assumes Split==1,
                // like the cursor tiling above.)
                return false;
            }

            if (cb > cursor) cursor = cb;
        }

        return cursor >= bot - tol;             // covered all the way to the band bottom
    }

    /// <summary>
    /// Is this cell's view present AND are its PIXELS ready to record? Realized alone is not enough:
    /// an ImageDoubleBuffered cell that hasn't finished its offscreen bake paints a PLACEHOLDER/blank
    /// during the record — the plane installs with a hole and the blit serves that hole while the user
    /// scrolls (the observed "gaps"). Cold double-buffered cell => not recordable => caller stays on
    /// DIRECT draw until the bake lands (which invalidates the plane via UpdateByChild).
    /// </summary>
    bool CellRecordReady(int index)
    {
        var view = ChildrenFactory.PeekRealizedViewForIndex(index);
        if (view == null)
            return false;
        // Prepared-views pipeline: an unmeasured (or mid-preparation) cell paints its placeholder SKELETON —
        // and after a pool eviction rebind it still carries the PREVIOUS context's RenderObject, so the
        // bake-check below can't see it. Recording would freeze the skeleton into the plane for its whole
        // service life (the constant ~3-cell "empty band" the trim repro caught).
        if (view.NeedMeasure || view.IsPreparingOffthread)
            return false;
        if (view.UsesCacheDoubleBuffering && view.RenderObject == null)
            return false; // never baked yet -> would record a blank
        return true;
    }

    /// <summary>
    /// Are realized views with READY pixels in place for every cell overlapping the content band
    /// [top..bot]? The record's DrawStack bails on a null view, and a cold double-buffered view records
    /// a blank (see <see cref="CellRecordReady"/>); either way draw live this frame instead so a partial
    /// plane is never installed.
    /// </summary>
    bool ViewportViewsRealized(LayoutStructure s, float top, float bot)
    {
        if (s == null) return false;
        foreach (var c in s.GetChildren())
        {
            if (c == null || !c.WasMeasured) continue;
            if (c.Destination.Bottom <= top) continue; // above band
            if (c.Destination.Top >= bot) break;        // below band (index-ordered) -> done
            if (!CellRecordReady(c.ControlIndex))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Shrink claimed coverage to the contiguous measured+realized cell span containing the bare viewport.
    /// Walks cells in index order (ascending top) within the claim band, merging contiguous recordable cells
    /// into segments; a hole (unmeasured/unrealized cell) or a gap (> tol with more cells beyond) closes a
    /// segment. Clamp rules: a blocked BELOW edge always clamps (the closer is a real cell the record misses);
    /// the ABOVE edge clamps only when a block preceded the segment inside the band. Content ends (no cells
    /// beyond) keep the original claim — empty space blits identically to a live draw, and clamping at an end
    /// would force a re-record on every bounce frame.
    /// </summary>
    void ClampCoverageToRecordable(LayoutStructure s, float tol, float bareTop, float bareBot,
        ref float coveredTop, ref float coveredBot)
    {
        bool blockedAbove = false;
        float segTop = float.NaN, segBot = float.NaN;

        foreach (var c in s.GetChildren()) // index order = ascending Destination.Top
        {
            if (c == null || c.IsCollapsed) continue;
            float t = c.Destination.Top, b = c.Destination.Bottom;
            if (b <= coveredTop) continue;
            if (t >= coveredBot) break;

            bool ok = c.WasMeasured && CellRecordReady(c.ControlIndex);
            bool hasSeg = !float.IsNaN(segTop);
            bool segDone = hasSeg && segTop <= bareTop && segBot >= bareBot; // segment spans the bare band

            if (!ok)
            {
                // hole: a real cell the record misses closes the segment from below
                if (segDone)
                {
                    if (blockedAbove) coveredTop = Math.Max(coveredTop, segTop);
                    coveredBot = Math.Min(coveredBot, segBot);
                    return;
                }

                blockedAbove = true; // any later segment has this hole above it
                segTop = segBot = float.NaN;
            }
            else if (!hasSeg)
            {
                segTop = t;
                segBot = b; // first segment start — nothing above it inside the band
            }
            else if (t > segBot + tol)
            {
                // gap with a real cell on its far side
                if (segDone)
                {
                    if (blockedAbove) coveredTop = Math.Max(coveredTop, segTop);
                    coveredBot = Math.Min(coveredBot, segBot);
                    return;
                }

                blockedAbove = true;
                segTop = t;
                segBot = b;
            }
            else
            {
                if (b > segBot) segBot = b;
            }
        }

        // loop ended at content end / claim edge: below is unblocked, keep coveredBot
        if (blockedAbove && !float.IsNaN(segTop) && segTop <= bareTop && segBot >= bareBot)
            coveredTop = Math.Max(coveredTop, segTop);
    }

    /// <summary>
    /// SYNC record of the band plane on the render thread. Gate-checked: records only when the viewport band
    /// is fully measured + realized + tiled (no hole/gap/overlap); a reject returns without touching state so
    /// the caller retries next frame. On success commits _recordOffsetY/_contentChanged/_cacheValid and the
    /// verified coverage, so the very next DrawCache blit serves the fresh plane. A successful record also
    /// kicks the off-thread compositor (see class summary).
    /// </summary>
    void CreateCache(DrawingContext context, SKRect recordArea)
    {
        // OVERSCAN: a record covers the INFLATED band [coveredTop..coveredBot] (viewport ± VirtualisationInflated
        // [Ratio]) so the plane can be REUSED (blitted, no record) while the bare viewport stays inside it.
        float vpH = (float)ParentViewport.Pixels.Height;
        float inflateY = (float)(VirtualisationInflated * RenderingScale);
        if (VirtualisationInflatedRatio >= 0)
            inflateY += (float)(VirtualisationInflatedRatio * vpH);
        float bareTop = -context.Destination.Top;
        float bareBot = bareTop + vpH;
        float coveredTop = bareTop - inflateY;
        float coveredBot = bareBot + inflateY;

        // The claim must never exceed the RECORD AREA (in content space): an SKPicture replays content
        // beyond its cull rect (cull is a hint), which silently served over-claims — but the compositor's
        // raster surface hard-clips at recordArea, turning the same over-claim into a blank band at the
        // plane edge (the 316px trim-repro band). Honest coverage for both blit paths.
        float raTopContent = recordArea.Top - context.Destination.Top;
        float raBotContent = recordArea.Bottom - context.Destination.Top;
        coveredTop = Math.Max(coveredTop, raTopContent);
        coveredBot = Math.Min(coveredBot, raBotContent);

        // COMPLETENESS GATES: never install a plane whose band has holes (unmeasured cells, unrealized views,
        // measure still catching up). A partial record installs a plane with a HOLE — the "cells 0-1-2-3 then
        // blank half screen" bug — and blitting it later shows the gap. Skip recording; the caller keeps
        // drawing live (which realizes+draws every visible cell) until the viewport is whole.
        {
            var live = base.GetStackStructure();
            float gTol = Math.Max(8f, (float)(Spacing * RenderingScale) + 2f);
            if (live == null
                || !SnapshotFillsViewport(live, bareTop, bareBot, gTol)
                || !ViewportViewsRealized(live, bareTop, bareBot))
                return; // reject: flags untouched, caller retries next frame

            // COVERAGE CLAMP: the gates prove the BARE viewport, but the claim is the INFLATED band — cells
            // in the margin can be unmeasured/unrealized, the record skips them, and blitting later shows
            // blank where live cells exist (persistent RenderTree IDXGAP while that plane serves). Shrink
            // the claim to the contiguous recordable span around the viewport. Content ENDS stay claimed
            // (nothing recordable beyond them; clamping there would force a re-record every bounce frame).
            ClampCoverageToRecordable(live, gTol, bareTop, bareBot, ref coveredTop, ref coveredBot);
        }

        // Gates passed -> this record WILL serve the current band: commit the drift origin and clear the
        // invalidation HERE (not at the call sites). Committing before the gates ran used to eat the flags on
        // a reject — postponing the next refresh half a viewport and masking a content change (the async
        // discard paths re-set _contentChanged, so a failed bake still forces a retry).
        _recordOffsetY = context.Destination.Top;
        _contentChanged = false;

        // This record consumes the dirty state (it paints the cells' CURRENT pixels). Blit frames never
        // reach the live DrawStack's ClearDirtyChildren — without clearing here one dirty cell keeps the
        // tracker non-empty forever and every frame re-records: an endless record/bake loop at rest
        // (device livelock). A cell dirtied during an async bake re-dirties the tracker afterwards and
        // simply triggers one more record.
        ClearDirtyChildren();

        // ASYNC SECOND PLANE: hand the record to the worker and keep blitting the current plane — no
        // render-thread paint on this frame at all. Sync only when async genuinely can't serve: mode off,
        // unsafe head without prepared views, or the FIRST plane (nothing to blit yet).
        if (AsyncPlaneAllowed && ForegroundPlane != null)
        {
            KickAsyncBake(context, recordArea, bareTop, bareBot, coveredTop, coveredBot);
            return;
        }

        // SYNC record on the render thread (single-plane mode / first plane).
        // If a mutation lands during Paint the gates validated a structure the paint no longer used —
        // discard such records (the picture would mix pre/post states).
        var mutationStamp = Volatile.Read(ref _structureGen);

        _recorder ??= new SKPictureRecorder();
        var rc = context.CreateForRecordingOperations(_recorder, recordArea);

        SkiaLayout.ResetPaintedBounds();
        SkiaLayout.CollectPaintedBounds = true;

        SKPicture picture;
        try
        {
            Paint(rc); // => DrawStack, DrawStackVisibleChildren..
            picture = _recorder.EndRecording();
        }
        finally
        {
            SkiaLayout.CollectPaintedBounds = false;
        }

        if (Volatile.Read(ref _structureGen) != mutationStamp || _contentChanged)
        {
            picture.Dispose();
            _contentChanged = true;
            return;
        }

        // PAINTED-BAND CLAMP: the gates verify cells are recordABLE, but the record only PAINTS its own
        // visibility window — with prepared views realizing/baking cells far beyond it, the recordable claim
        // over-reaches. Never claim what this record didn't paint.
        if (SkiaLayout.PaintedBoundsTop < SkiaLayout.PaintedBoundsBottom)
        {
            float paintedTopContent = SkiaLayout.PaintedBoundsTop - context.Destination.Top;
            float paintedBotContent = SkiaLayout.PaintedBoundsBottom - context.Destination.Top;
            if (paintedTopContent > coveredTop) coveredTop = Math.Min(paintedTopContent, bareTop);
            if (paintedBotContent < coveredBot) coveredBot = Math.Max(paintedBotContent, bareBot);
        }

        if (ForegroundPlane == null)
        {
            ForegroundPlane = new CachedObject(SkiaCacheType.Operations, picture, context.Destination, recordArea);
        }
        else
        {
            ForegroundPlane.Picture?.Dispose();
            ForegroundPlane.Picture = picture;
            ForegroundPlane.Bounds = context.Destination;
            ForegroundPlane.RecordingArea = recordArea;
        }

        _foregroundCoveredTop = coveredTop; // verified (clamped) coverage of this record
        _foregroundCoveredBot = coveredBot;
        _planeHasSkeletons = SkiaLayout.PaintedSkeleton; // prep-completion re-records only matter if true
        _cacheValid = true; // the blit branch accepts the plane (only ever set after a successful record)

    }

    #region ASYNC SECOND PLANE (UseDoubleBuffering = true)

    // Resurrection of the proven two-planes pipeline (DrawnChatList 76e06d9 "use cached") with the safety
    // work that was missing then:
    //  - the bake's paint pass is a PURE READ (SkiaLayout.IsPlaneBakePass): no draining of pending structure
    //    changes into the frozen snapshot (lost updates), no render-tree swap, no dirty-tracker clears — the
    //    historical structure-corruption vectors are structurally closed;
    //  - on MAUI heads (thread-unsafe BindableObject) the async bake is allowed ONLY under UsePreparedViews:
    //    every band cell is already bound+measured+cache-baked (gates verify), so the bake never binds or
    //    measures — zero writes to bindable storage (the crash class that forced the old AsyncBakeSafe
    //    kill-switch, commit a6dac86);
    //  - generation guard at publish AND at consume, planes (including a mid-flight bake's coverage)
    //    re-anchored across windowed trims.

    // Heads with a lock-guarded BindableObject implementation (SharedNet BindableObject.Plain) can bake
    // live views safely even without prepared views. MAUI heads cannot: MAUI's BindableObject is a plain
    // dictionary the UI thread mutates while a bake would read it (GetValue even WRITES on a read-miss).
#if ANDROID || IOS || MACCATALYST || WINDOWS
    private const bool AsyncBakeSafe = false;
#else
    private const bool AsyncBakeSafe = true;
#endif

    /// <summary>Second background-prepared plane is allowed: flag on, and either the head is
    /// thread-safe for live-view bakes or the prepared-views pipeline guarantees the bake is a pure
    /// cache-blit pass (no binds, no measures).</summary>
#if BROWSER
    // Single-threaded WASM: the offscreen pump is Task.Run on the ONE thread, so a kicked bake can only
    // execute when the render frame yields — while the render thread's _bakeDone.Wait(16) blocks that very
    // thread. Result: 16ms burned EVERY frame with the bake never progressing (scroll hard-stop whenever
    // content is being measured/invalidated). Sync record is the correct mode here: an Operations plane is
    // an SKPicture record (no raster) — one predictable record-priced frame instead of a stall.
    private bool AsyncPlaneAllowed => false;
#else
    private bool AsyncPlaneAllowed => UseDoubleBuffering && (AsyncBakeSafe || UsePreparedViewsActive);
#endif

    // Bumped by the render thread on every structural rebase the bake's coordinates depend on: structure
    // rebuilds, batch integrations, collection changes and content translates. A bake frozen under an older
    // generation publishes PRE-shift coordinates + coverage — it is discarded at publish and re-checked at
    // consume (the reanchor sweep only fixes planes already in the slots).
    private int _structureGen;

    void IncrementStructureGen() => Interlocked.Increment(ref _structureGen);

    private volatile bool _bakeInFlight; // one off-thread bake at a time; keep blitting current plane meanwhile
    private readonly ManualResetEventSlim _bakeDone = new(true); // signalled when no bake is running

    // A freshly baked plane published by the worker, not yet installed. The render thread is the SOLE owner
    // of swapping: it consumes this in UpdatePlanes and promotes it to ForegroundPlane. The worker only ever
    // publishes here (via Interlocked), never touches ForegroundPlane.
    private CachedObject _preparedPlane;
    private volatile float _preparedCoveredTop, _preparedCoveredBot;
    private volatile int _preparedGen;

    /// <summary>
    /// Gesture render tree built by the bake over the frozen snapshot, paired with <see cref="_preparedPlane"/>.
    /// Written by the worker BEFORE the plane publish (the Interlocked exchange fences it), installed via
    /// SetRenderingTree on the RENDER thread when the plane is consumed. Keeps hit rects in the SAME
    /// coordinate frame as the served plane — the invariant DrawCache's RenderTree.Offset patching relies
    /// on; a tree from an older live frame maps taps to the wrong cells (harness: TapStaleTreeRepro).
    /// </summary>
    private List<SkiaControlWithRect> _preparedTree;

    // ---- frozen-structure snapshot for the bake -------------------------------------------------------
    // The bake runs Paint -> DrawStack on a worker while the render thread mutates the live structure
    // (head-remove translate, batch integration, cell offsets). Hand the bake a DEEP-frozen geometry
    // snapshot via a thread-local override of GetStackStructure(): only the baking thread sees it. POOLED
    // (one reusable structure + ControlInStack pool): zero per-bake allocation once warmed — a fresh deep
    // copy per bake was the historical GC-spike source. Reuse of a single buffer is safe because
    // _bakeInFlight serializes bakes.
    [ThreadStatic] private static LayoutStructure _bakeStructure;
    private LayoutStructure _frozenReusable;
    private readonly List<ControlInStack> _bakePool = new();

    /// <summary>The bake thread reads its frozen snapshot; every other thread reads the live structure.</summary>
    public override LayoutStructure GetStackStructure()
        => _bakeStructure ?? base.GetStackStructure();

    /// <summary>Deep copy into the pooled buffer: reused ControlInStack instances (frozen geometry/flags)
    /// over the SAME live Views. Zero allocation once the pool has grown to the structure size.</summary>
    private LayoutStructure FreezeStructure(LayoutStructure src)
    {
        var clone = _frozenReusable ??= new LayoutStructure();
        clone.Clear();
        if (src == null) return clone;
        int i = 0;
        foreach (var c in src.GetChildren())
        {
            if (c == null) continue;
            ControlInStack dst;
            if (i < _bakePool.Count)
                dst = _bakePool[i];
            else
                _bakePool.Add(dst = new ControlInStack());
            i++;

            dst.ControlIndex = c.ControlIndex;
            dst.Measured = c.Measured;
            dst.Layout = c.Layout;
            dst.Area = c.Area;
            dst.Destination = c.Destination;
            dst.OffsetOthers = c.OffsetOthers;
            dst.View = c.View;
            dst.Offset = c.Offset;
            dst.WasMeasured = c.WasMeasured;
            dst.IsVisible = c.IsVisible;
            dst.ZIndex = c.ZIndex;
            dst.Column = c.Column;
            dst.Row = c.Row;
            dst.IsCollapsed = c.IsCollapsed;
            dst.DebugMeasureBatch = c.DebugMeasureBatch;

            clone.Add(dst, c.Column, c.Row);
        }

        // Drop stale View refs on the unused pool tail: after a trim shrinks the structure, entries beyond
        // the current count would otherwise pin dead cells (and their caches) from GC indefinitely.
        for (int j = i; j < _bakePool.Count; j++)
            _bakePool[j].View = null;
        return clone;
    }

    /// <summary>
    /// Render thread: consume a freshly baked plane. Swaps ONLY when a new bake actually arrived, so the
    /// foreground never oscillates; a bake frozen before a structural rebase is dropped here (consume-time
    /// generation check runs on the same thread as the rebases = race-free).
    /// </summary>
    void UpdatePlanes()
    {
        var prepared = Interlocked.Exchange(ref _preparedPlane, null);
        if (prepared == null)
            return;

        var preparedTree = _preparedTree;
        _preparedTree = null;

        if (_preparedGen != Volatile.Read(ref _structureGen))
        {
            DisposeObject(prepared);
            _contentChanged = true; // force a fresh record next frame
            return;
        }

        var old = ForegroundPlane;
        ForegroundPlane = prepared;
        _foregroundCoveredTop = _preparedCoveredTop; // carry the bake's coverage with the promoted plane
        _foregroundCoveredBot = _preparedCoveredBot;
        _cacheValid = true;
        DisposeObject(old);

        // Install the bake's gesture tree WITH its plane (render thread = safe to swap): hit rects now
        // share the served plane's coordinate frame, so DrawCache's Offset patching maps taps correctly.
        if (preparedTree != null)
        {
            SetRenderingTree(preparedTree);
        }
    }

    /// <summary>
    /// Kick an off-thread ops record of the band (worker thread): freeze geometry, record the picture over
    /// the frozen snapshot as a pure-read pass, gate the result, publish for the render thread to consume.
    /// The render-thread caller has already run the completeness gates and committed the drift origin.
    /// </summary>
    void KickAsyncBake(DrawingContext context, SKRect recordArea, float bareTop, float bareBot,
        float coveredTop, float coveredBot)
    {
        if (_bakeInFlight)
            return; // one at a time; the current plane keeps blitting, drift re-kicks next frame

        LayoutStructure bakeSnapshot;
        lock (LockMeasure)
        {
            bakeSnapshot = FreezeStructure(base.GetStackStructure());
        }

        var gen = Volatile.Read(ref _structureGen);
        _bakeInFlight = true;
        _bakeDone.Reset();


        PushToOffscreenRendering(() =>
        {
            try
            {
                if (IsDisposed || IsDisposing)
                    return;

                SKPicture picture;
                List<SkiaControlWithRect> bakeTree;
                SkiaLayout.ResetPaintedBounds();
                SkiaLayout.CollectPaintedBounds = true;
                SkiaLayout.IsPlaneBakePass = true;
                SkiaLayout.CollectedBakeTree = null; // pooled worker thread: drop a previous bake's deposit
                _bakeStructure = bakeSnapshot;
                try
                {
                    // DEDICATED recorder per bake: the shared _recorder belongs to the render-thread sync
                    // path, which can run while this bake is in flight — sharing one SKPictureRecorder
                    // across threads is a native crash (EndRecording AV).
                    using var recorder = new SKPictureRecorder();
                    var rc = context.CreateForRecordingOperations(recorder, recordArea);

                    Paint(rc); // pure-read pass: no drain, no tree swap, no dirty clears (IsPlaneBakePass)

                    picture = recorder.EndRecording();
                }
                finally
                {
                    bakeTree = SkiaLayout.CollectedBakeTree; // gesture tree in the plane's coordinate frame
                    SkiaLayout.CollectedBakeTree = null;     // never pin cells via the pooled thread's static
                    _bakeStructure = null;
                    SkiaLayout.IsPlaneBakePass = false;
                    SkiaLayout.CollectPaintedBounds = false;
                }

                // POST-BAKE GATES on the frozen snapshot: the render thread may have mutated the LIVE
                // structure since the kick — the snapshot itself is what got painted, so validate it.
                float fillTol = Math.Max(8f, (float)(Spacing * RenderingScale) + 2f);
                bool complete = SnapshotFillsViewport(bakeSnapshot, bareTop, bareBot, fillTol)
                                && ViewportViewsRealized(bakeSnapshot, bareTop, bareBot);
                bool genValid = gen == Volatile.Read(ref _structureGen);
                if (!complete || !genValid)
                {
                    picture.Dispose();
                    _contentChanged = true; // force a retry next frame
                    return;
                }

                // PAINTED-BAND CLAMP: never claim coverage the record didn't actually paint (prepared views
                // realize cells far beyond the painted window, so "recordable" alone over-claims).
                if (SkiaLayout.PaintedBoundsTop < SkiaLayout.PaintedBoundsBottom)
                {
                    float paintedTopContent = SkiaLayout.PaintedBoundsTop - context.Destination.Top;
                    float paintedBotContent = SkiaLayout.PaintedBoundsBottom - context.Destination.Top;
                    if (paintedTopContent > coveredTop) coveredTop = Math.Min(paintedTopContent, bareTop);
                    if (paintedBotContent < coveredBot) coveredBot = Math.Max(paintedBotContent, bareBot);
                }

                // Publish: coverage + gesture tree set BEFORE the plane so the render thread consumes a
                // consistent set (the Interlocked exchange below fences the writes).
                _preparedCoveredTop = coveredTop;
                _preparedCoveredBot = coveredBot;
                _planeHasSkeletons = SkiaLayout.PaintedSkeleton; // this bake's own thread-static
                _preparedGen = gen;
                _preparedTree = bakeTree;
                var rendered = new CachedObject(SkiaCacheType.Operations, picture, context.Destination, recordArea);
                var stale = Interlocked.Exchange(ref _preparedPlane, rendered);
                if (stale != null)
                {
                    DisposeObject(stale);
                }

            }
            catch (Exception e)
            {
                Super.Log(e);
                _contentChanged = true;
            }
            finally
            {
                _bakeInFlight = false;
                _bakeDone.Set();
                Repaint(); // render thread consumes on the next frame
            }
        });
    }

    #endregion


    /// <summary>
    /// Draw cached plane with translation: we draw the cache at the position "where it was captured" and
    /// apply translation to position it according to the current scroll offset; translation and
    /// RenderTree.Offset keep gestures correctly mapped.
    /// </summary>
    void DrawCache(DrawingContext context, SKRect dest)
    {
        var cache = ForegroundPlane;
        if (cache != null)
        {
            // RenderTree is null until the first LIVE frame builds one — a plane can be served before
            // that (startup blit) and gestures have nothing to hit yet anyway.
            var tree = this.RenderTree;
            if (tree != null)
            {
                tree.Offset = CalculateCacheOffset(cache, dest);
            }

            cache.Draw(context.Context.Canvas, dest, null, FilterQuality.None);
        }
    }

    /// <summary>Offset between where the cache was captured and where it should be drawn.</summary>
    SKPoint CalculateCacheOffset(CachedObject cache, SKRect destination)
    {
        var moveY = cache.Bounds.Top - cache.RecordingArea.Top;
        var moveX = cache.Bounds.Left - cache.RecordingArea.Left;
        var x = (float)(destination.Left - cache.Bounds.Left + moveX);
        var y = (float)(destination.Top - cache.Bounds.Top + moveY);
        return new SKPoint(x, y);
    }

    /// <summary>
    /// A head trim reclaimed dead space: the base translated every cell -deltaPixels and compensated the
    /// parent scroll +deltaPixels, so next frame destination.Top moves +deltaPixels. Our plane was recorded
    /// in the PRE-shift coordinate frame; the rigid blit (CalculateCacheOffset reduces to y = destination.Top
    /// - RecordingArea.Top) would then land it deltaPixels off — a one-frame hole until a fresh record.
    /// Re-anchor the plane's RecordingArea/Bounds by the SAME delta so the blit stays pixel-stable across the
    /// commit. Runs on the render thread before the parent scroll computes its frame offset.
    /// </summary>
    public override void OnContentTranslatedVertically(float deltaPixels)
    {
        base.OnContentTranslatedVertically(deltaPixels);

        if (deltaPixels == 0)
            return;

        ReanchorPlane(ForegroundPlane, deltaPixels);
        ReanchorPlane(_preparedPlane, deltaPixels); // a bake published in the pre-shift frame
        IncrementStructureGen(); // a bake NOT yet published can't be re-anchored — orphan it (discards at publish/consume)
        _translatedThisFrame = true; // next draw: serve the re-anchored plane, not a desynced live frame
        _recordOffsetY += deltaPixels; // keep band-drift origin in the post-shift frame

        // cells moved -deltaPixels in content space, so the planes' covered content ranges move with them
        _foregroundCoveredTop -= deltaPixels;
        _foregroundCoveredBot -= deltaPixels;
        _preparedCoveredTop -= deltaPixels;
        _preparedCoveredBot -= deltaPixels;
    }

    static void ReanchorPlane(CachedObject plane, float deltaPixels)
    {
        if (plane == null)
            return;

        plane.RecordingArea = new SKRect(plane.RecordingArea.Left, plane.RecordingArea.Top + deltaPixels,
            plane.RecordingArea.Right, plane.RecordingArea.Bottom + deltaPixels);
        plane.Bounds = new SKRect(plane.Bounds.Left, plane.Bounds.Top + deltaPixels,
            plane.Bounds.Right, plane.Bounds.Bottom + deltaPixels);
    }

    /// <summary>The plane currently being blitted. ONLY the render thread reads/writes it.</summary>
    public CachedObject ForegroundPlane { get; set; }

    /// <summary>True while frames are served by blitting the plane (false = live per-cell drawing).</summary>
    public bool IsCaching { get; protected set; }

    public override void OnWillDisposeWithChildren()
    {
        base.OnWillDisposeWithChildren();

        DisposeObject(Interlocked.Exchange(ref _preparedPlane, null));
        DisposeObject(ForegroundPlane);
    }

    public override void OnDisposing()
    {
        DisposeObject(Interlocked.Exchange(ref _preparedPlane, null));
        ForegroundPlane = null;
        _recorder?.Dispose();
        _recorder = null;

        base.OnDisposing();
    }
}
