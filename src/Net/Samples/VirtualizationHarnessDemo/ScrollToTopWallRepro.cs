using DrawnChatList;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// User-reported wall: manually scrolling into history, around message ~176 the scroll stopped at the
/// content edge and NEVER extended again (LoadOlder committed but blit-mode frames never ran the live
/// pipeline that integrates batches and grows the content extent, so SkiaScroll's load-more never
/// re-armed). Repro: pan into history relentlessly until the OLDEST message (Index 0) is on screen;
/// assert progress never stalls for long and the top is reachable.
/// </summary>
public static class ScrollToTopWallRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= SCROLL-TO-TOP WALL (pan whole history) =========");

        var page = new ChatPage();
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: ChatTheme.Bg);
        host.Canvas.Content = page.CreateCanvasContent();
        page.InitializeList();

        for (int i = 0; i < 400 && page.ChatStack.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(4); }
        for (int f = 0; f < 40; f++) { host.RenderFrame(16); Thread.Sleep(3); }

        var robot = new GestureRobot(host);
        bool reachedTop = false;
        float lastOff = float.NaN;
        int stalledPans = 0, pans = 0;

        for (; pans < 160 && !reachedTop; pans++)
        {
            robot.Pan(215, 250, 215, 820, durationMs: 90, steps: 8); // inverted: drag down = into history
            for (int f = 0; f < 30; f++) { host.RenderFrame(16); Thread.Sleep(3); }

            reachedTop = AtOldestTop(page);

            var off = page.MainScroll.ViewportOffsetY;
            if (!float.IsNaN(lastOff) && Math.Abs(off - lastOff) < 4f && page.ProbeWindowStart == _lastWinStart)
            {
                if (++stalledPans >= 12) // ~12 consecutive no-progress pans = the wall
                    break;
            }
            else
            {
                stalledPans = 0;
            }

            lastOff = off;
            _lastWinStart = page.ProbeWindowStart;
        }

        // settle + final verdict
        for (int f = 0; f < 90; f++) { host.RenderFrame(16); Thread.Sleep(3); }
        reachedTop |= AtOldestTop(page);

        Console.WriteLine($"pans={pans} win=[{page.ProbeWindowStart}..{page.ProbeWindowEnd}) " +
                          $"vis=[{page.ChatStack.FirstVisibleIndex}..{page.ChatStack.LastVisibleIndex}] " +
                          $"offY={page.MainScroll.ViewportOffsetY:0} stalled={stalledPans} " +
                          $"rescues={page.ChatStack.CountGapRescueMeasures} rtMeasures={page.ChatStack.CountRenderThreadCellMeasures}");
        Console.WriteLine(reachedTop
            ? "=> PASS (panned through the whole history to message 0 — no wall)"
            : "=> FAIL (scroll WALLED before reaching the oldest message)");
        Console.WriteLine("==========================================================");
    }

    static int _lastWinStart = -1;

    static bool AtOldestTop(ChatPage page)
    {
        if (page.ProbeWindowStart != 0)
            return false;
        var tree = page.ChatStack.RenderTree;
        if (tree == null) return false;
        foreach (var t in tree)
            if (t.FreezeBindingContext is ChatMessage m && m.Index == 0)
                return true;
        return false;
    }
}
