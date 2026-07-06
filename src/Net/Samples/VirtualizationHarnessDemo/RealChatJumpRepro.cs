using DrawnChatList;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;

namespace VirtualizationHarnessDemo;

/// <summary>
/// THE real repro: the actual <see cref="ChatPage"/> (real ChatCell template, real AppMessagesStack ->
/// CellsStackCached, real WindowedSource + MockChatService), mounted headless. Reproduces the reported
/// "jump to replied message -> not shown" after AppMessagesStack's base became CellsStackCached.
///
/// Drives the exact app path: ScrollToMessage(msg) -> ScrollToIndex(Center, animate:true). Samples fill
/// (non-background fraction) every frame and the visible local index range, to catch a stale/blank band.
/// </summary>
public static class RealChatJumpRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("============== REAL CHATPAGE CENTERED-JUMP ==============");

        DrawnUi.Draw.SkiaLayout.DebugBackgroundMeasureDelayMs = 60; // match Program.cs of the OpenTk head

        var page = new ChatPage();
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: ChatTheme.Bg);
        host.Canvas.Content = page.CreateCanvasContent(); // assigns MainScroll + ChatStack
        page.InitializeList();                             // wires WindowedSource + MockChatService + seeds

        // wait for first window to materialize
        for (int i = 0; i < 400 && page.ChatStack.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(4); }
        host.AdvanceFrames(12, 16);
        Report(page, host, "start (newest)");

        const int target = 189;
        page.ScrollToMessage(new ChatMessage { Index = target }); // exact app path: Center + animate:true

        double minFill = 1.0; int minFrame = -1, blankRun = 0, maxBlankRun = 0, frame = 0;
        void Sample()
        {
            double f = host.NonBackgroundFraction(ChatTheme.Bg);
            if (f < minFill) { minFill = f; minFrame = frame; }
            if (f < 0.01) { blankRun++; if (blankRun > maxBlankRun) maxBlankRun = blankRun; } else blankRun = 0;
            frame++;
        }

        for (int f = 0; f < 600; f++)
        {
            host.RenderFrame(16); Sample(); Thread.Sleep(3);
            if (f < 60 || f % 20 == 0)
                Console.WriteLine($"   f{f,3} win=[{page.ProbeWindowStart}..{page.ProbeWindowEnd}) offY={page.MainScroll.ViewportOffsetY,7:0} " +
                                  $"ordered={page.MainScroll.OrderedScrollToIndexIsSet} suppress={page.ChatStack.SuppressLoadMore} " +
                                  $"vis=[{page.ChatStack.FirstVisibleIndex}..{page.ChatStack.LastVisibleIndex}] frontier={page.ChatStack.LastMeasuredIndex}");
            if (!page.MainScroll.OrderedScrollToIndexIsSet && page.ChatStack.LastVisibleIndex >= 0 && frame > 30) break;
        }
        for (int i = 0; i < 20; i++) { host.RenderFrame(16); Sample(); }

        host.SavePng(Path.Combine(AppContext.BaseDirectory, "real-chat-jump.png"));
        Report(page, host, $"after jump->{target}");
        Console.WriteLine($"  minFill={minFill:0.000}@f{minFrame} maxBlankRun={maxBlankRun}");

        // TARGET-SPECIFIC assertion: is the cell bound to msg #189 actually IN the 0..920 viewport?
        // (Global fill only proves the screen isn't blank — neighbors could render while 189 is off-screen.)
        const float vpTop = 0, vpBot = 920;
        float tTop = float.NaN, tBot = float.NaN;
        bool foundInTree = false;
        var tree = page.ChatStack.RenderTree;
        if (tree != null)
            foreach (var t in tree)
                if (t.FreezeBindingContext is ChatMessage m && m.Index == target)
                {
                    foundInTree = true; tTop = t.HitRect.Top; tBot = t.HitRect.Bottom; break;
                }

        bool onScreen = foundInTree && tBot > vpTop && tTop < vpBot;
        bool resident = page.ProbeWindowStart <= target && target < page.ProbeWindowEnd;
        int targetLocal = page.ProbeWindowEnd - 1 - target; // inverted mapping
        Console.WriteLine($"  WINDOW=[{page.ProbeWindowStart}..{page.ProbeWindowEnd}) resident={page.ProbeResident} " +
                          $"targetResident={resident} targetLocal={targetLocal} frontier(LastMeasured)={page.ChatStack.LastMeasuredIndex} " +
                          $"contentH={page.MainScroll.ContentSize.Pixels.Height:0} vpH={vpBot:0}");
        Console.WriteLine($"  TARGET msg{target}: inRenderTree={foundInTree} hitY=[{tTop:0}..{tBot:0}] " +
                          $"onScreen(0..920)={onScreen}");

        bool ok = onScreen;
        Console.WriteLine(ok ? "=> PASS (msg189 cell is on-screen)"
                             : "=> FAIL (msg189 NOT on screen — 'not shown' reproduced)");
        Console.WriteLine("=========================================================");

        DrawnUi.Draw.SkiaLayout.DebugBackgroundMeasureDelayMs = 0;
    }

    private static void Report(ChatPage page, HeadlessCanvasHost host, string label)
    {
        double fill = host.NonBackgroundFraction(ChatTheme.Bg);
        Console.WriteLine($"{label,-18} fill={fill,6:0.000} renderTree={page.ChatStack.RenderTree?.Count ?? 0,3} " +
                          $"vis=[{page.ChatStack.FirstVisibleIndex}..{page.ChatStack.LastVisibleIndex}] " +
                          $"offY={page.MainScroll.ViewportOffsetY,8:0} ordered={page.MainScroll.OrderedScrollToIndexIsSet}");
    }
}
