using DrawnChatList;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// SkiaCachedStack.UseDoubleBuffering=true (second background-prepared plane): user reports the adopted
/// IMAGE plane draws at a WRONG Y at plain app startup (offset ~one cell), before any scroll/trim — the
/// cleanest lab for the picture-vs-image non-equivalence. Renders the same settled startup screen twice:
/// once served by the baked IMAGE (flag on, adopt awaited) and once by the PICTURE (flag off), saves both
/// framebuffers and measures the vertical shift via row-profile comparison.
/// </summary>
public static class PlaneImageStartupRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= PLANE IMAGE STARTUP (UseDoubleBuffering=true) =========");
        var dir = @"C:\Users\taubl\AppData\Local\Temp\claude\C--Users-taubl\2c597eec-477f-4fa9-a277-76a6f3f92aa3\scratchpad";

        var profiles = new List<int[]>();

        foreach (var useDouble in new[] { false, true })
        {
            var page = new ChatPage();
            using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: ChatTheme.Bg);
            host.Canvas.Content = page.CreateCanvasContent();
            page.InitializeList();

            var stack = (SkiaCachedStack)page.ChatStack;
            stack.UseDoubleBuffering = useDouble;

            for (int i = 0; i < 400 && page.ChatStack.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(4); }
            for (int f = 0; f < 90; f++) { host.RenderFrame(16); Thread.Sleep(3); } // settle + bake + adopt

            bool servingImage = stack.ForegroundPlane != null && stack.ForegroundPlane.Picture == null &&
                                stack.ForegroundPlane.Image != null;
            Console.WriteLine($"  double={useDouble} plane={(stack.ForegroundPlane == null ? "none" : servingImage ? "IMG" : "PIC")} " +
                              $"caching={stack.IsCaching} vis=[{stack.FirstVisibleIndex}..{stack.LastVisibleIndex}]");

            var name = useDouble ? "startup-image.png" : "startup-picture.png";
            host.SavePng(System.IO.Path.Combine(dir, name));
        }

        Console.WriteLine("  saved startup-picture.png / startup-image.png -> compare externally");
        Console.WriteLine("=================================================================");
    }
}
