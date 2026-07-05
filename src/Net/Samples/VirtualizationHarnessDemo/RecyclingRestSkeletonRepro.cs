using DrawnChatList;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// DEVICE-REPORTED (recycling=true): after fast scrolling, cells render as EMPTY BUBBLES (skeleton
/// shapes, correct sizes, no content) and STAY that way at full rest (~70fps = plane happily blitting).
/// Repro: fast pans into history, then rest; dump per-visible-cell state (NeedMeasure /
/// IsPreparingOffthread / MeasureClaim / cache) each rest second and save a PNG of the rested viewport.
/// PASS = at rest every visible cell is prepared (no NeedMeasure, claim free) — content, not skeletons.
/// </summary>
public static class RecyclingRestSkeletonRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= RECYCLING REST-SKELETON (device empty-bubbles) =========");

        var page = new ChatPage();
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: ChatTheme.Bg);
        host.Canvas.Content = page.CreateCanvasContent();
        page.InitializeList();

        for (int i = 0; i < 400 && (page.ProbeWindowEnd == 0 || (page.ChatStack.RenderTree?.Count ?? 0) == 0); i++)
        {
            host.RenderFrame(16);
            Thread.Sleep(4);
        }

        for (int f = 0; f < 40; f++) { host.RenderFrame(16); Thread.Sleep(3); }

        // fast pans into history (like the device swipes that produced the state)
        var robot = new GestureRobot(host);
        for (int pan = 0; pan < 6; pan++)
        {
            robot.Pan(220, 500, 220, 860, durationMs: 90, steps: 5); // fast drag = fling into history
            robot.SettleFling(page.MainScroll, 240, 16);
        }

        // REST: the device shows the bug at full rest — sample over ~4 seconds
        int badFinal = 0;
        for (int second = 0; second < 4; second++)
        {
            for (int f = 0; f < 60; f++) { host.RenderFrame(16); Thread.Sleep(3); }

            int first = page.ChatStack.FirstVisibleIndex, last = page.ChatStack.LastVisibleIndex;
            int bad = 0;
            for (int i = Math.Max(0, first); i <= last; i++)
            {
                var v = page.ChatStack.ChildrenFactory.PeekRealizedViewForIndex(i);
                bool unpreparedVisible = v == null || v.NeedMeasure || !page.ChatStack.ChildrenFactory.IsViewPrepared(i);
                if (unpreparedVisible)
                {
                    bad++;
                    Console.WriteLine($"  rest+{second + 1}s idx{i}: " + (v == null
                        ? "view=NULL"
                        : $"uid={v.Uid.ToString()[..8]} need={v.NeedMeasure} prepared={page.ChatStack.ChildrenFactory.IsViewPrepared(i)} " +
                          $"cache={(v.RenderObject != null ? "OK" : "NULL")} " +
                          $"cachePrev={(v.RenderObjectPrevious != null ? "OK" : "NULL")}"));
                }
            }

            Console.WriteLine($"  rest+{second + 1}s vis=[{first}..{last}] unprepared={bad} " +
                              $"rescues={page.ChatStack.CountGapRescueMeasures}");
            badFinal = bad;
        }

        host.SavePng(Path.Combine(AppContext.BaseDirectory, "recycling-rest.png"));
        Console.WriteLine($"  PNG: recycling-rest.png");
        Console.WriteLine($"  POOL: {page.ChatStack.ChildrenFactory.GetDebugInfo()} " +
                          $"ceiling={page.ChatStack.ChildrenFactory.PoolMaxSize}");
        Console.WriteLine(badFinal == 0
            ? "=> PASS (all visible cells prepared at rest)"
            : $"=> FAIL ({badFinal} visible cells still unprepared at rest — device empty-bubbles reproduced)");
        Console.WriteLine("==================================================================");
    }
}
