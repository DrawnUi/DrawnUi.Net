using System.Windows.Input;
using SkiaSharp;
using AppoMobi.Specials;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// ENGAGE-ON-GROW COLLIDES WITH THE LOADMORE THAT TRIGGERED IT (device: DrawnCells variant A, 2-column
/// SkiaCachedStack, "blink showing empty screen around id 200, then it repositions").
///
/// The LoadMore AddRange IS the engage trigger: OnItemsSourceCollectionChanged consumes the Add
/// (SkiaLayout.Shared.cs, "the caller's collection change is consumed by the engage reset") and engages
/// the window instead. LoadMore fires at the TAIL, so the window seeds centered on the freshest end and
/// its upper half is the items added milliseconds earlier — none of them measured yet.
/// TryEngageWindowInPlace keeps only MEASURED cells, so LastMeasuredIndexLocal lands mid-window,
/// UpdateProgressiveContentSize recomputes a much SHORTER content, the scroll clamps into it and the
/// viewport (which sat exactly at the frontier) has nothing left to draw.
///
/// Probe: content height and the visible band across the engage frames. A content SHRINK at engage, or
/// any frame with an empty visible band while items exist, is the bug.
/// </summary>
public static class EngageOnLoadMoreRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= ENGAGE-ON-GROW vs LOADMORE (2-col CachedStack, tail add) =========");
        try { RunCore(); }
        catch (Exception ex) { Console.WriteLine($"  CRASH: {ex}"); }
        Console.WriteLine("===========================================================================");
    }

    class Item
    {
        public int Id { get; init; }
    }

    /// <summary>Exposes the clamp and the fling animator state so the freeze can be READ, not guessed at.</summary>
    class ProbeScroll : SkiaScroll
    {
        public SKRect BoundsProbe => ContentOffsetBounds;
        public bool FlingRunning => _animatorFlingY != null && _animatorFlingY.IsRunning;
        public double FlingVelocity => _animatorFlingY?.CurrentVelocity ?? 0;
        public bool ScrollerRunning => _scrollerY != null && _scrollerY.IsRunning;
        public bool BounceRunning => _vectorAnimatorBounceY != null && _vectorAnimatorBounceY.IsRunning;
    }

    const int PageSize = 100;

    static void RunCore()
    {
        var bg = Colors.Black;
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: bg);

        var items = new ObservableRangeCollection<Item>(
            Enumerable.Range(1, PageSize).Select(i => new Item { Id = i }));

        SkiaLayout grid = null;
        ProbeScroll scroll = null;
        host.Canvas.Content = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new ProbeScroll
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    LoadMoreOffset = 200,
                    Content = new SkiaCachedStack
                    {
                        Tag = "Grid",
                        Split = 2,
                        Spacing = 10,
                        Padding = new Thickness(10, 12),
                        HorizontalOptions = LayoutOptions.Fill,
                        RecyclingTemplate = RecyclingTemplate.Enabled,
                        ReserveTemplates = 40,
                        ItemsSource = items,
                        ItemTemplate = new DataTemplate(() => new SkiaShape
                        {
                            Type = ShapeType.Rectangle,
                            CornerRadius = 8,
                            BackgroundColor = Colors.White,
                            HeightRequest = 270,
                            HorizontalOptions = LayoutOptions.Fill,
                            UseCache = SkiaCacheType.ImageDoubleBuffered,
                        }),
                    }.Assign(out grid)
                }.Assign(out scroll)
            }
        };

        var probe = new VirtualizationProbe(host, scroll, grid);
        var robot = new GestureRobot(host);

        for (int i = 0; i < 300 && grid.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(3); }
        Settle(host, grid, items);
        Console.WriteLine($"  start: items={items.Count} measured={grid.LastMeasuredIndex} content={probe.ContentHeight:0}");

        int failures = 0;

        // FAITHFUL LOADMORE: the app's command fires at LoadMoreOffset while the fling is still running and
        // the page lands ~250ms later (mock API delay + BeginInvokeOnMainThread). Engage therefore happens
        // on a MOVING viewport with background measurement in flight — settling first hides the bug.
        int pendingAddInFrames = -1;
        bool busy = false;
        scroll.LoadMoreCommand = new Command(() =>
        {
            if (busy || items.Count >= 1000)
                return;
            busy = true;
            pendingAddInFrames = 15; // ~250ms of frames before the page arrives
        });

        float worstFill = float.MaxValue;
        string worstAt = "";
        float baseFill = SettleFill(host, bg);
        Console.WriteLine($"  baseline fill={baseFill:0.000}");

        // FRAME COST: the window cut costs a visible spike on device. Time every RenderFrame so the spike
        // reports itself with its position relative to the engage, instead of being guessed at.
        var sw = new System.Diagnostics.Stopwatch();
        var frameCost = new List<(double ms, int items, int sinceAdd)>();
        int framesSinceAdd = int.MaxValue;

        // MOTION CONTINUITY: device reports the scroll STOPPING dead at the window cut. Track the per-frame
        // offset delta so a fling that dies at the engage shows up as deltas collapsing to 0 at sinceAdd 0-2
        // while the frames before it were still moving.
        double prevOffset = scroll.ViewportOffsetY;
        var motionAtEngage = new List<string>();
        bool engageSeen = false;

        for (int step = 0; step < 400 && items.Count < 400; step++)
        {
            robot.Pan(220, 800, 220, 180, durationMs: 60, steps: 6);

            for (int f = 0; f < 10; f++)
            {
                if (pendingAddInFrames > 0 && --pendingAddInFrames == 0)
                {
                    int before = items.Count;
                    bool engaging = before + PageSize >= 300 && before < 300;
                    items.AddRange(Enumerable.Range(before + 1, PageSize).Select(i => new Item { Id = i }));
                    busy = false;
                    framesSinceAdd = 0;
                    if (engaging)
                        engageSeen = true;
                    Console.WriteLine($"  ADD {before} -> {items.Count}{(engaging ? "   <== ENGAGE" : "")}" +
                                      $"  offset={scroll.ViewportOffsetY:0} vis=[{grid.FirstVisibleIndex}..{grid.LastVisibleIndex}]");
                }

                sw.Restart();
                host.RenderFrame(16);
                sw.Stop();
                frameCost.Add((sw.Elapsed.TotalMilliseconds, items.Count, framesSinceAdd));

                double now = scroll.ViewportOffsetY;
                double moved = Math.Abs(now - prevOffset);
                prevOffset = now;
                if (engageSeen && motionAtEngage.Count < 12)
                    motionAtEngage.Add($"      +{framesSinceAdd,2} moved={moved,7:0.00}px offset={now:0} boundsTop={scroll.BoundsProbe.Top:0} fling={(scroll.FlingRunning ? "RUN" : "off")} v={scroll.FlingVelocity:0} scroller={(scroll.ScrollerRunning ? "RUN" : "off")} bounce={(scroll.BounceRunning ? "RUN" : "off")} pending={grid.HasPendingStructureChanges}");

                if (framesSinceAdd < int.MaxValue)
                    framesSinceAdd++;
                Thread.Sleep(3);

                float fill = (float)host.NonBackgroundFraction(bg);
                if (fill < worstFill)
                {
                    worstFill = fill;
                    worstAt = $"items={items.Count} offset={scroll.ViewportOffsetY:0} content={probe.ContentHeight:0} " +
                              $"vis=[{grid.FirstVisibleIndex}..{grid.LastVisibleIndex}] measured={grid.LastMeasuredIndex}";
                }
            }
        }

        Console.WriteLine($"  worst fill during continuous fling + paging: {worstFill:0.000}");
        Console.WriteLine($"    at {worstAt}");

        // Frame-cost report: median as the steady-state reference, then the worst frames with how many
        // frames after an ADD they landed — a spike AT the cut shows up as sinceAdd 0..2.
        var sorted = frameCost.Select(x => x.ms).OrderBy(x => x).ToList();
        double median = sorted.Count > 0 ? sorted[sorted.Count / 2] : 0;
        if (motionAtEngage.Count > 0)
        {
            Console.WriteLine("  motion across the window cut (frame offset delta):");
            foreach (var line in motionAtEngage)
                Console.WriteLine(line);
        }

        Console.WriteLine($"  frames={frameCost.Count} median={median:0.00}ms p99={(sorted.Count > 0 ? sorted[(int)(sorted.Count * 0.99)] : 0):0.00}ms");
        foreach (var (ms, count, since) in frameCost.OrderByDescending(x => x.ms).Take(8))
            Console.WriteLine($"    SPIKE {ms:0.00}ms  items={count} framesAfterAdd={(since == int.MaxValue ? -1 : since)}  (x{ms / Math.Max(median, 0.01):0.0} median)");

        Console.WriteLine($"  final {grid.DebugString}");

        if (worstFill < baseFill * 0.5f)
        {
            failures++;
            Console.WriteLine($"  FAIL screen went dark (fill {baseFill:0.000} -> {worstFill:0.000})");
        }

        Console.WriteLine(failures == 0
            ? "=> PASS (never went dark across paging + engage while flinging)"
            : $"=> FAIL ({failures} problems)");
    }

    /// <summary>Render until the double-buffered cell bakes have landed, then report the painted fraction.</summary>
    static float SettleFill(HeadlessCanvasHost host, Color bg)
    {
        float fill = 0;
        for (int i = 0; i < 40; i++)
        {
            host.AdvanceFrames(3, 16);
            Thread.Sleep(20);
            fill = (float)host.NonBackgroundFraction(bg);
            if (fill > 0.05f)
                break;
        }

        return fill;
    }

    static void Settle(HeadlessCanvasHost host, SkiaLayout grid, IList<Item> items)
    {
        for (int i = 0; i < 250; i++)
        {
            host.RenderFrame(16);
            Thread.Sleep(2);
            if (grid.LastMeasuredIndex >= items.Count - 1)
                break;
        }
    }
}
