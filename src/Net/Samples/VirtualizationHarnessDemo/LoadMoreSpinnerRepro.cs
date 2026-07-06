using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Validates the LoadMore loading-state machine that drives a spinner: with an async SliceLoader,
/// IsLoadingOlder/IsLoadingNewer must go true on trigger, stay true for the fetch, then clear once the
/// batch applied (and the window advanced). Re-entrant LoadMore calls during the fetch must be ignored.
/// Pure headless — no spinner control, just the state contract the UI binds to.
/// </summary>
public static class LoadMoreSpinnerRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("=============== LOADMORE-SPINNER STATE ===============");

        using var scene = new ChatLikeScene(total: 1000, latencyMs: 60); // data-source latency = observable spinner
        var probe = new VirtualizationProbe(scene.Host, scene.Scroll, scene.List);
        scene.Warmup(frames: 12, sleepMs: 8);
        probe.SettleBackground();

        int startWindow = scene.WindowStart; // at present => window [950..1000), older available

        scene.TriggerLoadOlder();
        bool loadingSeenImmediately = scene.IsLoadingOlder;          // set synchronously, before await
        scene.TriggerLoadOlder();                                    // re-entrant: must be ignored
        scene.TriggerLoadOlder();

        // pump until the fetch completes and applies
        int frames = 0;
        for (; frames < 300 && scene.IsLoadingOlder; frames++)
        {
            scene.Host.RenderFrame(16);
            Thread.Sleep(4);
        }

        bool clearedAfter = !scene.IsLoadingOlder;
        bool windowGrew = scene.WindowStart < startWindow;
        int advanced = startWindow - scene.WindowStart;

        Console.WriteLine($"loadingSeenImmediately={loadingSeenImmediately}  clearedAfter={clearedAfter}  " +
                          $"windowGrew={windowGrew} (advanced {advanced}, expected 50)  framesToClear={frames}");

        bool ok = loadingSeenImmediately && clearedAfter && windowGrew && advanced == 50;
        Console.WriteLine(ok ? "=> PASS (state machine)" : "=> FAIL (state machine)");

        // Phase 2: gesture-driven history paging with the delayed loader still active — async paging must
        // keep advancing the window AND keep content drawn (no blank), proving the deferred apply path
        // preserves scroll continuity, not just the single-shot state machine above.
        var robot = new GestureRobot(scene.Host);
        int w0 = scene.WindowStart;
        for (int round = 0; round < 10 && scene.WindowStart > 0; round++)
        {
            robot.Pan(215, 180, 215, 560, durationMs: 90, steps: 8); // drag down = scroll into history (inverted)
            for (int f = 0; f < 70; f++) { scene.Host.RenderFrame(16); Thread.Sleep(4); } // cover the 600ms fetch
        }
        scene.Host.RenderFrame(16);

        double fill = scene.Host.NonBackgroundFraction(Color.FromArgb("#000000"));
        bool paged = scene.WindowStart < w0;
        bool notBlank = fill > 0.05;
        Console.WriteLine($"async gesture paging: paged={paged} (WindowStart {w0} -> {scene.WindowStart})  " +
                          $"fill={fill:0.000}  resident={scene.ResidentCount}  " +
                          $"{(paged && notBlank ? "PASS (continuity)" : "FAIL (continuity)")}");
        Console.WriteLine("======================================================");
    }
}
