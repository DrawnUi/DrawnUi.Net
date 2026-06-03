using DrawnUi.Testing;

// Headless reconstruction of LoadMoreRepro (static 1000 items, Managed planes).
// Drives scrolling and reports the measurement frontier + whether item measurement happens on the
// render thread (foreground = non-fluid) or on background threadpool threads (fluid).

using var scene = new VirtualizationScene(itemsCount: 1000);
scene.Warmup();

var (wfg, wbg) = scene.DrainMeasures();
Console.WriteLine($"warmup: frontier={scene.List.LastMeasuredIndex} MEASURES fg(render)={wfg} bg(pool)={wbg}");

var probe = new VirtualizationProbe(scene.Host, scene.Scroll, scene.List);
var robot = new GestureRobot(scene.Host);

int totalFg = 0, totalBg = 0;
for (int round = 0; round < 30; round++)
{
    robot.Pan(215, 520, 215, 180, durationMs: 90, steps: 8);
    probe.SettleBackground();
    var (fg, bg) = scene.DrainMeasures();
    totalFg += fg;
    totalBg += bg;

    if (round % 5 == 0 || probe.Frontier >= probe.ItemsCount - 1)
        Console.WriteLine(
            $"round{round,2}: frontier={probe.Frontier,4}/{probe.ItemsCount} offY={probe.OffsetY,7:0} " +
            $"contentH={probe.ContentHeight,7:0}  MEASURES fg(render)={fg,3} bg(pool)={bg,3}");

    if (probe.Frontier >= probe.ItemsCount - 1)
        break;
}

Console.WriteLine();
Console.WriteLine($"FINAL frontier={probe.Frontier}/{probe.ItemsCount}");
Console.WriteLine($"TOTAL scroll-phase MEASURES: fg(render-thread)={totalFg}  bg(threadpool)={totalBg}");
Console.WriteLine(totalFg == 0
    ? "=> GOAL MET: ALL scroll-phase measurement happened in BACKGROUND (zero on render thread)."
    : $"=> NOT MET: {totalFg} cell measurements ran on the render thread during scroll (jank risk).");

scene.Host.SavePng(Path.Combine(AppContext.BaseDirectory, "virt-demo-final.png"));
Console.WriteLine("done.");
