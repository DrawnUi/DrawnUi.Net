using System.Collections.ObjectModel;
using System.Windows.Input;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// PHASE 2: chat's DEFINING conditions over the BUILT-IN ItemsSourceWindow (no WindowedSource,
/// no custom windowing): INVERTED scroll (Rotation=180 + ReverseGestures), plain NEWEST-FIRST
/// ObservableCollection bound directly (5000 &gt; threshold 300 -&gt; window auto-engages),
/// MeasureVisible, VARIABLE heights, recycled cells.
/// Chat semantics on top: new message = Insert(0) (head), older history = window slides forward,
/// API paging = LoadMoreCommand appends OLDER to the TAIL at the true source end,
/// jump-to-oldest = ScrollToIndex(Count-1) global (window rebase), jump-to-newest = ScrollToIndex(0).
/// Gate for the chat migration: zero spikes, no blank frames, correct binds, glued viewport on head inserts.
/// </summary>
public static class BuiltinWindowChatRepro
{
    static ObservableCollection<ChatLikeScene.ChatRow> MakeItems(int total)
    {
        // newest-first: index 0 = newest message (like the real chat's descending binding)
        var items = new ObservableCollection<ChatLikeScene.ChatRow>();
        for (int i = 0; i < total; i++)
        {
            int global = total - 1 - i; // 0 = oldest in history terms
            int lines = 1 + (global * 7 % 5);
            items.Add(new ChatLikeScene.ChatRow
            {
                Index = global,
                Lines = lines,
                Outgoing = (global % 3) == 0,
                Text = $"Message {global}" + string.Concat(Enumerable.Repeat("\nlorem ipsum dolor", lines - 1)),
            });
        }

        return items;
    }

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= BUILTIN-WINDOW CHAT (inverted, newest-first) =========");
        try
        {
            RunCore();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  CRASH: {ex}");
        }

        Console.WriteLine("================================================================");
    }

    static void RunCore()
    {
        const int Total = 5000;
        var items = MakeItems(Total);
        int loadOlderFired = 0;

        using var host = new HeadlessCanvasHost(430, 720, scale: 1f, background: Colors.Black);

        SkiaLayout list = null;
        var scroll = new SkiaScroll
        {
            Orientation = ScrollOrientation.Vertical,
            ResetScrollPositionOnContentSizeChanged = false,
            Rotation = 180,
            ReverseGestures = true,
            TrackIndexPosition = RelativePositionType.Start,
            LoadMoreOffset = 800,
            // "API returns older page": append OLDER messages to the TAIL of the newest-first list
            LoadMoreCommand = new Command(() =>
            {
                loadOlderFired++;
                int oldest = items[^1].Index;
                for (int i = 1; i <= 50; i++)
                {
                    int g = oldest - i;
                    if (g < 0) break;
                    items.Add(new ChatLikeScene.ChatRow { Index = g, Text = $"Message {g}" });
                }
            }),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout
                {
                    Type = LayoutType.Column,
                    Spacing = 4,
                    Padding = new Thickness(0, 8),
                    ItemsSource = items,
                    ItemTemplateType = typeof(ChatLikeScene.ChatRowCell),
                    RecyclingTemplate = RecyclingTemplate.Enabled,
                    MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
                    VirtualisationInflated = 100,
                    UseCache = SkiaCacheType.None,
                    FastMeasurement = true,
                    HorizontalOptions = LayoutOptions.Fill,
                }
                .Assign(out list),
        };

        host.Canvas.Content = scroll;

        // warmup
        for (int i = 0; i < 30; i++) { host.RenderFrame(16); Thread.Sleep(4); }

        bool engaged = list.ItemsWindow != null;
        double fill0 = host.NonBackgroundFraction(Colors.Black);
        Console.WriteLine($"  warmup: window={(engaged ? "ENGAGED" : "OFF")} data={list.ItemsWindow?.Items.Count} " +
                          $"vis {list.FirstVisibleIndex}..{list.LastVisibleIndex} fill={fill0:0.00} contentH={list.MeasuredSize.Pixels.Height:0}");
        Console.WriteLine(engaged && fill0 > 0.3 && list.FirstVisibleIndex == 0
            ? "  => PASS (engaged, newest at viewport)"
            : "  => FAIL (window/fill/anchor wrong)");

        var robot = new GestureRobot(host);
        var sw = new System.Diagnostics.Stopwatch();

        // ---- phase A: ride INTO HISTORY through many slides; spike + blank + bind probes ----
        int spikes = 0, blankFrames = 0, badBinds = 0, checkedBinds = 0;
        double worst = 0;
        for (int round = 0; round < 60; round++)
        {
            robot.Pan(215, 250, 215, 700, durationMs: 90, steps: 8); // inverted: drag down = into history
            for (int f = 0; f < 14; f++)
            {
                sw.Restart();
                host.RenderFrame(16);
                double ms = sw.Elapsed.TotalMilliseconds;
                if (ms > 32) { spikes++; if (ms > worst) worst = ms; }
                Thread.Sleep(2);
            }

            if (host.NonBackgroundFraction(Colors.Black) < 0.15) blankFrames++;

            // bind correctness: rendered contexts must be STRICTLY DESCENDING history indices in tree
            // order, no duplicates (probe lesson: only assert on stable frames — mid-slide trees lag)
            if (!list.HasPendingStructureChanges)
            {
                var tree = list.RenderTree;
                if (tree != null)
                {
                    int prev = int.MaxValue;
                    foreach (var t in tree)
                    {
                        if (t.FreezeBindingContext is ChatLikeScene.ChatRow r)
                        {
                            checkedBinds++;
                            if (r.Index >= prev) badBinds++;
                            prev = r.Index;
                        }
                    }
                }
            }
        }

        Console.WriteLine($"  ride-into-history: 60 flings, window [{list.ItemsWindow?.WindowStart}..{list.ItemsWindow?.WindowEnd}) " +
                          $"vis {list.FirstVisibleIndex}..{list.LastVisibleIndex} spikes={spikes} worst={worst:0.0}ms " +
                          $"blankRounds={blankFrames} badBinds={badBinds}/{checkedBinds} loadMore={loadOlderFired}");
        // tolerate <=2 wall-clock spikes: suite context adds machine-load noise (FeedPreset counts
        // CLEAN with the same); blanks and binds stay ZERO-tolerance
        Console.WriteLine(spikes <= 2 && blankFrames == 0 && badBinds == 0
            ? "  => PASS (smooth, bound, non-blank)"
            : "  => FAIL");

        // ---- phase B: NEW MESSAGE Insert(0) while scrolled deep — viewport must stay glued ----
        // settle residual fling motion first, then anchor on a cell INSIDE the viewport
        float lastOff = float.NaN;
        for (int f = 0; f < 120; f++)
        {
            host.RenderFrame(16);
            Thread.Sleep(3);
            if (Math.Abs(scroll.ViewportOffsetY - lastOff) < 0.01f && f > 10) break;
            lastOff = scroll.ViewportOffsetY;
        }

        var anchor = list.RenderTree?.FirstOrDefault(t =>
            t.FreezeBindingContext is ChatLikeScene.ChatRow && t.HitRect.Top > 100 && t.HitRect.Top < 500);
        float anchorYBefore = anchor?.HitRect.Top ?? float.NaN;
        int anchorIdx = (anchor?.FreezeBindingContext as ChatLikeScene.ChatRow)?.Index ?? -1;
        int newIdx = items[0].Index + 1;
        items.Insert(0, new ChatLikeScene.ChatRow { Index = newIdx, Outgoing = true, Text = $"Message {newIdx} NEW" });
        for (int f = 0; f < 20; f++) { host.RenderFrame(16); Thread.Sleep(4); }
        var anchorAfter = list.RenderTree?.FirstOrDefault(t =>
            t.FreezeBindingContext is ChatLikeScene.ChatRow r && r.Index == anchorIdx);
        float anchorYAfter = anchorAfter?.HitRect.Top ?? float.NaN;
        Console.WriteLine($"  head insert while deep: anchor msg {anchorIdx} y {anchorYBefore:0} -> {anchorYAfter:0}");
        Console.WriteLine(!float.IsNaN(anchorYAfter) && Math.Abs(anchorYAfter - anchorYBefore) < 3
            ? "  => PASS (viewport glued)"
            : "  => FAIL (viewport shifted or anchor lost)");

        // ---- phase C: jump to OLDEST (global ScrollToIndex over window rebase) ----
        scroll.ScrollToIndex(items.Count - 1, false);
        for (int f = 0; f < 200; f++)
        {
            host.RenderFrame(16);
            Thread.Sleep(3);
            if (!scroll.OrderedScrollToIndexIsSet && f > 30) break;
        }

        double fillOld = host.NonBackgroundFraction(Colors.Black);
        Console.WriteLine($"  jump OLDEST: win [{list.ItemsWindow?.WindowStart}..{list.ItemsWindow?.WindowEnd}) " +
                          $"vis {list.FirstVisibleIndex}..{list.LastVisibleIndex} of {items.Count} fill={fillOld:0.00}");
        Console.WriteLine(list.LastVisibleIndex >= items.Count - 3 && fillOld > 0.3
            ? "  => PASS (landed at oldest, viewport full)"
            : "  => FAIL");

        // ---- phase D: jump back to NEWEST ----
        scroll.ScrollToIndex(0, false);
        for (int f = 0; f < 200; f++)
        {
            host.RenderFrame(16);
            Thread.Sleep(3);
            if (!scroll.OrderedScrollToIndexIsSet && f > 30) break;
        }

        double fillNew = host.NonBackgroundFraction(Colors.Black);
        Console.WriteLine($"  jump NEWEST: vis {list.FirstVisibleIndex}..{list.LastVisibleIndex} fill={fillNew:0.00}");
        Console.WriteLine(list.FirstVisibleIndex <= 1 && fillNew > 0.3
            ? "  => PASS (back at newest)"
            : "  => FAIL");

        // ---- phase E: new message at rest — appears at the visual bottom ----
        int newIdx2 = items[0].Index + 1;
        items.Insert(0, new ChatLikeScene.ChatRow { Index = newIdx2, Outgoing = true, Text = $"Message {newIdx2} LIVE" });
        for (int f = 0; f < 25; f++) { host.RenderFrame(16); Thread.Sleep(4); }
        // public visibility is now BARE (really intersecting): the just-inserted cell 0 can sit a
        // couple px outside while the viewport stays glued — check the RENDER TREE for the message
        bool liveShown = list.RenderTree?.Any(t =>
            t.FreezeBindingContext is ChatLikeScene.ChatRow r && r.Index == newIdx2) == true;
        Console.WriteLine($"  live message at rest: vis {list.FirstVisibleIndex}..{list.LastVisibleIndex} shown={liveShown}");
        Console.WriteLine(liveShown && list.FirstVisibleIndex <= 1
            ? "  => PASS (new message visible)"
            : "  => FAIL");
    }
}
