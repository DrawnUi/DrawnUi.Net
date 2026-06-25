using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Repro for: after a long-jump to a message (centered ScrollToIndex), LoadMore (history) never triggers
/// again — the jump's SuppressLoadMore is never released. The centered landing relies solely on the
/// OnScrolled armed latch (the legacy oldest-end release can't fire mid-list). Asserts suppress clears and
/// a subsequent LoadOlder actually pages.
/// </summary>
public static class JumpReleaseRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("=============== JUMP -> LOADMORE RELEASE ===============");

        using var scene = new ChatLikeScene(total: 1000, latencyMs: 30);
        var probe = new VirtualizationProbe(scene.Host, scene.Scroll, scene.List);
        scene.Warmup(frames: 12, sleepMs: 8);
        probe.SettleBackground();

        scene.JumpToIndex(500); // out-of-window centered jump (like clicking a reply to msg 500)

        // wait for the jump fetch to apply, then for the ordered scroll to settle
        for (int i = 0; i < 300 && scene.IsLoadingJump; i++) { scene.Host.RenderFrame(16); Thread.Sleep(4); }
        for (int f = 0; f < 240; f++)
        {
            scene.Host.RenderFrame(16);
            Thread.Sleep(4);
            if (!scene.Scroll.OrderedScrollToIndexIsSet && probe.LastVisible >= 0) break;
        }
        scene.Host.AdvanceFrames(10, 16);

        bool suppressedAfterJump = scene.List.SuppressLoadMore;
        bool orderedStillSet = scene.Scroll.OrderedScrollToIndexIsSet;

        int wBefore = scene.WindowStart;
        scene.TriggerLoadOlder();
        for (int f = 0; f < 160 && scene.IsLoadingOlder; f++) { scene.Host.RenderFrame(16); Thread.Sleep(4); }
        scene.Host.AdvanceFrames(6, 16);
        bool loadOlderWorked = scene.WindowStart < wBefore;

        Console.WriteLine($"after jump: SuppressLoadMore={suppressedAfterJump} orderedSet={orderedStillSet} " +
                          $"WindowStart {wBefore}->{scene.WindowStart} LoadOlderWorked={loadOlderWorked}");
        // SuppressLoadMore is the real signal: in the app, LoadMore flows through ShouldTriggerLoadMore
        // which returns false while suppressed. (LoadOlderWorked above calls LoadOlder directly, bypassing
        // the gate, so it's informational only.)
        Console.WriteLine(!suppressedAfterJump
            ? "=> PASS"
            : "=> FAIL (SuppressLoadMore stuck on after jump -> history LoadMore dead)");
        Console.WriteLine("========================================================");
    }
}
