using DrawnUi.Draw;
using DrawnUi.Testing;
using Color = DrawnUi.Color;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Reproduces the reported "blank screen after a second jump-to-newest" bug headlessly, on the real
/// ChatPage mechanics (inverted + windowed + bidirectional LoadMore + variable heights + MeasureVisible),
/// driving the jump BUTTONS (ReplaceRange rebase + ordered ScrollToIndex) exactly as the app does.
///
/// Runs the same sequence twice — once with the item-keyed measurement memo ON (capacity 1000) and once
/// OFF (capacity 0) — so we can tell whether the memo caused the regression or it pre-exists.
/// "Blank" = NonBackgroundFraction ~ 0 on a scene that should be full of message bubbles.
/// </summary>
public static class BlankJumpRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("==================== BLANK-JUMP REPRO ====================");
        RunOnce("MEMO ON  (capacity=1000)", memoCapacity: 1000);
        RunOnce("MEMO OFF (capacity=0)", memoCapacity: 0);
        Console.WriteLine("==========================================================");
    }

    private static void RunOnce(string title, int memoCapacity)
    {
        Console.WriteLine();
        Console.WriteLine($"--- {title} ---");

        using var scene = new ChatLikeScene(total: 1000, measurementCacheCapacity: memoCapacity);
        var probe = new VirtualizationProbe(scene.Host, scene.Scroll, scene.List);

        scene.Warmup(frames: 12, sleepMs: 8);
        probe.SettleBackground();
        Report(scene, probe, "start (newest)");

        // Reported sequence: oldest -> newest -> oldest -> newest. The blank surfaced on a re-visit of a
        // previously-measured window (background measurement served entirely from the memo). Alternate a few
        // cycles so any blanking step is caught.
        for (int cycle = 1; cycle <= 3; cycle++)
        {
            scene.JumpToOldest();
            Settle(scene, probe);
            scene.ReleaseSuppress();
            Report(scene, probe, $"oldest #{cycle}");

            scene.JumpToNewest();
            Settle(scene, probe);
            scene.ReleaseSuppress();
            Report(scene, probe, $"newest #{cycle}");
        }
    }

    private static void Settle(ChatLikeScene scene, VirtualizationProbe probe)
    {
        probe.SettleBackground();
        scene.Host.AdvanceFrames(12, 16); // let the ordered scroll + draw catch up
    }

    private static void Report(ChatLikeScene scene, VirtualizationProbe probe, string label)
    {
        scene.Host.RenderFrame(16);
        double fill = scene.Host.NonBackgroundFraction(Color.FromArgb("#000000"));
        string flag = fill < 0.01 ? "  <<< BLANK" : "";
        Console.WriteLine(
            $"{label,-14} fill={fill,6:0.000} resident={scene.ResidentCount,3} " +
            $"window=[{scene.WindowStart,4}..{scene.WindowEnd,4}) frontier={probe.Frontier,4} " +
            $"vis=[{probe.FirstVisible,4}..{probe.LastVisible,4}] offY={probe.OffsetY,8:0} " +
            $"contentH={probe.ContentHeight,8:0}{flag}");

        if (probe.ContentHeight < 3000 || fill < 0.01)
            Console.WriteLine("    ROWS: " + scene.DumpRows(54));
    }
}
