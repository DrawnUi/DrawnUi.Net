using System.Collections.ObjectModel;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// "SUPER fluid"路: SkiaCachedStack (banded Operations plane, double-buffer, prepared views,
/// its own MeasureVisible) + BUILT-IN ItemsSourceWindow over a big source. New combo — the plane
/// machinery was proven with the app-level chat window, never with the built-in one (its head trims
/// translate coordinates via CommitPendingHeadRemove -> OnContentTranslatedVertically re-anchor).
/// Asserts: no blank frames (plane never blits a hole after slides/commits), correct binds at rest,
/// slides both directions, non-resident jump.
/// </summary>
public static class CachedStackWindowRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= CACHEDSTACK + BUILT-IN WINDOW =========");

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
            Content = new SkiaCachedStack
                {
                    Type = LayoutType.Column,
                    HorizontalOptions = LayoutOptions.Fill,
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
                                    var n = int.Parse(s.Substring(5));
                                    me.HeightRequest = 60 + (n % 5) * 20;
                                }
                            });
                    }),
                    ItemsSource = items,
                }
                .Assign(out stack),
        };

        // this repro covers the double-buffer plane machinery over the built-in window: opt in so the
        // async bake runs while the viewport moves.
        ((SkiaCachedStack)stack).AutoDoubleBuffering = true;

        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: Colors.Black);
        host.Canvas.Content = scroll;

        for (int i = 0; i < 400 && stack.LastVisibleIndexLocal < 0; i++)
        {
            host.RenderFrame(16);
            Thread.Sleep(1);
        }

        host.AdvanceFrames(10, 16);

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

            if (host.NonBackgroundFraction(Colors.Black) < 0.05)
                blankFrames++;

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
                var expected = $"Item {start + t.FreezeIndex}";
                if (ctx != expected || label.Text != ctx)
                {
                    badCells++;
                    if (badCells == 1)
                        firstBad = $"frame {frame}: local {t.FreezeIndex} ctx '{ctx}' label '{label.Text}' expected '{expected}' win [{start}..{w?.WindowEnd})";
                }
            }
        }

        for (int round = 0; round < 25; round++)
        {
            robot.Pan(215, 700, 215, 250, durationMs: 70, steps: 6);
            for (int f = 0; f < 30; f++)
            {
                host.RenderFrame(16);
                frame++;
                Thread.Sleep(1);
                CheckFrame();
            }

            if (round % 15 == 0)
            {
                var w = stack.ItemsWindow;
                Console.WriteLine(
                    $"  fwd round {round}: win [{w?.WindowStart}..{w?.WindowEnd}) vis {stack.FirstVisibleIndex}..{stack.LastVisibleIndex} bad={badCells} blank={blankFrames}");
            }
        }

        var wf = stack.ItemsWindow;
        bool slidFwd = (wf?.WindowStart ?? 0) > 0;
        Console.WriteLine($"  forward: win [{wf?.WindowStart}..{wf?.WindowEnd}) " + (slidFwd ? "=> slid" : "=> NEVER SLID (fail)"));

        // BACKWARD, VIOLENT: device shows a plane GAP on fast backward flings (backward slides insert
        // a cold batch above; if the plane serves before coverage catches up -> empty band).
        int maxBand = 0, bandFrames = 0;
        var scratch = Environment.GetEnvironmentVariable("TEMP") ?? ".";
        for (int round = 0; round < 40 && (stack.ItemsWindow?.WindowStart ?? 0) > 0; round++)
        {
            robot.Pan(215, 150, 215, 850, durationMs: 50, steps: 4); // violent
            for (int f = 0; f < 30; f++)
            {
                host.RenderFrame(16);
                frame++;
                Thread.Sleep(1);
                CheckFrame();

                int band = host.MaxInteriorEmptyBand(Colors.Black, out var bt, out var bb);
                if (band > 120)
                {
                    bandFrames++;
                    if (band > maxBand)
                    {
                        maxBand = band;
                        host.SavePng(System.IO.Path.Combine(scratch, "cachedwin-band.png"));
                        Console.WriteLine($"  BAND {band}px @round {round} f{f} screenY [{bt}..{bb}] win [{stack.ItemsWindow?.WindowStart}..{stack.ItemsWindow?.WindowEnd}) vis {stack.FirstVisibleIndex}..{stack.LastVisibleIndex}");
                    }
                }
            }
        }

        var wb = stack.ItemsWindow;
        Console.WriteLine($"  backward: win [{wb?.WindowStart}..{wb?.WindowEnd}) vis {stack.FirstVisibleIndex}..{stack.LastVisibleIndex} bad={badCells} blank={blankFrames} maxBand={maxBand}px bandFrames={bandFrames}");

        scroll.ScrollToIndex(600, animate: false);
        bool landed = false;
        for (int f = 0; f < 600; f++)
        {
            host.RenderFrame(16);
            frame++;
            Thread.Sleep(1);
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
        Console.WriteLine("=================================================");
    }
}
