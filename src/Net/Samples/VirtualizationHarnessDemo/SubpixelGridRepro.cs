using DrawnChatList;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Device: "saw"/tearing while scrolling the chat — neighbors seem to move by slightly different amounts,
/// worst in regions where cells are reused/trimmed. Theory: MIXED FRACTIONAL GEOMETRY — some cell tops are
/// on integer pixels (initial arrange rounds), others go fractional (raw measured heights, trim/rebase
/// translates, remeasure offsets), so during scroll neighbors land on different subpixel phases.
///
/// Measures, per rendered frame while gesture-scrolling the REAL ChatPage:
///   1. PHASE SPREAD  = max-min of frac(cell.HitRect.Top) across visible cells. 0 = uniform grid.
///   2. MOTION SPREAD = max-min of (cell.top - same cell's top last frame) across cells visible in both
///      frames. 0 = whole content moves rigidly. > 0 = TEARING (neighbors moved different amounts).
/// Correlates first divergence with head-insert/head-remove commits (Trace grep).
/// </summary>
public static class SubpixelGridRepro
{
    private sealed class RebaseListener : System.Diagnostics.TraceListener
    {
        public int HeadInsert, HeadRemove;
        public override void WriteLine(string message) => Write(message);
        public override void Write(string message)
        {
            if (message == null) return;
            if (message.Contains("Head insert committed")) HeadInsert++;
            else if (message.Contains("Head remove committed")) HeadRemove++;
        }
    }

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= SUBPIXEL GRID / TEARING PROBE =========");

        var grep = new RebaseListener();
        System.Diagnostics.Trace.Listeners.Add(grep);
        try { RunInner(grep); }
        finally { System.Diagnostics.Trace.Listeners.Remove(grep); }

        Console.WriteLine("=================================================");
    }

    private static void RunInner(RebaseListener grep)
    {
        var page = new ChatPage();
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: ChatTheme.Bg);
        host.Canvas.Content = page.CreateCanvasContent();
        page.InitializeList();

        for (int i = 0; i < 400 && page.ChatStack.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(4); }
        host.AdvanceFrames(8, 16);

        if (page.ChatStack?.RenderTree == null)
        {
            Console.WriteLine("  SKIPPED (page never rendered — known cross-repro dispatcher artifact)");
            return;
        }

        var robot = new GestureRobot(host);

        float worstPhase = 0, worstMotion = 0;
        int worstPhaseFrame = -1, worstMotionFrame = -1;
        int worstPhaseIns = 0, worstPhaseRem = 0, worstMotionIns = 0, worstMotionRem = 0;
        int frame = 0, firstMixedFrame = -1;

        var prevTops = new Dictionary<int, float>();

        // flick into history repeatedly (LoadOlder + window trims fire), sampling every frame
        for (int round = 0; round < 30; round++)
        {
            robot.Pan(215, 300, 215, 720, durationMs: 90, steps: 8); // inverted chat: pan down = older
            for (int f = 0; f < 25; f++)
            {
                host.RenderFrame(16);
                frame++;

                var tops = new Dictionary<int, float>();
                foreach (var t in page.ChatStack.RenderTree)
                {
                    if (t.HitRect.Bottom <= t.HitRect.Top) continue;
                    tops[t.FreezeIndex] = t.HitRect.Top;
                }
                if (tops.Count < 3) { prevTops = tops; continue; }

                // 1. phase spread within this frame
                float minFrac = 1f, maxFrac = 0f;
                foreach (var kv in tops)
                {
                    var fr = kv.Value - (float)Math.Floor(kv.Value);
                    if (fr < minFrac) minFrac = fr;
                    if (fr > maxFrac) maxFrac = fr;
                }
                // phases wrap (0.99 vs 0.01 are 0.02 apart) — take min of direct and wrapped spread
                var spread = Math.Min(maxFrac - minFrac, 1f - (maxFrac - minFrac));
                if (spread > worstPhase)
                {
                    worstPhase = spread; worstPhaseFrame = frame;
                    worstPhaseIns = grep.HeadInsert; worstPhaseRem = grep.HeadRemove;
                }
                if (spread > 0.05f && firstMixedFrame < 0)
                {
                    firstMixedFrame = frame;
                    Console.WriteLine($"  first MIXED grid @frame {frame} spread={spread:0.000} " +
                                      $"(headInsert={grep.HeadInsert} headRemove={grep.HeadRemove})");
                }

                // 2. motion spread vs previous frame (rigid scroll = all deltas equal)
                float minD = float.MaxValue, maxD = float.MinValue; int common = 0;
                foreach (var kv in tops)
                {
                    if (!prevTops.TryGetValue(kv.Key, out var was)) continue;
                    var d = kv.Value - was;
                    if (d < minD) minD = d;
                    if (d > maxD) maxD = d;
                    common++;
                }
                if (common >= 3)
                {
                    var mspread = maxD - minD;
                    if (mspread > worstMotion && Math.Abs(maxD) < 200 && Math.Abs(minD) < 200) // ignore rebase teleports
                    {
                        worstMotion = mspread; worstMotionFrame = frame;
                        worstMotionIns = grep.HeadInsert; worstMotionRem = grep.HeadRemove;
                        if (mspread > 0.4f)
                        {
                            Console.WriteLine($"  TEAR @frame {frame}: neighbors moved {minD:0.00}..{maxD:0.00}px " +
                                              $"(spread {mspread:0.00}) ins={grep.HeadInsert} rem={grep.HeadRemove}");
                            // which cell diverges? dump per-cell deltas ordered by screen position
                            foreach (var kv in tops.OrderBy(k => k.Value))
                            {
                                if (!prevTops.TryGetValue(kv.Key, out var w)) continue;
                                Console.WriteLine($"    cell idx={kv.Key} top {w:0.000} -> {kv.Value:0.000} delta={kv.Value - w:0.000}");
                            }
                        }
                    }
                }

                prevTops = tops;
                Thread.Sleep(2);
            }
        }

        Console.WriteLine($"  RESULT frames={frame} rebases: ins={grep.HeadInsert} rem={grep.HeadRemove}");
        Console.WriteLine($"  PHASE  worstSpread={worstPhase:0.000} @frame {worstPhaseFrame} (ins={worstPhaseIns} rem={worstPhaseRem}) " +
                          (worstPhase <= 0.05f ? "=> UNIFORM grid" : "=> MIXED grid (subpixel phases diverge)"));
        Console.WriteLine($"  MOTION worstSpread={worstMotion:0.00}px @frame {worstMotionFrame} (ins={worstMotionIns} rem={worstMotionRem}) " +
                          (worstMotion <= 0.4f ? "=> rigid scroll, no tearing" : "=> TEARING reproduced"));
    }
}
