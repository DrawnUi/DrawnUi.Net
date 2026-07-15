using System.Reflection;
using DrawnChatList;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// USER-REPORTED: launch the app and scroll into history IMMEDIATELY (before the initial background
/// measurement finishes) -> the frontier catch-up spinner appears and NEVER disappears.
/// Suspected: UpdateFrontierSpinner is only called from OnChatScrolled — parked at the edge there are
/// no scroll events, so when the measurement frontier catches up nothing re-evaluates the spinner.
/// The repro also discriminates the worse variant: the frontier itself stalling at rest.
/// </summary>
public static class FrontierSpinnerStuckRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= FRONTIER SPINNER STUCK (scroll before initial measurement) =========");

        var page = new ChatPage();
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: ChatTheme.Bg);
        host.Canvas.Content = page.CreateCanvasContent();
        page.InitializeList();

        // wait ONLY until first cells appear — deliberately no settle (user scrolls immediately)
        for (int i = 0; i < 400 && page.ChatStack.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(4); }

        var robot = new GestureRobot(host);

        var fSpinner = typeof(ChatPage).GetField("_frontierSpinner", BindingFlags.NonPublic | BindingFlags.Instance);
        var fSpinnerView = typeof(ChatPage).GetField("Spinner", BindingFlags.NonPublic | BindingFlags.Instance);

        bool SpinnerOn() => (bool)fSpinner.GetValue(page)
                            || ((fSpinnerView.GetValue(page) as SkiaControl)?.IsVisible ?? false);

        // The harness measures too fast to catch the STARTUP lag the user hits on a device — so force
        // the same state via LoadOlder: pan into history until the frontier spinner actually ARMS
        // (fresh batch committed, measurement frontier behind), then PARK instantly.
        bool armed = false;
        for (int p = 0; p < 30 && !armed; p++)
        {
            robot.Pan(215, 250, 215, 820, durationMs: 90, steps: 8);
            for (int f = 0; f < 3; f++)
            {
                host.RenderFrame(16);
                if (SpinnerOn()) { armed = true; break; }
            }
        }

        Console.WriteLine($"  spinner armed by panning: {armed}");
        if (!armed)
        {
            Console.WriteLine("=> SKIP (could not arm the frontier spinner in harness timing)");
            Console.WriteLine("==============================================================================");
            return;
        }

        // park at the edge: NO gestures, just frames. Sample frontier + spinner.
        int count = page.ChatStack.ItemsSource?.Count ?? 0;
        int lastLogged = -1;
        bool frontierDone = false;
        int framesAfterDone = 0;
        bool spinnerAfterDone = false;

        for (int f = 0; f < 900; f++) // ~15s simulated
        {
            host.RenderFrame(16);
            Thread.Sleep(3);

            count = page.ChatStack.ItemsSource?.Count ?? 0;
            int measured = page.ChatStack.LastMeasuredIndex;
            if (measured != lastLogged && (f % 30 == 0 || measured >= count - 1))
            {
                Console.WriteLine($"  f{f,3}: measured={measured}/{count - 1} spinner={SpinnerOn()}");
                lastLogged = measured;
            }

            if (!frontierDone && count > 0 && measured >= count - 1)
                frontierDone = true;

            if (frontierDone)
            {
                framesAfterDone++;
                spinnerAfterDone = SpinnerOn();
                if (framesAfterDone >= 120) // 2s parked AFTER the frontier caught up
                    break;
            }
        }

        Console.WriteLine($"  frontierDone={frontierDone} spinnerAfterDone(2s parked)={spinnerAfterDone} " +
                          $"orderedSet={page.MainScroll.OrderedScrollToIndexIsSet} " + // TEMP PROBE
                          $"loadingOlder={typeof(ChatPage).GetField("_isLoadingOlder", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(page)} " +
                          $"loadingJump={typeof(ChatPage).GetField("_isLoadingJump", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(page)} " +
                          $"frontierFlag={fSpinner.GetValue(page)}");
        if (!frontierDone)
            Console.WriteLine("=> FAIL (frontier NEVER caught up at rest — measurement stall, worse than the spinner)");
        else if (spinnerAfterDone)
            Console.WriteLine("=> FAIL (frontier done but spinner still visible — stuck-spinner reproduced)");
        else
            Console.WriteLine("=> PASS (spinner cleared after the frontier caught up)");
        Console.WriteLine("==============================================================================");
    }
}
