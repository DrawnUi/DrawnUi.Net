using System.Reflection;
using DrawnChatList;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Repro for the reported streaming-AI-answer corruption: a cell growing in height at runtime
/// (mock AI reply streaming words into the newest bubble) transiently paints at a corrupted position /
/// overlaps its neighbor on MAUI, permanently on Blazor. Harness (Net head, worker threads ALIVE)
/// reproduces the MAUI-class transient. Per rendered frame this samples BOTH truths:
///   - structure truth: consecutive ControlInStack.Drawn rects (what layout thinks),
///   - gesture truth:   RenderTree rects (what taps hit),
/// and flags any vertical overlap between consecutive cells, together with the growing cell's stage
/// (NeedMeasure / IsPreparingOffthread / slot-vs-view size mismatch) and the plane state (blit vs live),
/// so the guilty pipeline stage is identified by EVIDENCE, not speculation.
/// </summary>
public static class StreamingGrowthRepro
{
    private static readonly FieldInfo _fPreparing =
        typeof(SkiaControl).GetField("IsPreparingOffthread", BindingFlags.Instance | BindingFlags.NonPublic);

    private static float _lastSlotH = -2, _lastViewH = -2;

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("============== STREAMING GROWTH (mock AI answer) ==============");

        var page = new ChatPage();
        using var host = new HeadlessCanvasHost(440, 920, scale: 2f, background: ChatTheme.Bg);
        host.Canvas.Content = page.CreateCanvasContent();
        page.InitializeList();

        for (int i = 0; i < 400 && page.ChatStack.LastVisibleIndex < 0; i++)
        {
            host.RenderFrame(16);
            Thread.Sleep(4);
        }

        host.AdvanceFrames(30, 16); // settle: prep pipeline warm, plane recorded

        var stack = page.ChatStack;

        // plane internals (SkiaCachedStack) — read-only probes
        var tCached = stack.GetType();
        var fCacheValid = FindField(tCached, "_cacheValid");
        var fBakeInFlight = FindField(tCached, "_bakeInFlight");
        var fContentChanged = FindField(tCached, "_contentChanged");

        // lib's built-in per-index tracer: follow the growing cell (index 0) through measures/shifts
        SkiaLayout.DebugTraceIndex = 0;

        // kick the mock AI stream (private by design — same action the Dev picker triggers)
        var mi = typeof(ChatPage).GetMethod("StartMockAiAnswer", BindingFlags.Instance | BindingFlags.NonPublic)!;
        mi.Invoke(page, null);
        Console.WriteLine("   mock AI stream started (grows newest bubble ~5s @180ms/word)");

        int overlapStructFrames = 0, overlapTreeFrames = 0;
        float worstStruct = 0, worstTree = 0;
        string worstStructLine = "", worstTreeLine = "";
        var stageHits = new Dictionary<string, int>();

        long deadline = Environment.TickCount64 + 6500;
        int frame = 0;

        while (Environment.TickCount64 < deadline)
        {
            host.RenderFrame(16);
            frame++;
            Thread.Sleep(8); // let worker threads (prep/bake) interleave like on device

            // growing cell = local index 0 (inverted chat: newest first in stack coords)
            var growView = stack.ChildrenFactory.PeekRealizedViewForIndex(0);
            var need = growView?.NeedMeasure ?? false;
            var prep = growView != null && _fPreparing != null && (bool)_fPreparing.GetValue(growView)!;
            var viewH = growView?.MeasuredSize.Pixels.Height ?? -1;

            var structure = stack.GetStackStructure();
            float slotH = -1;
            string stage;

            // ---- structure truth: consecutive Drawn rects
            float overS = 0;
            int overSa = -1;
            ControlInStack prev = null;
            if (structure != null)
            {
                foreach (var cell in structure.GetChildren())
                {
                    if (!cell.WasLastDrawn)
                    {
                        prev = null;
                        continue;
                    }

                    if (cell.ControlIndex == 0)
                        slotH = cell.Measured.Pixels.Height;

                    if (prev != null && cell.ControlIndex == prev.ControlIndex + 1)
                    {
                        // vertical stack: next cell must start at/after previous bottom
                        var o = prev.Drawn.Bottom - cell.Drawn.Top;
                        if (o > 1f && o > overS)
                        {
                            overS = o;
                            overSa = prev.ControlIndex;
                        }
                    }

                    prev = cell;
                }
            }

            stage = need
                ? (prep ? "worker-measuring" : "needmeasure-unclaimed")
                : (slotH >= 0 && viewH >= 0 && Math.Abs(slotH - viewH) > 1f
                    ? "measured-slot-mismatch" // worker done, structure not reconciled yet
                    : "consistent");

            // ---- gesture truth: RenderTree rects
            float overT = 0;
            int overTa = -1;
            SkiaControlWithRect prevT = default;
            bool hasPrevT = false;
            var tree = stack.RenderTree;
            if (tree != null)
            {
                foreach (var node in tree)
                {
                    // only adjacent stack indices are meaningful — the tree can interleave
                    // non-cell nodes / out-of-order entries
                    if (hasPrevT && node.Index == prevT.Index + 1)
                    {
                        var o = prevT.Rect.Bottom - node.Rect.Top;
                        if (o > 1f && o > overT)
                        {
                            overT = o;
                            overTa = prevT.Index;
                        }
                    }

                    prevT = node;
                    hasPrevT = true;
                }
            }

            bool cacheValid = fCacheValid != null && (bool)fCacheValid.GetValue(stack)!;
            bool baking = fBakeInFlight != null && (bool)fBakeInFlight.GetValue(stack)!;
            bool dirty = fContentChanged != null && (bool)fContentChanged.GetValue(stack)!;

            // timeline: log every slot/view height transition of the growing cell — shows whether the
            // reconcile ever ADOPTS (slot catches up to view) and whether something REVERTS it after
            if (Math.Abs(slotH - _lastSlotH) > 0.5f || Math.Abs(viewH - _lastViewH) > 0.5f)
            {
                Console.WriteLine(
                    $"   f{frame,3}: slotH {_lastSlotH:0}->{slotH:0} viewH {_lastViewH:0}->{viewH:0} " +
                    $"need={need} prep={prep} overS={overS:0} plane[valid={cacheValid} baking={baking} dirty={dirty}]");
                _lastSlotH = slotH;
                _lastViewH = viewH;
            }

            if (overS > 0)
            {
                overlapStructFrames++;
                Bump(stageHits, "struct:" + stage);
                if (overS > worstStruct)
                {
                    worstStruct = overS;
                    worstStructLine =
                        $"   WORST-STRUCT f{frame}: over={overS:0.0}px after#{overSa} stage={stage} " +
                        $"slotH={slotH:0} viewH={viewH:0} need={need} prep={prep} " +
                        $"plane[valid={cacheValid} baking={baking} dirty={dirty}]";
                }
            }

            if (overT > 0)
            {
                overlapTreeFrames++;
                Bump(stageHits, "tree:" + stage);
                if (overT > worstTree)
                {
                    worstTree = overT;
                    worstTreeLine =
                        $"   WORST-TREE   f{frame}: over={overT:0.0}px after#{overTa} stage={stage} " +
                        $"slotH={slotH:0} viewH={viewH:0} need={need} prep={prep} " +
                        $"plane[valid={cacheValid} baking={baking} dirty={dirty}]";
                }
            }
        }

        Console.WriteLine($"   frames={frame} overlapFrames struct={overlapStructFrames} renderTree={overlapTreeFrames}");
        foreach (var kvp in stageHits.OrderByDescending(k => k.Value))
            Console.WriteLine($"   stage {kvp.Key}: {kvp.Value} frames");
        if (worstStructLine != "") Console.WriteLine(worstStructLine);
        if (worstTreeLine != "") Console.WriteLine(worstTreeLine);

        Console.WriteLine(overlapStructFrames + overlapTreeFrames > 0
            ? "=> REPRODUCED: cells overlapped during streaming growth (see stages above)"
            : "=> no overlap detected during streaming growth");
        Console.WriteLine("=========================================================");

        SkiaLayout.DebugTraceIndex = -1;
    }

    private static FieldInfo FindField(Type t, string name)
    {
        while (t != null)
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null)
                return f;
            t = t.BaseType;
        }

        return null;
    }

    private static void Bump(Dictionary<string, int> map, string key)
    {
        map.TryGetValue(key, out var v);
        map[key] = v + 1;
    }
}
