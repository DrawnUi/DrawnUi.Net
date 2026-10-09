using DrawnChatList;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// USER-REPORTED (investigation, no lib fix here): after double-buffering landed, scrolling a bit into
/// history and tapping a message hits the WRONG cell (e.g. can't open message ~313's image).
/// Suspected mechanism: async bakes correctly skip SetRenderingTree, so the gesture RenderTree stays
/// from the LAST LIVE frame, while DrawCache sets RenderTree.Offset anchored to the PLANE's recording
/// frame — two different frames => hit rects offset by the scroll delta between them.
/// A/B proof: identical deterministic pan + tap at the same screen point with UseDoubleBuffering=false
/// (sync record rebuilds the tree in the same frame it records the plane — reference) vs true.
/// The actually-tapped message is captured END-TO-END from the chat stack's ChildTapped event.
/// </summary>
public static class TapStaleTreeRepro
{
    private const float TapX = 220, TapY = 460;

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= TAP AFTER SCROLL: STALE GESTURE TREE UNDER DOUBLE BUFFERING =========");

        var (offA, tappedA, treeA) = RunOne(doubleBuffering: false);
        var (offB, tappedB, treeB) = RunOne(doubleBuffering: true);

        Console.WriteLine($"  single-plane : offY={offA:0.#} tree@P={treeA} tapped={tappedA}");
        Console.WriteLine($"  double-buffer: offY={offB:0.#} tree@P={treeB} tapped={tappedB}");

        bool sameScroll = Math.Abs(offA - offB) < 30; // deterministic robot => runs must land close
        bool ok = sameScroll && tappedA >= 0 && tappedA == tappedB;
        Console.WriteLine(ok
            ? "=> PASS (same message tapped in both modes)"
            : $"=> FAIL (sameScroll={sameScroll}: tap resolved to msg {tappedB} with double buffering vs {tappedA} single-plane)");
        Console.WriteLine("===============================================================================");
    }

    /// <returns>settled offY, end-to-end tapped message index (-1 = none), tree-resolved index at P</returns>
    private static (float offY, int tapped, string treeAtP) RunOne(bool doubleBuffering)
    {
        var page = new ChatPage();
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: ChatTheme.Bg);
        host.Canvas.Content = page.CreateCanvasContent();
        CachedStackAccess.SetAutoDoubleBuffering(page.ChatStack, doubleBuffering);
        page.InitializeList();

        for (int i = 0; i < 400 && page.ChatStack.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(4); }
        for (int f = 0; f < 60; f++) { host.RenderFrame(16); Thread.Sleep(3); }

        var robot = new GestureRobot(host);

        // "scroll a bit up" (visually into history) = drag DOWN on the inverted chat
        robot.Pan(215, 250, 215, 820, durationMs: 90, steps: 8);
        robot.SettleFling(page.MainScroll);
        for (int f = 0; f < 40; f++) { host.RenderFrame(16); Thread.Sleep(3); } // steady blit/bake state

        var offY = page.MainScroll.ViewportOffsetY;

        // what the gesture pipeline WOULD resolve at P right now (tree entry containing P - tree.Offset)
        string treeAtP = "none";
        var tree = page.ChatStack.RenderTree;
        if (tree != null)
        {
            var adj = tree.AdjustOffset(new SkiaSharp.SKPoint(TapX, TapY));
            foreach (var t in tree)
                if (t.HitRect.Contains(adj.X, adj.Y))
                {
                    treeAtP = t.FreezeBindingContext is ChatMessage m ? $"msg{m.Index}" : "?";
                    break;
                }
        }

        // end-to-end tap: the recipient is the cell the chat stack reports as tapped (ChildTapped is raised
        // for the child that took the Tapped gesture). Not the cell's own Debug.WriteLine: Release builds
        // compile it out, which made this repro report "tapped -1" in both modes.
        int tapped = -1;
        void onChildTapped(object s, ControlTappedEventArgs e)
        {
            if (e.Control is SkiaControl child && child.BindingContext is ChatMessage m)
                tapped = m.Index;
        }

        page.ChatStack.ChildTapped += onChildTapped;
        try
        {
            robot.Tap(TapX, TapY);
            for (int f = 0; f < 10; f++) { host.RenderFrame(16); Thread.Sleep(3); }
        }
        finally
        {
            page.ChatStack.ChildTapped -= onChildTapped;
        }

        return (offY, tapped, treeAtP);
    }
}
