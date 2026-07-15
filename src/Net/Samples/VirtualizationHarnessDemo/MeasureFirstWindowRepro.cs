using System.Collections.ObjectModel;
using System.Windows.Input;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Repro for the device crash: MeasureFirst column + 2000-item ItemsSource -> built-in ItemsSourceWindow
/// engages -> scrolling triggers internal slides (uniform arithmetic add) -> NRE on device (managed stack
/// stripped there). Drives the same flow headlessly to get the real stack.
/// </summary>
public static class MeasureFirstWindowRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= MEASUREFIRST WINDOW SLIDE REPRO =========");
        System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());

        var items = new ObservableCollection<string>();
        for (int i = 0; i < 2000; i++)
        {
            items.Add($"Item {i}");
        }

        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: Colors.Black);

        // user LoadMore: must fire ONLY at the true source edge (window slides silently before that)
        int loadMoreFired = 0;
        var loadMore = new Command(() =>
        {
            loadMoreFired++;
            for (int i = 0; i < 20; i++)
            {
                items.Add($"Item {items.Count}");
            }
        });

        SkiaLayout stack = null;
        var scroll = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            LoadMoreCommand = loadMore,
            LoadMoreOffset = 200,
            Content = new SkiaLayout
                {
                    Type = LayoutType.Column,
                    HorizontalOptions = LayoutOptions.Fill,
                    MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
                    Virtualisation = VirtualisationType.Enabled,
                    RecyclingTemplate = RecyclingTemplate.Enabled,
                    Spacing = 0,
                    ItemTemplate = new DataTemplate(() => new SkiaLayout
                    {
                        UseCache = SkiaCacheType.Image,
                        HorizontalOptions = LayoutOptions.Fill,
                        HeightRequest = 80,
                        Children = new List<SkiaControl>
                        {
                            new SkiaLabel { Text = "cell", FontSize = 16 }
                        }
                    }),
                    ItemsSource = items,
                }
                .Assign(out stack),
        };

        host.Canvas.Content = scroll;
        host.AdvanceFrames(10, 16);

        Console.WriteLine(stack.ItemsWindow == null
            ? "  window NOT engaged!"
            : $"  window engaged [{stack.ItemsWindow.WindowStart}..{stack.ItemsWindow.WindowEnd}) of {items.Count}");

        var robot = new GestureRobot(host);

        try
        {
            for (int round = 0; round < 120; round++)
            {
                robot.Pan(215, 700, 215, 200, durationMs: 90, steps: 8); // scroll DOWN toward the tail
                for (int f = 0; f < 20; f++)
                {
                    host.RenderFrame(16);
                    Thread.Sleep(2);
                }

                var w = stack.ItemsWindow;
                if (round % 2 == 0)
                {
                    var tree = stack.RenderTree;
                    var minIdx = tree?.Count > 0 ? tree.Min(t => t.FreezeIndex) : -999;
                    var maxIdx = tree?.Count > 0 ? tree.Max(t => t.FreezeIndex) : -999;
                    Console.WriteLine(
                        $"  round {round}: win [{w?.WindowStart}..{w?.WindowEnd}) vis {stack.FirstVisibleIndex}..{stack.LastVisibleIndex} lastM {stack.LastMeasuredIndex} " +
                        $"offY {scroll.ViewportOffsetY:0} tree {minIdx}..{maxIdx}");
                }
                if (stack.LastVisibleIndex < 0 && round > 2)
                {
                    Console.WriteLine($"  WEDGED at round {round}");
                    DumpStructure(stack);
                    break;
                }

                if (w != null && w.WindowEnd >= 400)
                {
                    Console.WriteLine(
                        $"  reached WindowEnd={w.WindowEnd}, win [{w.WindowStart}..{w.WindowEnd}) res={w.Items.Count} — forward slides + trims worked");
                    break;
                }
            }

            // phase 2: scroll back UP into trimmed space — backward slides (head inserts) must refill
            for (int round = 0; round < 200; round++)
            {
                robot.Pan(215, 200, 215, 700, durationMs: 90, steps: 8); // scroll UP toward the head
                for (int f = 0; f < 20; f++)
                {
                    host.RenderFrame(16);
                    Thread.Sleep(2);
                }

                var w = stack.ItemsWindow;
                if (round % 10 == 0)
                {
                    Console.WriteLine(
                        $"  up-round {round}: win [{w?.WindowStart}..{w?.WindowEnd}) res={w?.Items.Count} vis {stack.FirstVisibleIndex}..{stack.LastVisibleIndex}");
                }

                if (w != null && w.WindowStart == 0 && stack.FirstVisibleIndex <= 1)
                {
                    Console.WriteLine($"  back at head, win [{w.WindowStart}..{w.WindowEnd}) — backward slides worked");
                    break;
                }
            }

            // phase 3: GLOBAL ScrollToIndex over the window — non-resident targets must rebase and land
            foreach (var target in new[] { 700, 25, 1950 })
            {
                scroll.ScrollToIndex(target, animate: false);
                bool landed = false;
                for (int f = 0; f < 400; f++)
                {
                    host.RenderFrame(16);
                    Thread.Sleep(2);
                    if (f < 6 || f % 50 == 49)
                        Console.WriteLine(
                            $"    jump {target} f{f}: offY {scroll.ViewportOffsetY:0} ordered={scroll.OrderedScrollToIndexIsSet} vis {stack.FirstVisibleIndex}..{stack.LastVisibleIndex} lastM {stack.LastMeasuredIndex} contentH {stack.MeasuredSize.Pixels.Height:0} win [{stack.ItemsWindow?.WindowStart}..{stack.ItemsWindow?.WindowEnd})");
                    if (stack.FirstVisibleIndex <= target && target <= stack.LastVisibleIndex)
                    {
                        landed = true;
                        break;
                    }
                }

                var w2 = stack.ItemsWindow;
                Console.WriteLine(
                    $"  JUMP {target}: win [{w2?.WindowStart}..{w2?.WindowEnd}) vis {stack.FirstVisibleIndex}..{stack.LastVisibleIndex} " +
                    (landed ? "=> PASS" : "=> FAIL (never landed)"));
            }

            // phase 4: USER LoadMore only at the TRUE source edge. Jump near the end, scroll into it.
            Console.WriteLine($"  loadMore fired during phases 1-3: {loadMoreFired} (expected 0)");
            scroll.ScrollToIndex(1999, animate: false);
            for (int f = 0; f < 300 && loadMoreFired == 0; f++)
            {
                host.RenderFrame(16);
                Thread.Sleep(2);
                if (f % 8 == 0)
                    robot.Pan(215, 600, 215, 300, durationMs: 60, steps: 4); // keep pressing into the edge
            }

            for (int f = 0; f < 100; f++) { host.RenderFrame(16); Thread.Sleep(2); } // let append integrate

            var wEdge = stack.ItemsWindow;
            bool pickedUp = false;
            for (int f = 0; f < 300; f++)
            {
                host.RenderFrame(16);
                Thread.Sleep(2);
                if (f % 8 == 0) robot.Pan(215, 600, 215, 300, durationMs: 60, steps: 4);
                if (stack.LastVisibleIndex >= 2005) { pickedUp = true; break; }
            }

            Console.WriteLine(
                $"  EDGE LOADMORE: fired={loadMoreFired} source={items.Count} win [{wEdge?.WindowStart}..{wEdge?.WindowEnd}) vis {stack.FirstVisibleIndex}..{stack.LastVisibleIndex} " +
                (loadMoreFired >= 1 && pickedUp ? "=> PASS (fired at edge, window picked up appended items)" : "=> FAIL"));

            Console.WriteLine("  RESULT: no crash");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  CRASH: {ex}");
        }

        Console.WriteLine("=================================================");
    }

    static void DumpStructure(SkiaLayout stack)
    {
        var prop = typeof(SkiaLayout).GetProperty("StackStructure",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        var structure = prop?.GetValue(stack);
        if (structure == null)
        {
            Console.WriteLine("  structure NULL");
            return;
        }

        var getChildren = structure.GetType().GetMethod("GetChildren");
        var cells = (getChildren?.Invoke(structure, null) as IEnumerable<ControlInStack>)?.ToList();
        if (cells == null)
        {
            Console.WriteLine("  no cells");
            return;
        }

        Console.WriteLine($"  STRUCTURE {cells.Count} cells:");
        foreach (var c in cells.OrderBy(c => c.ControlIndex).Take(8))
        {
            Console.WriteLine($"    idx={c.ControlIndex} top={c.Destination.Top:0} bottom={c.Destination.Bottom:0} measured={c.WasMeasured}");
        }
        var lastCells = cells.OrderBy(c => c.ControlIndex).Skip(Math.Max(0, cells.Count - 3));
        foreach (var c in lastCells)
        {
            Console.WriteLine($"    idx={c.ControlIndex} top={c.Destination.Top:0} bottom={c.Destination.Bottom:0}");
        }
    }
}
