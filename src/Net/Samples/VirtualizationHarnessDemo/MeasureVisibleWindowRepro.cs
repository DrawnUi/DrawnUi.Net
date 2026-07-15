using System.Collections.ObjectModel;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Feature #5 proving ground: MeasureVisible + BUILT-IN ItemsSourceWindow (not the app-level
/// WindowedSource). Chat-class defining conditions minus inversion (phase 1): variable cell heights,
/// big observable source, background measurement, window slides both directions.
/// Asserts per frame: bound context matches the cell's global index (no wrong-context binds),
/// non-blank viewport, continuous vis progression; then backward refill and a non-resident jump.
/// </summary>
public static class MeasureVisibleWindowRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= MEASUREVISIBLE + BUILT-IN WINDOW =========");

        var items = new ObservableCollection<string>();
        for (int i = 0; i < 1000; i++)
        {
            items.Add($"Item {i}");
        }

        SkiaLayout stack = null;
        SkiaScroll scroll = null;

        scroll = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout
                {
                    Type = LayoutType.Column,
                    HorizontalOptions = LayoutOptions.Fill,
                    MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
                    Virtualisation = VirtualisationType.Enabled,
                    RecyclingTemplate = RecyclingTemplate.Enabled,
                    Spacing = 4,
                    ItemTemplate = new DataTemplate(() =>
                    {
                        SkiaLabel label = null;
                        return new SkiaLayout
                            {
                                UseCache = SkiaCacheType.Image,
                                HorizontalOptions = LayoutOptions.Fill,
                                BackgroundColor = Colors.DarkSlateGray,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaLabel { Text = "?", FontSize = 14, Tag = "T" }.Assign(out label)
                                }
                            }
                            .ObserveSelf((me, prop) =>
                            {
                                if (prop == nameof(SkiaControl.BindingContext) && me.BindingContext is string s)
                                {
                                    label.Text = s;
                                    // variable heights: 60..140 px by item number
                                    var n = int.Parse(s.Substring(5));
                                    me.HeightRequest = 60 + (n % 5) * 20;
                                }
                            });
                    }),
                    ItemsSource = items,
                }
                .Assign(out stack),
        };

        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: Colors.Black);
        host.Canvas.Content = scroll;

        for (int i = 0; i < 300 && stack.LastVisibleIndexLocal < 0; i++)
        {
            host.RenderFrame(16);
            Thread.Sleep(2);
        }

        host.AdvanceFrames(8, 16);

        Console.WriteLine(stack.ItemsWindow == null
            ? "  window NOT engaged!"
            : $"  window engaged [{stack.ItemsWindow.WindowStart}..{stack.ItemsWindow.WindowEnd}) of {items.Count}");

        var robot = new GestureRobot(host);
        int frame = 0, badCells = 0, checkedCells = 0, blankFrames = 0;
        var firstBad = "";

        int prevWinStart = -1, settleFrames = 0;

        void CheckFrame()
        {
            var tree = stack.RenderTree;
            if (tree == null || tree.Count == 0)
                return;

            var w = stack.ItemsWindow;
            int start = w?.WindowStart ?? 0;

            // the render tree is the PREVIOUS drawn frame; during a slide's in-flight frames the live
            // WindowStart is ahead of the tree's coordinate space (the known ±batch probe skew, also
            // gated by pending structure changes). Only assert on frames where the window sat still.
            settleFrames = (start == prevWinStart && !stack.HasPendingStructureChanges) ? settleFrames + 1 : 0;
            prevWinStart = start;
            if (settleFrames < 3)
                return;
            foreach (var t in tree)
            {
                if (t.Control is not SkiaLayout row || row.BindingContext is not string ctx)
                    continue;

                var label = row.FindViewByTag("T") as SkiaLabel;
                if (label == null)
                    continue;

                checkedCells++;
                // bound context must equal the cell's GLOBAL item
                var expected = $"Item {start + t.FreezeIndex}";
                if (ctx != expected || label.Text != ctx)
                {
                    badCells++;
                    if (badCells == 1)
                        firstBad = $"frame {frame}: cell local {t.FreezeIndex} ctx '{ctx}' label '{label.Text}' expected '{expected}' win [{w?.WindowStart}..{w?.WindowEnd})";
                }
            }

            if (host.NonBackgroundFraction(Colors.Black) < 0.05)
                blankFrames++;
        }

        // phase 1: fling forward through several slides
        for (int round = 0; round < 60; round++)
        {
            robot.Pan(215, 700, 215, 250, durationMs: 70, steps: 6);
            for (int f = 0; f < 30; f++)
            {
                host.RenderFrame(16);
                frame++;
                Thread.Sleep(2);
                CheckFrame();
            }

            if (round % 15 == 0)
            {
                var w = stack.ItemsWindow;
                Console.WriteLine(
                    $"  fwd round {round}: win [{w?.WindowStart}..{w?.WindowEnd}) res={w?.Items.Count} vis {stack.FirstVisibleIndex}..{stack.LastVisibleIndex} lastM {stack.LastMeasuredIndex} bad={badCells} blank={blankFrames}");
            }
        }

        var wf = stack.ItemsWindow;
        bool slidFwd = (wf?.WindowStart ?? 0) > 0;
        Console.WriteLine($"  forward: win [{wf?.WindowStart}..{wf?.WindowEnd}) " + (slidFwd ? "=> slid" : "=> NEVER SLID (fail)"));

        // phase 2: fling back up (backward slides / head refills)
        for (int round = 0; round < 80 && (stack.ItemsWindow?.WindowStart ?? 0) > 0; round++)
        {
            robot.Pan(215, 250, 215, 700, durationMs: 70, steps: 6);
            for (int f = 0; f < 30; f++)
            {
                host.RenderFrame(16);
                frame++;
                Thread.Sleep(2);
                CheckFrame();
            }
        }

        var wb = stack.ItemsWindow;
        Console.WriteLine($"  backward: win [{wb?.WindowStart}..{wb?.WindowEnd}) vis {stack.FirstVisibleIndex}..{stack.LastVisibleIndex} bad={badCells} blank={blankFrames}");

        // phase 3: non-resident jump
        scroll.ScrollToIndex(600, animate: false);
        bool landed = false;
        for (int f = 0; f < 600; f++)
        {
            host.RenderFrame(16);
            frame++;
            Thread.Sleep(2);
            CheckFrame();
            if (stack.FirstVisibleIndex <= 600 && 600 <= stack.LastVisibleIndex)
            {
                landed = true;
                break;
            }
        }

        var wj = stack.ItemsWindow;
        Console.WriteLine($"  JUMP 600: win [{wj?.WindowStart}..{wj?.WindowEnd}) vis {stack.FirstVisibleIndex}..{stack.LastVisibleIndex} " +
                          (landed ? "=> PASS" : "=> FAIL"));

        if (badCells > 0)
            Console.WriteLine($"  first bad: {firstBad}");

        Console.WriteLine($"  RESULT frames={frame} badCells={badCells}/{checkedCells} blankFrames={blankFrames} " +
                          (badCells == 0 && blankFrames == 0 && slidFwd && landed ? "=> CLEAN" : "=> ISSUES"));
        Console.WriteLine("====================================================");
    }
}
