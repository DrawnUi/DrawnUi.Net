using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Repro for: after switching the chat stack base from CellsStack to CellsStackCached, a centered
/// long-jump to an out-of-window message ("click reply to msg 500") lands structurally but the message
/// is NOT shown — the cached Operations band is stale at the resting position.
///
/// Runs the IDENTICAL centered-jump sequence twice: once on the plain (no-cache) stack and once on the
/// cached-plane stack (CachedPeekStack == app's CellsStackCached active config). Asserts BOTH: the target
/// row is structurally present+positioned AND the surface actually renders content (fill > 0) at rest.
/// A cached-path regression shows as structure-OK but fill≈0 (blank blit) on the cached run only.
/// </summary>
public static class CachedJumpRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("================ CACHED CENTERED-JUMP ================");
        // The app's ScrollToMessage uses animate:TRUE — exercise both timings.
        bool plainI = RunOnce("PLAIN  instant", cachedPlanes: false, animate: false);
        bool cachedI = RunOnce("CACHED instant", cachedPlanes: true, animate: false);
        bool plainA = RunOnce("PLAIN  animated", cachedPlanes: false, animate: true);
        bool cachedA = RunOnce("CACHED animated (app path)", cachedPlanes: true, animate: true);
        Console.WriteLine("------------------------------------------------------");
        bool all = plainI && cachedI && plainA && cachedA;
        Console.WriteLine(all
            ? "=> PASS (all render the centered target at rest)"
            : $"=> FAIL  plainI={F(plainI)} cachedI={F(cachedI)} plainA={F(plainA)} cachedA={F(cachedA)}");
        Console.WriteLine("======================================================");
    }

    private static string F(bool ok) => ok ? "ok" : "BLANK";

    private static bool RunOnce(string title, bool cachedPlanes, bool animate)
    {
        Console.WriteLine();
        Console.WriteLine($"--- {title} ---");

        // Match the real OpenTk app (Program.cs): lag background measurement so recycled cells re-measure
        // LATE — the condition that can let the cached band record a stale window before measurement lands.
        DrawnUi.Draw.SkiaLayout.DebugBackgroundMeasureDelayMs = 60;
        using var scene = new ChatLikeScene(total: 1000, latencyMs: 30, cachedPlanes: cachedPlanes);
        var probe = new VirtualizationProbe(scene.Host, scene.Scroll, scene.List);
        scene.Warmup(frames: 12, sleepMs: 8);
        probe.SettleBackground();

        var bg = Color.FromArgb("#000000");

        // WARM the measurement memo for the target window: jump there once, then back to newest. The real
        // "jump to a reply" is usually a re-visit — the window was already measured, so the second jump is
        // served synchronously from the memo and fires NO background-measurement event. That's the path the
        // cached band's early "_contentChanged=false" (set before CreateCache) can strand as stale.
        scene.JumpToIndex(500, animate);
        for (int i = 0; i < 300 && scene.IsLoadingJump; i++) { scene.Host.RenderFrame(16); Thread.Sleep(4); }
        for (int f = 0; f < 200 && scene.Scroll.OrderedScrollToIndexIsSet; f++) { scene.Host.RenderFrame(16); Thread.Sleep(4); }
        scene.ReleaseSuppress();
        scene.JumpToNewest();
        for (int i = 0; i < 300 && scene.IsLoadingJump; i++) { scene.Host.RenderFrame(16); Thread.Sleep(4); }
        for (int f = 0; f < 200 && scene.Scroll.OrderedScrollToIndexIsSet; f++) { scene.Host.RenderFrame(16); Thread.Sleep(4); }
        scene.ReleaseSuppress();
        scene.Host.AdvanceFrames(8, 16);

        scene.JumpToIndex(500, animate); // RE-VISIT centered jump (reply-to-message) — memo-served

        // Sample fill EVERY frame from the trigger through settle. A transient blank (cached band stale for
        // a stretch of frames) is the real-app symptom: at 60fps a multi-frame dip reads as "not shown" and
        // it never recovers until the user interacts again (no further invalidation).
        double minFill = 1.0; int minFrame = -1; int blankRun = 0, maxBlankRun = 0; int frame = 0;
        void Sample()
        {
            double f = scene.Host.NonBackgroundFraction(bg);
            if (f < minFill) { minFill = f; minFrame = frame; }
            if (f < 0.01) { blankRun++; if (blankRun > maxBlankRun) maxBlankRun = blankRun; } else blankRun = 0;
            frame++;
        }

        for (int i = 0; i < 300 && scene.IsLoadingJump; i++) { scene.Host.RenderFrame(16); Sample(); Thread.Sleep(4); }
        for (int f = 0; f < 240; f++)
        {
            scene.Host.RenderFrame(16); Sample();
            Thread.Sleep(4);
            if (!scene.Scroll.OrderedScrollToIndexIsSet && probe.LastVisible >= 0) break;
        }
        for (int i = 0; i < 12; i++) { scene.Host.RenderFrame(16); Sample(); }

        double fill = scene.Host.NonBackgroundFraction(bg);
        bool targetResident = scene.WindowStart <= 500 && 500 < scene.WindowEnd;

        // TARGET-SPECIFIC: is the cell for global row 500 actually on screen? (Global fill renders neighbors
        // too, so it can't tell whether the JUMP TARGET landed in view — that was a false-positive earlier.)
        var (found, tt, tb) = scene.TargetCell(500);
        bool onScreen = found && tb > 0 && tt < scene.ViewportHeight;

        Console.WriteLine(
            $"  fill={fill,6:0.000} minFill={minFill,6:0.000} maxBlankRun={maxBlankRun} " +
            $"resident={scene.ResidentCount,3} window=[{scene.WindowStart}..{scene.WindowEnd}) targetResident={targetResident} " +
            $"offY={probe.OffsetY:0} | TARGET500 found={found} y=[{tt:0}..{tb:0}] onScreen={onScreen}" +
            (onScreen ? "" : "  <<< NOT SHOWN"));

        DrawnUi.Draw.SkiaLayout.DebugBackgroundMeasureDelayMs = 0;
        return targetResident && onScreen;
    }
}
