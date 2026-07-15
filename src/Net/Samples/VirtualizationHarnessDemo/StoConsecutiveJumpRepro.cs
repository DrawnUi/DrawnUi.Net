using DrawnChatList;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Repro for the reported "ScrollToOldest jumps but doesn't scroll to the top on consecutive jumps" bug.
/// Real ChatPage, real WindowedSource. Sequence: settle at newest -> jump to oldest (cold) ->
/// jump to newest -> jump to oldest AGAIN (the failing case) -> fling then jump mid-fling.
/// PASS criteria per jump: window rebased to [0..batch), msg Index 0 cell on-screen, no runaway
/// LoadNewer growth while landing.
/// </summary>
public static class StoConsecutiveJumpRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("============== STO CONSECUTIVE JUMP ==============");

        // surface the library's Debug.WriteLine diagnostics (measurement start/cancel/skip reasons)
        var listener = new System.Diagnostics.ConsoleTraceListener();
        System.Diagnostics.Trace.Listeners.Add(listener);

        SkiaLayout.DebugBackgroundMeasureDelayMs = 60; // match the OpenTk head: simulate slow measuring

        var page = new ChatPage();
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: ChatTheme.Bg);
        host.Canvas.Content = page.CreateCanvasContent();
        page.InitializeList();

        for (int i = 0; i < 400 && page.ChatStack.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(4); }
        host.AdvanceFrames(12, 16);
        Console.WriteLine($"start: win=[{page.ProbeWindowStart}..{page.ProbeWindowEnd}) vis=[{page.ChatStack.FirstVisibleIndex}..{page.ChatStack.LastVisibleIndex}]");

        bool ok1 = Jump(page, host, "JUMP1 cold");

        page.ProbeScrollToNewest(true);
        Settle(page, host, "back to newest");

        bool ok2 = Jump(page, host, "JUMP2 consecutive");

        page.ProbeScrollToNewest(true);
        Settle(page, host, "back to newest");

        // fling into history, then jump mid-fling (the original repro)
        var robot = new GestureRobot(host);
        robot.Pan(215, 250, 215, 820, durationMs: 120, steps: 10); // inverted list: drag down = into history
        for (int f = 0; f < 12; f++) { host.RenderFrame(16); Thread.Sleep(3); } // fling running
        bool ok3 = Jump(page, host, "JUMP3 mid-fling");

        SkiaLayout.DebugBackgroundMeasureDelayMs = 0;
        System.Diagnostics.Trace.Listeners.Remove(listener);

        Console.WriteLine($"=> {(ok1 ? "PASS" : "FAIL")} cold | {(ok2 ? "PASS" : "FAIL")} consecutive | {(ok3 ? "PASS" : "FAIL")} mid-fling");
        Console.WriteLine(ok1 && ok2 && ok3
            ? "=> PASS (all jumps landed at the oldest message)"
            : "=> FAIL (a jump landed short — bug reproduced)");
        Console.WriteLine("==================================================");
    }

    private static bool Jump(ChatPage page, HeadlessCanvasHost host, string label)
    {
        page.ProbeScrollToOldest(true);

        // MIGRATED semantics: ScrollToOldest first FETCHES all remaining history, then issues the
        // ordered jump. Wait for the ordered scroll to actually START before waiting for it to
        // settle, else the settle detector can declare "stable" on the pre-jump idle frames.
        for (int f = 0; f < 600 && !page.MainScroll.OrderedScrollToIndexIsSet; f++)
        {
            host.RenderFrame(16);
            Thread.Sleep(3);
        }

        var frames = Settle(page, host, label);

        int winEnd = page.ProbeWindowEnd;
        // newest-first list: OLDEST lives at the TAIL — arrived = window covers the list end
        bool rebased = winEnd >= page.ProbeListCount;

        // is the oldest message's cell actually inside the viewport?
        bool onScreen = false;
        float top = float.NaN, bottom = float.NaN;
        var tree = page.ChatStack.RenderTree;
        if (tree != null)
            foreach (var t in tree)
                if (t.FreezeBindingContext is ChatMessage m && m.Index == 0)
                {
                    top = t.HitRect.Top; bottom = t.HitRect.Bottom;
                    onScreen = bottom > 0 && top < 920;
                    break;
                }

        bool ok = rebased && onScreen;
        Console.WriteLine($"{label,-18} settled in {frames,3}f win=[{page.ProbeWindowStart}..{winEnd}) " +
                          $"offY={page.MainScroll.ViewportOffsetY,7:0} msg0 y=[{top:0}..{bottom:0}] onScreen={onScreen} => {(ok ? "PASS" : "FAIL")}");
        return ok;
    }

    private static int Settle(ChatPage page, HeadlessCanvasHost host, string label)
    {
        float lastOff = float.NaN;
        int stable = 0, frame = 0;
        for (; frame < 900; frame++)
        {
            host.RenderFrame(16);
            Thread.Sleep(3);

            var off = page.MainScroll.ViewportOffsetY;
            bool ordered = page.MainScroll.OrderedScrollToIndexIsSet;
            if (!ordered && Math.Abs(off - lastOff) < 0.5f)
            {
                if (++stable >= 15) break;
            }
            else
            {
                stable = 0;
            }
            lastOff = off;
        }

        if (frame >= 900)
            Console.WriteLine($"   [{label}] WARNING: did not settle in 900 frames (ordered={page.MainScroll.OrderedScrollToIndexIsSet})");
        return frame;
    }
}
