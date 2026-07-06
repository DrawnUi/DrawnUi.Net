using DrawnChatList;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Prepared-views pipeline (SkiaLayout.UsePreparedViews): the render thread must NEVER measure a
/// templated cell — cells are bound+measured off-thread by CellPreparationService, unprepared cells
/// draw a skeleton for a frame or two. Real ChatPage (flag enabled there). Flow: settle -> flings
/// into history through LoadMore boundaries -> flings back toward newest -> jump to oldest -> final
/// settle. Asserts: SkiaLayout.CountRenderThreadCellMeasures stays 0 for the WHOLE run, tree
/// integrity after every fling (no duplicates/holes/overlaps — also proves skeletons materialized,
/// since unprepared cells don't enter the render tree), and full viewport coverage at rest.
/// </summary>
public static class PreparedViewsRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("============== PREPARED VIEWS (no render-thread measures) ==============");

        var page = new ChatPage();
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: ChatTheme.Bg);
        host.Canvas.Content = page.CreateCanvasContent();
        page.InitializeList();

        for (int i = 0; i < 400 && page.ChatStack.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(4); }
        Frames(host, 30);

        if (!page.ChatStack.UsePreparedViews)
        {
            Console.WriteLine("=> FAIL (ChatPage does not enable UsePreparedViews — repro void)");
            return;
        }

        // startup at rest = known-good coverage baseline (accounts for page chrome/padding overlap)
        var baselineCoverage = CoveredHeight(page);

        var robot = new GestureRobot(host);
        bool corrupted = false;

        // into history (inverted list: drag down = older, indices grow)
        for (int k = 0; k < 10 && !corrupted; k++)
        {
            robot.Pan(215, 250, 215, 820, durationMs: 100, steps: 8);
            for (int f = 0; f < 45; f++) { host.RenderFrame(16); Thread.Sleep(3); }
            corrupted |= !CheckTree(page, $"older{k}");
        }

        // back toward newest
        for (int k = 0; k < 6 && !corrupted; k++)
        {
            robot.Pan(215, 820, 215, 250, durationMs: 100, steps: 8);
            for (int f = 0; f < 45; f++) { host.RenderFrame(16); Thread.Sleep(3); }
            corrupted |= !CheckTree(page, $"newer{k}");
        }

        // cold jump to oldest, settle, jump back
        page.ProbeScrollToOldest(true);
        for (int f = 0; f < 300 && page.ProbeWindowStart != 0; f++) { host.RenderFrame(16); Thread.Sleep(3); }
        Settle(page, host);
        corrupted |= !CheckTree(page, "at-oldest");
        page.ProbeScrollToNewest(true);
        Settle(page, host);
        Frames(host, 60);
        corrupted |= !CheckTree(page, "final");

        // full viewport coverage at rest (no lingering skeleton = every visible cell is real & in tree);
        // compare against the startup-at-rest baseline so page chrome doesn't skew the check
        var covered = CoveredHeight(page);
        bool coverageOk = covered >= Math.Min(baselineCoverage - 8, 920 * 0.85f);
        if (!coverageOk)
            Console.WriteLine($"   [final] viewport coverage only {covered:0}px (baseline {baselineCoverage:0}) — skeleton never materialized?");

        var renderMeasures = page.ChatStack.CountRenderThreadCellMeasures;
        Console.WriteLine($"render-thread cell measures: {renderMeasures} (must be 0)  coverage={covered:0}px (baseline {baselineCoverage:0})");

        bool pass = renderMeasures == 0 && !corrupted && coverageOk;
        Console.WriteLine(pass
            ? "=> PASS (zero render-thread measures, tree intact, viewport full)"
            : "=> FAIL (prepared-views invariant broken)");
        Console.WriteLine("========================================================================");
    }

    static void Frames(HeadlessCanvasHost host, int n)
    {
        for (int f = 0; f < n; f++) { host.RenderFrame(16); Thread.Sleep(3); }
    }

    static void Settle(ChatPage page, HeadlessCanvasHost host)
    {
        float last = float.NaN;
        int stable = 0;
        for (int f = 0; f < 600; f++)
        {
            host.RenderFrame(16); Thread.Sleep(3);
            var off = page.MainScroll.ViewportOffsetY;
            if (!page.MainScroll.OrderedScrollToIndexIsSet && Math.Abs(off - last) < 0.5f)
            {
                if (++stable >= 12) return;
            }
            else stable = 0;
            last = off;
        }
    }

    static float CoveredHeight(ChatPage page)
    {
        var tree = page.ChatStack.RenderTree;
        if (tree == null) return 0;
        float top = float.MaxValue, bottom = float.MinValue;
        foreach (var t in tree)
            if (t.FreezeBindingContext is ChatMessage)
            {
                top = Math.Min(top, t.HitRect.Top);
                bottom = Math.Max(bottom, t.HitRect.Bottom);
            }

        if (top >= bottom) return 0;
        return Math.Min(bottom, 920) - Math.Max(top, 0);
    }

    /// <summary>Duplicate indices, non-consecutive sequence or overlapping cells => corrupted.</summary>
    static bool CheckTree(ChatPage page, string label)
    {
        var seq = new List<(float top, float bottom, int idx)>();
        var tree = page.ChatStack.RenderTree;
        if (tree == null) return true;
        foreach (var t in tree)
            if (t.FreezeBindingContext is ChatMessage m)
                seq.Add((t.HitRect.Top, t.HitRect.Bottom, m.Index));
        if (seq.Count < 3) return true;

        var seen = new HashSet<int>();
        foreach (var c in seq)
        {
            if (!seen.Add(c.idx))
            {
                Console.WriteLine($"   [{label}] DUPLICATE cell for msg {c.idx}!");
                return false;
            }
        }

        // sequence (pre-rotation space: descending)
        seq.Sort((a, b) => a.top.CompareTo(b.top));
        for (int i = 1; i < seq.Count; i++)
        {
            var diff = seq[i].idx - seq[i - 1].idx;
            if (diff == -1) continue;
            Console.WriteLine($"   [{label}] BAD SEQUENCE {seq[i - 1].idx} -> {seq[i].idx} (diff {diff})");
            return false;
        }

        float prevBottom = float.NaN;
        foreach (var c in seq)
        {
            if (!float.IsNaN(prevBottom) && prevBottom - c.top > 2f)
            {
                Console.WriteLine($"   [{label}] OVERLAP {prevBottom - c.top:0}px at msg {c.idx}");
                return false;
            }

            prevBottom = Math.Max(float.IsNaN(prevBottom) ? c.bottom : prevBottom, c.bottom);
        }

        return true;
    }
}
