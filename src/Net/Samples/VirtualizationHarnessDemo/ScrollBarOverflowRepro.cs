using System.Reflection;
using DrawnChatList;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Repro for reported bug: on the real inverted chat (growing windowed content via LoadMore),
/// the SkiaScrollBar thumb progressively leaves the track bounds while scrolling toward the end.
/// Tracks thumb visual rect (arranged rect + translation) vs bar bounds every round.
/// </summary>
public static class ScrollBarOverflowRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("============== SCROLLBAR THUMB OVERFLOW ==============");

        DrawnUi.Draw.SkiaLayout.DebugBackgroundMeasureDelayMs = 60;

        var page = new ChatPage();
        using var host = new HeadlessCanvasHost(440, 920, scale: 2f, background: ChatTheme.Bg);
        host.Canvas.Content = page.CreateCanvasContent();
        page.InitializeList();

        for (int i = 0; i < 400 && page.ChatStack.LastVisibleIndex < 0; i++)
        {
            host.RenderFrame(16);
            Thread.Sleep(4);
        }

        host.AdvanceFrames(12, 16);

        var scroll = page.MainScroll;
        var bar = scroll.ScrollBar as SkiaScrollBar;
        if (bar == null)
        {
            Console.WriteLine("=> SKIP: no SkiaScrollBar attached to chat scroll");
            return;
        }

        var thumb = (SkiaShape)typeof(SkiaScrollBar)
            .GetField("_thumb", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(bar)!;

        var robot = new GestureRobot(host);
        bool overflow = false;
        float worstOverflow = 0;
        int overflowFrames = 0;
        string worstLine = "";

        // sample EVERY rendered frame — catches transient overflow during fling + content growth
        void CheckFrame(string tag)
        {
            var barRect = bar.DrawingRect;
            var thumbArranged = thumb.DrawingRect;
            var translY = (float)thumb.TranslationY * host.Canvas.RenderingScale;
            var visTop = thumbArranged.Top + translY;
            var visBot = thumbArranged.Bottom + translY;
            var over = Math.Max(barRect.Top - visTop, visBot - barRect.Bottom);
            if (over > 0.5f)
            {
                overflow = true;
                overflowFrames++;
                if (over > worstOverflow)
                {
                    worstOverflow = over;
                    worstLine =
                        $"   WORST {tag}: over={over:0.0}px bar=[{barRect.Top:0}..{barRect.Bottom:0}] " +
                        $"thumbArr=[{thumbArranged.Top:0}..{thumbArranged.Bottom:0}] hReq={thumb.HeightRequest:0} " +
                        $"translY={translY:0} vis=[{visTop:0}..{visBot:0}] prog={scroll.ScrollProgressY:0.000} " +
                        $"contentH={scroll.ContentSize.Pixels.Height:0} win=[{page.ProbeWindowStart}..{page.ProbeWindowEnd})";
                }
            }
        }

        for (int round = 0; round < 40; round++)
        {
            robot.Pan(215, 250, 215, 780, durationMs: 90, steps: 8);

            for (int f = 0; f < 35; f++)
            {
                host.RenderFrame(16);
                CheckFrame($"round{round} f{f}");
                Thread.Sleep(2);
            }

            var progress = (float)scroll.ScrollProgressY;
            var barRect = bar.DrawingRect;
            var thumbArranged = thumb.DrawingRect;
            var translY = (float)thumb.TranslationY * host.Canvas.RenderingScale;

            if (round % 4 == 0 || progress >= 0.999f)
                Console.WriteLine(
                    $"round{round,2}: win=[{page.ProbeWindowStart}..{page.ProbeWindowEnd}) contentH={scroll.ContentSize.Pixels.Height,7:0} " +
                    $"prog={progress,6:0.000} bar=[{barRect.Top:0}..{barRect.Bottom:0}] " +
                    $"vis=[{thumbArranged.Top + translY:0}..{thumbArranged.Bottom + translY:0}] hReq={thumb.HeightRequest,5:0}");

            if (progress >= 0.999f)
                break;
        }

        // overscroll bounce at the far end: keep pulling past the edge, sample every frame
        Console.WriteLine("   -- overscroll bounce at end --");
        for (int i = 0; i < 3; i++)
        {
            robot.Pan(215, 300, 215, 860, durationMs: 60, steps: 6);
            for (int f = 0; f < 45; f++)
            {
                host.RenderFrame(16);
                CheckFrame($"bounce{i} f{f}");
                Thread.Sleep(2);
            }
        }

        Console.WriteLine(overflow
            ? $"=> REPRODUCED: thumb outside track on {overflowFrames} frames, worst {worstOverflow:0.0}px\n{worstLine}"
            : "=> no overflow detected (per-frame sampling)");
        Console.WriteLine("=========================================================");

        DrawnUi.Draw.SkiaLayout.DebugBackgroundMeasureDelayMs = 0;
    }
}
