using DrawnChatList;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// User-reported corruption flow: delete a visible message at startup, several ScrollToOldest /
/// ScrollToNewest jump cycles, then from the oldest scroll DOWN toward newest (LoadNewer paging +
/// tail trims) — duplicate cells appeared ("message 41 twice, overlapping") around the first
/// LoadNewer boundaries. Asserts: no duplicate indices, sequence consecutive, no overlaps.
/// </summary>
public static class DeleteJumpCycleRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("============== DELETE + JUMP CYCLES + LOADNEWER ==============");

        var page = new ChatPage();
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: ChatTheme.Bg);
        host.Canvas.Content = page.CreateCanvasContent();
        page.InitializeList();

        for (int i = 0; i < 400 && page.ChatStack.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(4); }
        Frames(host, 30);

        // 1. delete a visible message at startup
        ChatMessage victim = null;
        var tree = page.ChatStack.RenderTree;
        if (tree != null)
            foreach (var t in tree)
                if (t.FreezeBindingContext is ChatMessage m && t.HitRect.Top > 100 && t.HitRect.Bottom < 800)
                {
                    victim = m;
                    break;
                }

        if (victim == null)
        {
            Console.WriteLine("=> FAIL (no startup victim)");
            return;
        }

        Console.WriteLine($"startup delete Index={victim.Index}");
        page.DeleteMessage(victim);
        Frames(host, 60);

        // 2. several jump cycles oldest <-> newest
        for (int cycle = 0; cycle < 3; cycle++)
        {
            page.ProbeScrollToOldest(true);
            WaitWindowStart(page, host, 0);
            Settle(page, host);
            page.ProbeScrollToNewest(true);
            Settle(page, host);
        }

        // 3. land at oldest, then scroll DOWN toward newest through LoadNewer cycles
        page.ProbeScrollToOldest(true);
        WaitWindowStart(page, host, 0);
        Settle(page, host);

        var robot = new GestureRobot(host);
        bool corrupted = false;
        for (int k = 0; k < 12 && !corrupted; k++)
        {
            robot.Pan(215, 820, 215, 250, durationMs: 100, steps: 8); // toward newest
            for (int f = 0; f < 45; f++) { host.RenderFrame(16); Thread.Sleep(3); }
            corrupted |= !CheckTree(page, $"pan{k}");
        }

        Frames(host, 90);
        corrupted |= !CheckTree(page, "final");

        Console.WriteLine(corrupted
            ? "=> FAIL (duplicate/overlap/sequence corruption reproduced)"
            : "=> PASS (no duplicates through delete + jumps + LoadNewer)");
        Console.WriteLine("==============================================================");
    }

    static void Frames(HeadlessCanvasHost host, int n)
    {
        for (int f = 0; f < n; f++) { host.RenderFrame(16); Thread.Sleep(3); }
    }

    static void WaitWindowStart(ChatPage page, HeadlessCanvasHost host, int start)
    {
        for (int f = 0; f < 300 && page.ProbeWindowStart != start; f++) { host.RenderFrame(16); Thread.Sleep(3); }
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

        // duplicates
        var seen = new HashSet<int>();
        foreach (var c in seq)
        {
            if (!seen.Add(c.idx))
            {
                Console.WriteLine($"   [{label}] DUPLICATE cell for msg {c.idx}!");
                return false;
            }
        }

        // sequence (pre-rotation space: descending; -2 once = deleted index)
        seq.Sort((a, b) => a.top.CompareTo(b.top));
        int skips = 0;
        for (int i = 1; i < seq.Count; i++)
        {
            var diff = seq[i].idx - seq[i - 1].idx;
            if (diff == -1) continue;
            if (diff == -2 && skips++ == 0) continue;
            Console.WriteLine($"   [{label}] BAD SEQUENCE {seq[i - 1].idx} -> {seq[i].idx} (diff {diff})");
            return false;
        }

        // overlaps
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
