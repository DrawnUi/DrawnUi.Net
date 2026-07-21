using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Case "B": the Fiddle Feed preset (src/Blazor/DrawnUi.Fiddle/FiddlePresets.cs, `Feed`).
/// Reported on WASM: EMPTY CELLS appear while scrolling (as if BindingContext never applied),
/// then scrolling starts to lag. Reproduces the exact preset structure headlessly:
/// 1000 ints, MeasureFirst, RecyclingTemplate, cell content filled via
/// ObserveSelf(BindingContext). Every frame validates each VISIBLE cell's title text against
/// its BindingContext — an empty or mismatched title = the bug, platform-independent.
/// NOTE: 1000 items now auto-engages the built-in ItemsSourceWindow.
/// </summary>
public static class FeedPresetRepro
{
    static void DumpStructureRange(SkiaLayout stack)
    {
        var prop = typeof(SkiaLayout).GetProperty("StackStructure",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        var structure = prop?.GetValue(stack);
        var getChildren = structure?.GetType().GetMethod("GetChildren");
        var cells = (getChildren?.Invoke(structure, null) as IEnumerable<ControlInStack>)
            ?.Where(c => c != null).OrderBy(c => c.ControlIndex).ToList();
        if (cells == null || cells.Count == 0)
        {
            Console.WriteLine("    structure EMPTY");
            return;
        }

        Console.WriteLine(
            $"    structure {cells.Count} cells idx {cells[0].ControlIndex}..{cells[^1].ControlIndex} " +
            $"tops {cells[0].Destination.Top:0}..{cells[^1].Destination.Bottom:0}");
    }

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= FEED PRESET (B) EMPTY-CELLS REPRO =========");

        SkiaLayout stack = null;
        SkiaScroll scroll = null;

        var root = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };

        scroll = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            ZoomLocked = true, // browser: mouse wheel scrolls the list (fiddle case B input mode)
            Content = new SkiaLayout
            {
                Tag = "Feed",
                Type = LayoutType.Column,
                Spacing = 8,
                Padding = new Thickness(16, 8),
                HorizontalOptions = LayoutOptions.Fill,
                RecyclingTemplate = RecyclingTemplate.Enabled,
                MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
                ItemsSource = Enumerable.Range(1, 1000).ToList(),
                ItemTemplate = new DataTemplate(() =>
                {
                    SkiaLabel title = null;
                    SkiaLabel subtitle = null;
                    SkiaLabel initials = null;

                    return new SkiaLayout
                    {
                        Type = LayoutType.Row,
                        Spacing = 12,
                        Padding = new Thickness(12, 10),
                        BackgroundColor = Color.FromHex("#111827"),
                        HorizontalOptions = LayoutOptions.Fill,
                        UseCache = SkiaCacheType.Image,
                        Children =
                        {
                            new SkiaShape
                            {
                                Type = ShapeType.Circle,
                                WidthRequest = 42,
                                LockRatio = 1,
                                BackgroundColor = Color.FromHex("#1F2937"),
                                Children =
                                {
                                    new SkiaLabel
                                    {
                                        FontSize = 14,
                                        TextColor = Color.FromHex("#67E8F9"),
                                        HorizontalOptions = LayoutOptions.Center,
                                        VerticalOptions = LayoutOptions.Center,
                                    }.Assign(out initials),
                                }
                            },
                            new SkiaLayout
                            {
                                Type = LayoutType.Column,
                                Spacing = 3,
                                VerticalOptions = LayoutOptions.Center,
                                Children =
                                {
                                    new SkiaLabel
                                    {
                                        FontSize = 15,
                                        FontAttributes = FontAttributes.Bold,
                                        TextColor = Colors.White,
                                        Tag = "Title",
                                    }.Assign(out title),
                                    new SkiaLabel
                                    {
                                        FontSize = 12,
                                        TextColor = Color.FromHex("#94A3B8"),
                                    }.Assign(out subtitle),
                                }
                            },
                        }
                    }
                    .ObserveSelf((me, prop) =>
                    {
                        // recycled cell: only update content when a new item arrives
                        if (prop == nameof(SkiaControl.BindingContext) && me.BindingContext is int i)
                        {
                            initials.Text = $"{i % 100}";
                            title.Text = $"Contact {i}";
                            subtitle.Text = $"Recycled drawn cell #{i} — scroll me fast";
                        }
                    });
                })
            }.Assign(out stack)
        };

        root.AddSubView(scroll);

        // browser-parity: an AUTO-SIZED label whose text changes periodically (the fiddle FPS badge).
        // Suspect: its invalidation remeasures the stack -> MeasureStamp++ -> staged slide changes get
        // stamp-dropped -> adapter/structure desync (Data stuck at post-add count) + rebake spikes.
        SkiaLabel fpsLike = null;
        root.AddSubView(new SkiaLabel
        {
            Text = "FPS 0.0",
            FontSize = 12,
            BackgroundColor = Colors.Black,
            HorizontalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 8, 8, 0),
        }.Assign(out fpsLike));

        // faithful to preset B: DebugString observer overlay — every DrawStack raises
        // OnPropertyChanged(DebugString), the label text changes, canvas repaints -> a
        // CONTINUOUS repaint loop even when the scroll is at rest (like on device/WASM)
        root.AddSubView(new SkiaLabel
        {
            Margin = new(16, 0, 16, 100),
            Padding = 2,
            BackgroundColor = Color.Parse("#AA000000"),
            HorizontalOptions = LayoutOptions.Start,
            InputTransparent = true,
            TextColor = Colors.LawnGreen,
            VerticalOptions = LayoutOptions.Center,
            Rotation = -20,
            ZIndex = 100,
        }.ObserveProperty(() => stack, nameof(SkiaLayout.DebugString),
            me => { me.Text = stack.DebugString; }));

        // fiddle canvas geometry: 621x495 (~8 rows visible), wheel input
        using var host = new HeadlessCanvasHost(621, 495, scale: 1f, background: Colors.Black);
        host.Canvas.Content = root;
        host.AdvanceFrames(10, 16);

        Console.WriteLine(stack.ItemsWindow == null
            ? "  window NOT engaged"
            : $"  window engaged [{stack.ItemsWindow.WindowStart}..{stack.ItemsWindow.WindowEnd}) of 1000");

        var robot = new GestureRobot(host);
        int badFrames = 0, checkedCells = 0, badCells = 0, frame = 0;
        var firstBad = -1;
        int churnStops = 0; // rest positions where the window would not sit still

        // device signature: at the cut, DebugString "cells X/Y" collapses (11/24 -> 0/24) = all
        // in-use views dumped -> mass re-bind + cache re-bake = the lag spike. Track X per frame.
        int prevInUse = -1, minInUse = int.MaxValue, collapses = 0;
        var cellsRx = new System.Text.RegularExpressions.Regex(@"cells (\d+)/(\d+)");

        // reflection peek at the adapter's private in-use map (probe-only, no lib change)
        var adapterField = typeof(SkiaLayout).GetProperty("ChildrenFactory");
        var inUseField = typeof(ViewsAdapter).GetField("_cellsInUseViews",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        List<int> InUseKeys()
        {
            var adapter = adapterField?.GetValue(stack) as ViewsAdapter;
            var dict = adapter == null ? null : inUseField?.GetValue(adapter) as System.Collections.IDictionary;
            var keys = new List<int>();
            if (dict != null)
                foreach (var k in dict.Keys) keys.Add((int)k);
            keys.Sort();
            return keys;
        }

        var prevKeys = new List<int>();

        // browser bug 2026-07-09: "Data:" (adapter snapshot size) jumps between values while
        // scrolling, with lag spikes at the jump moments. Track Data per frame + frame wall time.
        var dataRx = new System.Text.RegularExpressions.Regex(@"Data:\s+(\d+)");
        int prevData = -1, dataFlips = 0, dataOffCapFrames = 0, spikes = 0;
        double worstFrameMs = 0;
        var sw = new System.Diagnostics.Stopwatch();

        void TrackData(string where, double frameMs)
        {
            var m = dataRx.Match(stack.DebugString ?? "");
            if (!m.Success) return;
            int data = int.Parse(m.Groups[1].Value);
            if (prevData > 0 && data != prevData)
            {
                dataFlips++;
                if (dataFlips <= 10)
                    Console.WriteLine($"  DATA FLIP {prevData}->{data} @{where} frame={frameMs:0.0}ms");
            }

            if (stack.ItemsWindow != null && data != stack.ItemsWindow.Items.Count)
                dataOffCapFrames++;

            if (frameMs > 32) { spikes++; if (frameMs > worstFrameMs) worstFrameMs = frameMs; }
            prevData = data;
        }

        void TrackInUse(string where)
        {
            var m = cellsRx.Match(stack.DebugString ?? "");
            if (!m.Success) return;
            int inUse = int.Parse(m.Groups[1].Value);
            if (inUse < minInUse) minInUse = inUse;
            var keys = InUseKeys();
            if (prevInUse >= 8 && inUse <= prevInUse / 2)
            {
                collapses++;
                if (collapses <= 8)
                {
                    Console.WriteLine($"  IN-USE COLLAPSE {prevInUse}->{inUse} @{where} win [{stack.ItemsWindow?.WindowStart}..{stack.ItemsWindow?.WindowEnd}) visLocal {stack.FirstVisibleIndexLocal}..{stack.LastVisibleIndexLocal}");
                    Console.WriteLine($"    prev keys: {string.Join(",", prevKeys)}");
                    Console.WriteLine($"    cur  keys: {string.Join(",", keys)}");
                    Console.WriteLine($"    offY {scroll.ViewportOffsetY:0.0} contentH {stack.MeasuredSize.Pixels.Height:0}");
                    DumpStructureRange(stack);
                }
            }
            prevInUse = inUse;
            prevKeys = keys;
        }

        for (int round = 0; round < 60; round++)
        {
            // DEVICE-style fast flick: high-velocity pan -> FLING animator drives frames (the finger
            // path, not the wheel RangeAnimator). Track in-use through the whole inertia.
            robot.Pan(310, 420, 310, 120, durationMs: 60, steps: 5);

            for (int f = 0; f < 40; f++)
            {
                if (frame % 30 == 0)
                    fpsLike.Text = $"FPS {60 + (frame % 40) * 0.7:0.0}"; // width changes -> relayout ripple

                sw.Restart();
                host.RenderFrame(16);
                sw.Stop();
                frame++;
                TrackInUse($"round {round} f{f}");
                TrackData($"round {round} f{f}", sw.Elapsed.TotalMilliseconds);

                var tree = stack.RenderTree;
                if (tree == null)
                    continue;

                bool frameBad = false;
                foreach (var t in tree)
                {
                    if (t.Control is not SkiaLayout row || row.BindingContext is not int ctx)
                        continue;

                    var title = row.FindViewByTag("Title") as SkiaLabel;
                    if (title == null)
                        continue;

                    checkedCells++;
                    var expected = $"Contact {ctx}";
                    if (string.IsNullOrEmpty(title.Text) || title.Text != expected)
                    {
                        badCells++;
                        frameBad = true;
                        if (firstBad < 0)
                        {
                            firstBad = frame;
                            Console.WriteLine(
                                $"  FIRST BAD @frame {frame}: ctx={ctx} title='{title.Text}' " +
                                $"win [{stack.ItemsWindow?.WindowStart}..{stack.ItemsWindow?.WindowEnd}) vis {stack.FirstVisibleIndex}..{stack.LastVisibleIndex}");
                        }
                    }
                }

                if (frameBad)
                    badFrames++;
            }

            var w = stack.ItemsWindow;
            if (round % 10 == 0)
            {
                Console.WriteLine(
                    $"  round {round}: win [{w?.WindowStart}..{w?.WindowEnd}) vis {stack.FirstVisibleIndex}..{stack.LastVisibleIndex} badCells={badCells}/{checkedCells}");
            }

            // IDLE AT THIS DEPTH. Phase 1: let inertia die (state unchanged 30 consecutive frames).
            // Phase 2: settled state must STAY frozen — any later move = the reported churn bug
            // (indexes jumping at rest after the first cut).
            (int s, int e, int v, float o) State()
            {
                var cw = stack.ItemsWindow;
                return (cw?.WindowStart ?? -1, cw?.WindowEnd ?? -1, stack.FirstVisibleIndex, scroll.ViewportOffsetY);
            }

            var prev = State();
            int calm = 0;
            for (int f = 0; f < 600 && calm < 30; f++)
            {
                host.RenderFrame(16);
                frame++;
                Thread.Sleep(2);
                var cur = State();
                calm = cur == prev ? calm + 1 : 0;
                prev = cur;
            }

            if (calm < 30)
            {
                churnStops++;
                Console.WriteLine($"  round {round}: NEVER SETTLES (600 frames) win [{prev.s}..{prev.e}) vis {prev.v}");
                continue;
            }

            int movesHere = 0;
            for (int f = 0; f < 120; f++)
            {
                host.RenderFrame(16);
                frame++;
                Thread.Sleep(2);
                var cur = State();
                if (cur != prev)
                {
                    movesHere++;
                    if (churnStops < 4 && movesHere <= 3)
                        Console.WriteLine(
                            $"    CHURN @round {round} settledFrame {f}: win [{prev.s}..{prev.e})vis{prev.v}off{prev.o:0} -> [{cur.s}..{cur.e})vis{cur.v}off{cur.o:0}");
                    prev = cur;
                }
            }

            if (movesHere > 0)
            {
                churnStops++;
                Console.WriteLine($"  round {round}: RESUMED MOTION after settle ({movesHere} moves in 120 frames)");
            }
        }

        // IDLE PHASE: viewport at rest must produce a STATIONARY window. The ping-pong churn
        // (forward/backward slides re-triggering each other) shows up as WindowStart oscillating
        // here — exactly the "jumping diags" seen on device A and the unbound cells on WASM.
        int slidesAtRest = 0;
        int prevStart = stack.ItemsWindow?.WindowStart ?? -1;
        for (int f = 0; f < 200; f++)
        {
            host.RenderFrame(16);
            Thread.Sleep(2);
            var s = stack.ItemsWindow?.WindowStart ?? -1;
            if (s != prevStart)
            {
                slidesAtRest++;
                if (slidesAtRest <= 6)
                    Console.WriteLine($"  REST-SLIDE @idle frame {f}: window start {prevStart} -> {s}");
                prevStart = s;
            }
        }

        Console.WriteLine($"  IDLE: {slidesAtRest} window moves in 200 rest frames " +
                          (slidesAtRest <= 1 ? "=> STATIONARY" : "=> PING-PONG CHURN"));

        Console.WriteLine($"  RESULT frames={frame} badFrames={badFrames} badCells={badCells}/{checkedCells} churnStops={churnStops} " +
                          $"inUseCollapses={collapses} minInUse={minInUse} dataFlips={dataFlips} dataDesyncFrames={dataOffCapFrames} spikes={spikes} worst={worstFrameMs:0.0}ms " +
                          (badCells == 0 && churnStops == 0 && collapses == 0 ? "=> CLEAN" : "=> BUG REPRODUCED"));
        Console.WriteLine("=================================================");
    }
}
