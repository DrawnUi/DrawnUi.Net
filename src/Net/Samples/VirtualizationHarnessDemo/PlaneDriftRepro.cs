using System.Reflection;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Answers: HOW MANY pixels can a SkiaCachedStack scroll before its band plane is re-recorded, and WHY
/// each re-record happens. Steps ViewportOffsetY in small deterministic increments and watches the plane
/// through reflection: a record = the ForegroundPlane picture identity (or _recordOffsetY commit) changing.
/// Each record is classified against the gate inputs captured BEFORE the frame:
///   dirty cells / contentChanged / planeStale (prep heal) / coverage runout / half-viewport drift (by design).
/// Design cadence: one record per vpH*0.5 of travel (drift refresh), pure blits in between.
/// Two cell shapes: PLAIN (no per-cell cache) and the DrawnCells shop-grid shape (root ImageDoubleBuffered)
/// whose async cache bakes dirty the plane the way streaming-in images do.
/// </summary>
public static class PlaneDriftRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= PLANE DRIFT (how far can we scroll before a re-record, and why) =========");
        try
        {
            RunCore("plain cells", doubleBufferedCells: false);
            RunCore("ImageDoubleBuffered cells (DrawnCells-A shape)", doubleBufferedCells: true);
        }
        catch (Exception ex) { Console.WriteLine($"  CRASH: {ex}"); }
        Console.WriteLine("===================================================================================");
    }

    class Item
    {
        public int Id { get; init; }
    }

    static void RunCore(string label, bool doubleBufferedCells)
    {
        Console.WriteLine($"--- {label} ---");

        var bg = Colors.Black;
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: bg);

        var items = new List<Item>(Enumerable.Range(1, 300).Select(i => new Item { Id = i }));

        SkiaLayout grid = null;
        SkiaScroll scroll = null;
        host.Canvas.Content = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaScroll
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Content = new SkiaCachedStack
                    {
                        Tag = "DriftGrid",
                        Split = 2,
                        Spacing = 10,
                        Padding = new Thickness(10, 12),
                        HorizontalOptions = LayoutOptions.Fill,
                        RecyclingTemplate = RecyclingTemplate.Enabled,
                        MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
                        Virtualisation = VirtualisationType.Enabled,
                        ItemsSource = items,
                        ItemTemplate = new DataTemplate(() =>
                        {
                            var root = new SkiaShape
                            {
                                Type = ShapeType.Rectangle,
                                CornerRadius = 8,
                                BackgroundColor = Colors.White,
                                HeightRequest = 270,
                                HorizontalOptions = LayoutOptions.Fill,
                            };
                            if (doubleBufferedCells)
                                root.UseCache = SkiaCacheType.ImageDoubleBuffered;
                            return root;
                        }),
                    }.Assign(out grid)
                }.Assign(out scroll)
            }
        };

        var stack = (SkiaCachedStack)grid;
        var spy = new PlaneSpy(stack);

        // settle: realize + measure everything so measurement noise never pollutes the drift phase
        for (int i = 0; i < 300 && grid.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(3); }
        for (int i = 0; i < 400; i++)
        {
            host.RenderFrame(16);
            Thread.Sleep(2);
            if (grid.LastMeasuredIndex >= items.Count - 1) break;
        }

        // settle the plane itself: run frames at rest until a valid plane serves and nothing is dirty
        int settled = 0;
        for (int i = 0; i < 400 && settled < 5; i++)
        {
            host.RenderFrame(16);
            Thread.Sleep(2);
            var st = spy.Read(scroll);
            settled = (st.CacheValid && !st.ContentChanged && !st.Dirty && !st.BakeInFlight) ? settled + 1 : 0;
        }

        var s0 = spy.Read(scroll);
        Console.WriteLine($"  settled: measured={grid.LastMeasuredIndex + 1}/{items.Count} planeValid={s0.CacheValid} " +
                          $"covered=[{s0.CoveredTop:0}..{s0.CoveredBot:0}] vpH=920 driftThreshold={920 * 0.5f:0}px");

        // ---- DRIFT PHASE: step the offset 30px per frame for 3000px, log every record + its cause ----
        const float step = 30f;
        const float total = 3000f;

        float lastRecordAt = 0; // px scrolled at the previous record
        int records = 0, liveFrames = 0, blitFrames = 0, frames = 0, shown = 0;
        var reasons = new Dictionary<string, int>();

        var pre = spy.Read(scroll);
        long planeId = pre.PictureId;
        float recordOrigin = pre.RecordOffsetY;

        for (float px = step; px <= total; px += step)
        {
            scroll.ViewportOffsetY = -px;

            pre = spy.Read(scroll);
            host.RenderFrame(16);
            Thread.Sleep(2); // let async bakes land (real worker thread vs synthetic clock)
            frames++;

            var post = spy.Read(scroll);
            if (stack.IsCaching) blitFrames++; else liveFrames++;

            // COMMIT = CreateCache passed its gates and re-recorded (drift origin moved) — a real record.
            // SWAP-only = the async worker published the previously committed record's picture — NOT a new
            // record, just the one-frame-later landing of the same bake.
            bool committed = post.RecordOffsetY != recordOrigin;
            bool swapped = post.PictureId != planeId;
            planeId = post.PictureId;

            if (committed)
            {
                records++;
                float travelled = px - lastRecordAt;
                lastRecordAt = px;
                recordOrigin = post.RecordOffsetY;

                string reason = Classify(pre, px, 920f);
                reasons[reason] = reasons.GetValueOrDefault(reason) + 1;
                if (++shown <= 25)
                    Console.WriteLine($"   RECORD at {px,5:0}px  (+{travelled,4:0}px since last)  reason: {reason}");
            }
            else if (swapped && shown <= 25)
            {
                Console.WriteLine($"   (async publish at {px,5:0}px — previous record's plane landed, no new record)");
            }
        }

        float avg = records > 1 ? (lastRecordAt - 0) / records : 0;
        Console.WriteLine($"  scrolled {total:0}px in {frames} frames: records={records} " +
                          $"(avg {avg:0}px between), blit frames={blitFrames}, live frames={liveFrames}");
        foreach (var kv in reasons.OrderByDescending(k => k.Value))
            Console.WriteLine($"    reason `{kv.Key}`: {kv.Value}");
        Console.WriteLine($"  => design cadence would be ~{total / (920 * 0.5f):0} records " +
                          $"(one per {920 * 0.5f:0}px) with 100% blit frames in between");
    }

    static string Classify(PlaneState pre, float px, float vpH)
    {
        // order matters: these are the exact branch conditions of DrawDirectCore/CreateCache
        if (!pre.CacheValid) return "plane was invalid (prior invalidation)";
        if (pre.Dirty) return "dirty cells (UpdateByChild — cache bake/content change)";
        if (pre.ContentChanged) return "contentChanged (structure/collection invalidation)";
        float vpTop = px, vpBot = px + vpH;
        if (vpTop < pre.CoveredTop - 1f || vpBot > pre.CoveredBot + 1f) return "coverage runout (scrolled past band)";
        if (pre.PlaneStale) return "planeStale (prep worker healed a skeleton)";
        if (Math.Abs(px - pre.RecordOffsetY) >= vpH * 0.5f) return "half-viewport drift refresh (BY DESIGN)";
        return "unknown (none of the gate inputs were set pre-frame)";
    }

    struct PlaneState
    {
        public bool CacheValid, ContentChanged, PlaneStale, BakeInFlight, Dirty;
        public float RecordOffsetY, CoveredTop, CoveredBot;
        public long PictureId;
    }

    /// <summary>Reflection reader over SkiaCachedStack plane internals + the dirty tracker.</summary>
    class PlaneSpy
    {
        readonly SkiaCachedStack _stack;
        readonly FieldInfo _cacheValid, _contentChanged, _planeStale, _bakeInFlight,
            _recordOffsetY, _coveredTop, _coveredBot;

        public PlaneSpy(SkiaCachedStack stack)
        {
            _stack = stack;
            var t = typeof(SkiaCachedStack);
            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
            _cacheValid = t.GetField("_cacheValid", F);
            _contentChanged = t.GetField("_contentChanged", F);
            _planeStale = t.GetField("_planeStale", F);
            _bakeInFlight = t.GetField("_bakeInFlight", F);
            _recordOffsetY = t.GetField("_recordOffsetY", F);
            _coveredTop = t.GetField("_foregroundCoveredTop", F);
            _coveredBot = t.GetField("_foregroundCoveredBot", F);
        }

        public PlaneState Read(SkiaScroll scroll)
        {
            bool dirty = !_stack.DirtyChildrenTracker.IsEmpty;

            var plane = _stack.ForegroundPlane;
            return new PlaneState
            {
                CacheValid = (bool)(_cacheValid?.GetValue(_stack) ?? false),
                ContentChanged = (bool)(_contentChanged?.GetValue(_stack) ?? false),
                PlaneStale = (bool)(_planeStale?.GetValue(_stack) ?? false),
                BakeInFlight = (bool)(_bakeInFlight?.GetValue(_stack) ?? false),
                RecordOffsetY = -(float)(_recordOffsetY?.GetValue(_stack) ?? 0f),
                CoveredTop = (float)(_coveredTop?.GetValue(_stack) ?? 0f),
                CoveredBot = (float)(_coveredBot?.GetValue(_stack) ?? 0f),
                Dirty = dirty,
                PictureId = plane?.Picture != null
                    ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(plane.Picture)
                    : 0,
            };
        }
    }
}
