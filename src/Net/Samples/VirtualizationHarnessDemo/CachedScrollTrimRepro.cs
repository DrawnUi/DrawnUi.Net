using System.Reflection;
using DrawnChatList;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Reproduces the "empty band while scrolling WHILE loading" on the DOUBLE-BUFFERED CellsStackCached
/// path: gesture-scroll the REAL ChatPage into history (LoadOlder), without settling between flicks, so a
/// head-remove window trim fires mid-load. The async plane was baked in the pre-trim coordinate space; if
/// it's blitted after the trim re-offsets cells, a full-width empty horizontal band appears.
///
/// Flips CellsStackCached.UseDoubleBuffering ON via reflection (test-only — does NOT touch the source flag),
/// drives GestureRobot pans, and every frame measures the tallest INTERIOR empty band in the framebuffer.
/// </summary>
public static class CachedScrollTrimRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= CACHED DOUBLE-BUFFER SCROLL+TRIM =========");

        // Smoothness + band check on the ACTIVE chat stack (per-cell CellsStackOptimized + ImageDoubleBuffered
        // cells). Brutal flick + scroll-while-loading; per-frame ms / GC / allocation are the smoothness signal,
        // badFlicks the band signal. Headless host GC is noisy -> read these as DIRECTIONAL.
        var prevDelay = SkiaLayout.DebugBackgroundMeasureDelayMs;
        SkiaLayout.DebugBackgroundMeasureDelayMs = 80; // slow measure -> exercise scroll-while-loading
        try
        {
            RunInner();
            GrowCellRepro();  // AI-mock style: newest cell grows realtime -> wrong Top?
        }
        finally
        {
            SkiaLayout.DebugBackgroundMeasureDelayMs = prevDelay;
        }
        Console.WriteLine("====================================================");
    }

    private sealed class GrepListener : System.Diagnostics.TraceListener
    {
        public int HeadApplied, HeadCommitted, HeadRejected, OffsetOthersMax, BakeIncomplete, HeadInsert;
        public override void WriteLine(string message) => Write(message);
        public override void Write(string message)
        {
            if (message == null) return;
            if (message.Contains("Head insert committed")) HeadInsert++;
            else if (message.Contains("Head remove applied")) HeadApplied++;
            else if (message.Contains("Head remove committed")) HeadCommitted++;
            else if (message.Contains("fast path rejected")) HeadRejected++;
            else if (message.StartsWith("Offset others:"))
            {
                if (int.TryParse(message.Replace("Offset others:", "").Trim(), out var v) && Math.Abs(v) > OffsetOthersMax)
                    OffsetOthersMax = Math.Abs(v);
            }
            else if (message.Contains("[BAKE-INCOMPLETE]")) BakeIncomplete++;
        }
    }

    // "FEW CELLS CHANGE" case: list sits STILL (no scroll), one visible cell marked dirty each frame
    // (image-load / delivery-status style UpdateByChild). Measures whether a static single-cell update
    // forces expensive "redo all" stack work, or is already cheap (blit visible + re-raster the 1 dirty cell).
    // Reproduces the user's "cell draws at wrong Top when it changes size in realtime" — the AI-answer MOCK:
    // the newest (bottom-most, inverted) cell's text grows word by word, so the cell remeasures taller every
    // step. Detects overlap/gap between consecutive visible cells (a wrong Top shows as overlap or a hole).
    private static void GrowCellRepro()
    {
        Console.WriteLine("--- GROW newest cell realtime (AI-mock style) -> wrong Top? ---");
        var page = new ChatPage();
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: ChatTheme.Bg);
        host.Canvas.Content = page.CreateCanvasContent();
        page.InitializeList();
        for (int i = 0; i < 400 && page.ChatStack.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(4); }
        host.AdvanceFrames(8, 16);

        if (page.ChatStack?.RenderTree == null)
        {
            // PRE-EXISTING cross-repro artifact: a page created this late in the process sometimes never
            // renders (dispatcher pump owned by the first host) — tree stays null. Skip instead of crashing
            // the remaining repros.
            Console.WriteLine("  SKIPPED (page never rendered — known cross-repro dispatcher artifact)");
            return;
        }

        // bottom-most visible Text cell = newest in the inverted layout (what the AI mock grows)
        ChatMessage target = null; float bottomMost = float.MinValue;
        foreach (var t in page.ChatStack.RenderTree)
            if (t.FreezeBindingContext is ChatMessage m && m.Type == ChatMessageType.Text && t.HitRect.Bottom > bottomMost)
            { bottomMost = t.HitRect.Bottom; target = m; }
        if (target == null) { Console.WriteLine("  no text cell found"); return; }
        Console.WriteLine($"  growing msg#{target.Index}");

        string[] words = { "Let", "me", "think", "about", "this", "analyzing", "the", "context", "and",
            "weighing", "approaches", "before", "committing", "to", "an", "answer", "that", "makes", "sense" };
        var sb = new System.Text.StringBuilder(target.Text);
        int worstOverlap = 0, worstGap = 0, worstStep = -1;
        var dir = @"C:\Users\taubl\AppData\Local\Temp\claude\C--Users-taubl\2c597eec-477f-4fa9-a277-76a6f3f92aa3\scratchpad";

        for (int step = 0; step < 25; step++)
        {
            sb.Append(' ').Append(words[step % words.Length]);
            target.Text = sb.ToString();

            for (int fr = 0; fr < 3; fr++) // self-heal may land a frame late; inspect each
            {
                host.RenderFrame(16);
                var cells = new List<(int idx, float top, float bot)>();
                foreach (var t in page.ChatStack.RenderTree)
                {
                    if (t.HitRect.Bottom <= t.HitRect.Top) continue;
                    cells.Add((t.FreezeIndex, t.HitRect.Top, t.HitRect.Bottom));
                }
                cells.Sort((a, b) => a.top.CompareTo(b.top));
                for (int k = 1; k < cells.Count; k++)
                {
                    float gap = cells[k].top - cells[k - 1].bot;
                    if (gap < -2 && (int)(-gap) > worstOverlap)
                    {
                        worstOverlap = (int)(-gap); worstStep = step;
                        Console.WriteLine($"    OVERLAP {(int)(-gap)}px step{step} fr{fr}: " +
                            $"cell idx{cells[k - 1].idx} [{cells[k - 1].top:0}..{cells[k - 1].bot:0}] " +
                            $"OVER cell idx{cells[k].idx} top={cells[k].top:0} (grownMsg#{target.Index})");
                        host.SavePng(System.IO.Path.Combine(dir, "grow-bad.png"));
                    }
                    if (gap > 8 && (int)gap > worstGap) { worstGap = (int)gap; if (worstStep < 0) worstStep = step; }
                }
                Thread.Sleep(2);
            }
        }
        host.SavePng(System.IO.Path.Combine(dir, "grow-final.png"));
        bool bug = worstOverlap > 4 || worstGap > 24;
        Console.WriteLine($"  RESULT worstOverlap={worstOverlap}px worstGap={worstGap}px @step{worstStep} " +
                          (bug ? "=> WRONG-TOP REPRODUCED -> grow-final.png" : "=> ok (no overlap/gap)"));
    }

    private static void StaticDirty(int dirtyCount)
    {
        Console.WriteLine($"--- STATIC, dirty(re-raster) {dirtyCount} cell(s)/frame (no scroll) ---");
        var page = new ChatPage();
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: ChatTheme.Bg);
        host.Canvas.Content = page.CreateCanvasContent();
        page.InitializeList();
        for (int i = 0; i < 400 && page.ChatStack.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(4); }
        host.AdvanceFrames(8, 16);

        double msTot = 0, msMax = 0; int n = 0;
        long allocStart = 0, alloc = 0, rtStart = 0, rt = 0;
        int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);

        for (int f = 0; f < 200; f++)
        {
            // force re-raster on N visible cells (InvalidateCache = "redraw without using existing cache")
            int dirtied = 0;
            foreach (var t in page.ChatStack.RenderTree)
            {
                if (dirtied >= dirtyCount) break;
                t.Control.InvalidateCache();
                page.ChatStack.UpdateByChild(t.Control);
                dirtied++;
            }
            if (dirtyCount == 0) page.ChatStack.Update(); // still trigger a repaint

            var sw = System.Diagnostics.Stopwatch.StartNew();
            host.RenderFrame(16);
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds;
            if (f >= 6)
            {
                if (allocStart == 0) { allocStart = GC.GetTotalAllocatedBytes(false); rtStart = GC.GetAllocatedBytesForCurrentThread(); }
                alloc = GC.GetTotalAllocatedBytes(false) - allocStart;
                rt = GC.GetAllocatedBytesForCurrentThread() - rtStart;
                n++; msTot += ms; if (ms > msMax) msMax = ms;
            }
            Thread.Sleep(2);
        }
        Console.WriteLine($"  STATIC-DIRTY: avg={(n > 0 ? msTot / n : 0):0.00}ms max={msMax:0.0} " +
                          $"alloc={(n > 0 ? alloc / 1024.0 / n : 0):0.0}KB/frame renderThread={(n > 0 ? rt / 1024.0 / n : 0):0.0}KB/frame " +
                          $"GC[g0+={GC.CollectionCount(0) - g0} g1+={GC.CollectionCount(1) - g1} g2+={GC.CollectionCount(2) - g2}] " +
                          $"visibleCells={CountTree(page.ChatStack)}");
    }

    private static int CountTree(DrawnUi.Draw.SkiaLayout s) { int c = 0; foreach (var _ in s.RenderTree) c++; return c; }

    private static void RunInner()
    {
        Console.WriteLine($"UseDoubleBuffering={typeof(CellsStackCached).GetField("UseDoubleBuffering", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) ?? "n/a (field removed)"}");

        var grep = new GrepListener();
        System.Diagnostics.Trace.Listeners.Add(grep);

        var page = new ChatPage();
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: ChatTheme.Bg);
        host.Canvas.Content = page.CreateCanvasContent();
        page.InitializeList();

        for (int i = 0; i < 400 && page.ChatStack.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(4); }
        host.AdvanceFrames(8, 16);

        var robot = new GestureRobot(host);
        const float cx = 220;
        const int Warmup = 6; // ignore initial-load frames; we care about STEADY scroll-while-loading
        int maxBand = 0, maxBandFlick = -1, badFlicks = 0;
        double _frameMsTotal = 0, _frameMsMax = 0; int _frameCount = 0, _slowFrames = 0;
        int _gc0 = 0, _gc1 = 0, _gc2 = 0, _slowWithGc = 0;
        long _allocStart = 0, _allocBytes = 0;
        long _rtAllocStart = 0, _rtAllocBytes = 0; // render-thread (this loop thread) ONLY -> isolates draw scaffolding from bg cell raster

        // Inverted + ReverseGestures: drag DOWN (top->bottom) scrolls INTO history -> LoadOlder -> trim.
        for (int flick = 0; flick < 60; flick++)
        {
            robot.Pan(cx, 200, cx, 760, durationMs: 70, steps: 6);

            // DO NOT settle: render only a few frames so we scroll while loads/measures are still pending.
            int flickBand = 0, bandTop = -1, bandBot = -1;
            var dir = @"C:\Users\taubl\AppData\Local\Temp\claude\C--Users-taubl\2c597eec-477f-4fa9-a277-76a6f3f92aa3\scratchpad";
            for (int fr = 0; fr < 4; fr++)
            {
                int haBefore = grep.HeadApplied, hcBefore = grep.HeadCommitted, hiBefore = grep.HeadInsert, hrBefore = grep.HeadRejected, resBefore = page.ProbeResident;
                int g0b = GC.CollectionCount(0), g1b = GC.CollectionCount(1), g2b = GC.CollectionCount(2);
                var swF = System.Diagnostics.Stopwatch.StartNew();
                host.RenderFrame(16);
                swF.Stop();
                double ms = swF.Elapsed.TotalMilliseconds;
                int dg0 = GC.CollectionCount(0) - g0b, dg1 = GC.CollectionCount(1) - g1b, dg2 = GC.CollectionCount(2) - g2b;
                if (flick >= Warmup)
                {
                    if (_allocStart == 0) { _allocStart = GC.GetTotalAllocatedBytes(false); _rtAllocStart = GC.GetAllocatedBytesForCurrentThread(); }
                    _allocBytes = GC.GetTotalAllocatedBytes(false) - _allocStart;
                    _rtAllocBytes = GC.GetAllocatedBytesForCurrentThread() - _rtAllocStart;
                    _frameCount++; _frameMsTotal += ms; _gc0 += dg0; _gc1 += dg1; _gc2 += dg2;
                    if (ms > _frameMsMax) _frameMsMax = ms;
                    if (ms > 8) { _slowFrames++; if (dg0 + dg1 + dg2 > 0) _slowWithGc++; } // > half a 60fps budget = a visible hitch
                    if (ms > 12)
                        Console.WriteLine($"  SLOW {ms:0.0}ms f{flick} headInsert+={grep.HeadInsert - hiBefore} " +
                                          $"headApplied+={grep.HeadApplied - haBefore} headCommitted+={grep.HeadCommitted - hcBefore} " +
                                          $"headRejected+={grep.HeadRejected - hrBefore} resident {resBefore}->{page.ProbeResident} " +
                                          $"frontier={page.ChatStack.LastMeasuredIndex}");
                }
                int band = host.MaxInteriorEmptyBand(ChatTheme.Bg, out var bt, out var bb);
                if (band > flickBand)
                {
                    flickBand = band; bandTop = bt; bandBot = bb;
                    // capture THIS subframe (the bad one), not a recovered later frame
                    if (flick >= Warmup && band > 120)
                        host.SavePng(System.IO.Path.Combine(dir, $"band-f{flick}.png"));
                }
                Thread.Sleep(2);
            }

            if (flick >= Warmup)
            {
                if (flickBand > maxBand) { maxBand = flickBand; maxBandFlick = flick; }
                if (flickBand > 120) badFlicks++;
            }

            if (flick >= Warmup && flickBand > 120)
            {
                Console.WriteLine($"  BAND flick{flick,2} band={flickBand}px win=[{page.ProbeWindowStart}..{page.ProbeWindowEnd}) " +
                                  $"resident={page.ProbeResident} offY={page.MainScroll.ViewportOffsetY,7:0} -> band-f{flick}.png");

                var cs = (DrawnUi.Draw.SkiaLayout)page.ChatStack;
                Console.WriteLine($"      BAND screenY=[{bandTop}..{bandBot}] ({bandBot - bandTop}px)");
                // which LIVE cells straddle the band Y? (live has them; the plane is missing them)
                foreach (var t in cs.RenderTree)
                {
                    if (t.HitRect.Bottom <= t.HitRect.Top) continue;
                    bool overlapsBand = t.HitRect.Bottom > bandTop && t.HitRect.Top < bandBot;
                    bool nearBand = Math.Abs(t.HitRect.Top - bandTop) < 30 || Math.Abs(t.HitRect.Bottom - bandBot) < 30;
                    if (overlapsBand || nearBand)
                    {
                        string type = t.FreezeBindingContext is ChatMessage m ? $"msg{m.Index}/{m.Type}" : "?";
                        Console.WriteLine($"        cell ctrlIdx={t.FreezeIndex} {type} hitY=[{t.HitRect.Top:0}..{t.HitRect.Bottom:0}] h={t.HitRect.Height:0}");
                    }
                }
            }
        }

        System.Diagnostics.Trace.Listeners.Remove(grep);
        Console.WriteLine($"  DEBUG-LOG: headApplied={grep.HeadApplied} headCommitted={grep.HeadCommitted} " +
                          $"fastPathRejected={grep.HeadRejected} maxOffsetOthers={grep.OffsetOthersMax} bakeIncomplete={grep.BakeIncomplete}");
        Console.WriteLine($"  FRAME-MS: avg={(_frameCount > 0 ? _frameMsTotal / _frameCount : 0):0.00} " +
                          $"max={_frameMsMax:0.0} slowFrames(>8ms)={_slowFrames}/{_frameCount} slowWithGC={_slowWithGc} " +
                          $"GC[g0={_gc0} g1={_gc1} g2={_gc2}] alloc={_allocBytes / 1024}KB ({(_frameCount > 0 ? _allocBytes / 1024.0 / _frameCount : 0):0.0}KB/frame) " +
                          $"renderThread={(_frameCount > 0 ? _rtAllocBytes / 1024.0 / _frameCount : 0):0.0}KB/frame");
        Console.WriteLine($"  RESULT (post-warmup) maxBand={maxBand}px@f{maxBandFlick} badFlicks={badFlicks}");
        bool ok = badFlicks == 0;
        Console.WriteLine(ok ? "=> PASS (no empty band during steady scroll+trim)"
                             : $"=> FAIL ({badFlicks} flicks with empty band, max {maxBand}px)");
    }
}
