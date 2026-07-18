using System.Collections.ObjectModel;
using System.Windows.Input;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// TELEGRAM-STYLE chat lifecycle over the built-in window (the migration scenario):
///  1. cold start: 80 newest from "DB" (&lt; threshold 300) — window OFF, instant list;
///  2. reading history: LoadMoreCommand pages +50 older (appended to the newest-first tail);
///     crossing 300 must ENGAGE-ON-GROW the window IN PLACE — anchored, no visual jump, no blank;
///  3. incoming messages Insert(0) while reading — viewport glued (unread-badge scenario);
///  4. badge tap: global ScrollToIndex(0) back to newest;
///  5. keep paging deeper after engage — LoadMore still fires at the true end only.
/// </summary>
public static class TelegramLikeRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= TELEGRAM-LIKE (engage-on-grow, badge jump) =========");
        try
        {
            RunCore();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  CRASH: {ex}");
        }

        Console.WriteLine("==============================================================");
    }

    static ChatLikeScene.ChatRow MakeRow(int global) => new()
    {
        Index = global,
        Lines = 1 + (global * 7 % 5),
        Outgoing = (global % 3) == 0,
        Text = $"Message {global}" + string.Concat(Enumerable.Repeat("\nlorem ipsum dolor", global * 7 % 5)),
    };

    static void RunCore()
    {
        const int HistoryTop = 100_000; // newest message id at cold start
        var items = new ObservableCollection<ChatLikeScene.ChatRow>();
        for (int i = 0; i < 80; i++) items.Add(MakeRow(HistoryTop - i)); // 80 newest, newest-first

        int pagesLoaded = 0;

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
            LoadMoreCommand = new Command(() =>
            {
                pagesLoaded++;
                int oldest = items[^1].Index;
                for (int i = 1; i <= 50; i++) items.Add(MakeRow(oldest - i)); // older page from "server"
            }),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout
                {
                    Type = LayoutType.Column,
                    Spacing = 4,
                    Padding = new Thickness(0, 8),
                    ItemsSource = items,
                    ItemTemplate = new DataTemplate(() => new ChatLikeScene.ChatRowCell()),
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

        for (int i = 0; i < 30; i++) { host.RenderFrame(16); Thread.Sleep(4); }
        Console.WriteLine($"  cold start: items={items.Count} window={(list.ItemsWindow != null ? "ENGAGED" : "off")} " +
                          $"vis {list.FirstVisibleIndex}..{list.LastVisibleIndex} fill={host.NonBackgroundFraction(Colors.Black):0.00}");
        Console.WriteLine(list.ItemsWindow == null && list.FirstVisibleIndex == 0
            ? "  => PASS (fast plain list under threshold)"
            : "  => FAIL");

        // ---- read history: fling up until ONE PAGE below the threshold ----
        var robot = new GestureRobot(host);
        int blankRounds = 0;
        var sw = new System.Diagnostics.Stopwatch();
        int spikes = 0;
        double worst = 0;

        for (int round = 0; round < 60 && items.Count < 280; round++)
        {
            robot.Pan(215, 250, 215, 700, durationMs: 90, steps: 8);
            for (int f = 0; f < 14; f++)
            {
                sw.Restart();
                host.RenderFrame(16);
                double ms = sw.Elapsed.TotalMilliseconds;
                if (ms > 32) { spikes++; if (ms > worst) worst = ms; }
                Thread.Sleep(2);
            }

            if (host.NonBackgroundFraction(Colors.Black) < 0.15) blankRounds++;
        }

        // settle, anchor mid-viewport, then grow past the threshold WITHOUT any user motion:
        // the engage must be pixel-invisible
        float lastOff0 = float.NaN;
        for (int f = 0; f < 120; f++)
        {
            host.RenderFrame(16);
            Thread.Sleep(3);
            if (Math.Abs(scroll.ViewportOffsetY - lastOff0) < 0.01f && f > 10) break;
            lastOff0 = scroll.ViewportOffsetY;
        }

        var eng = list.RenderTree?.FirstOrDefault(t =>
            t.FreezeBindingContext is ChatLikeScene.ChatRow && t.HitRect.Top > 100 && t.HitRect.Top < 400);
        int anchorIdx = (eng?.FreezeBindingContext as ChatLikeScene.ChatRow)?.Index ?? -1;
        float anchorBefore = eng?.HitRect.Top ?? float.NaN;
        bool wasOff = list.ItemsWindow == null;

        Console.WriteLine($"  pre-grow: offY={scroll.ViewportOffsetY:0} vis {list.FirstVisibleIndex}..{list.LastVisibleIndex} contentH={list.MeasuredSize.Pixels.Height:0}");

        // "server page arrives": grow over the threshold while the user reads, settled
        int oldest0 = items[^1].Index;
        for (int i = 1; i <= 60; i++) items.Add(MakeRow(oldest0 - i));
        for (int f = 0; f < 30; f++) { host.RenderFrame(16); Thread.Sleep(4); }

        Console.WriteLine($"  post-grow: offY={scroll.ViewportOffsetY:0} vis {list.FirstVisibleIndex}..{list.LastVisibleIndex} contentH={list.MeasuredSize.Pixels.Height:0}");

        bool engaged = list.ItemsWindow != null;
        var engAfter = list.RenderTree?.FirstOrDefault(t =>
            t.FreezeBindingContext is ChatLikeScene.ChatRow r && r.Index == anchorIdx);
        float anchorAfter = engAfter?.HitRect.Top ?? float.NaN;

        Console.WriteLine($"  grow: items={items.Count} pages={pagesLoaded} wasOff={wasOff} engaged={engaged} " +
                          $"win [{list.ItemsWindow?.WindowStart}..{list.ItemsWindow?.WindowEnd}) vis {list.FirstVisibleIndex}..{list.LastVisibleIndex} " +
                          $"spikes={spikes} worst={worst:0.0}ms blankRounds={blankRounds}");
        Console.WriteLine($"  engage anchor: msg {anchorIdx} y {anchorBefore:0} -> {anchorAfter:0}");
        bool anchorOk = !float.IsNaN(anchorAfter) && Math.Abs(anchorAfter - anchorBefore) < 60; // < one row
        Console.WriteLine(wasOff && engaged && blankRounds == 0 && anchorOk
            ? "  => PASS (engaged in place, anchored, no blanks)"
            : "  => FAIL");

        // ---- incoming while reading (badge scenario) ----
        float lastOff = float.NaN;
        for (int f = 0; f < 120; f++)
        {
            host.RenderFrame(16);
            Thread.Sleep(3);
            if (Math.Abs(scroll.ViewportOffsetY - lastOff) < 0.01f && f > 10) break;
            lastOff = scroll.ViewportOffsetY;
        }

        var deepAnchor = list.RenderTree?.FirstOrDefault(t =>
            t.FreezeBindingContext is ChatLikeScene.ChatRow && t.HitRect.Top > 100 && t.HitRect.Top < 500);
        int deepIdx = (deepAnchor?.FreezeBindingContext as ChatLikeScene.ChatRow)?.Index ?? -1;
        float deepYBefore = deepAnchor?.HitRect.Top ?? float.NaN;
        for (int m = 1; m <= 5; m++) items.Insert(0, MakeRow(HistoryTop + m)); // 5 incoming
        for (int f = 0; f < 25; f++) { host.RenderFrame(16); Thread.Sleep(4); }
        var deepAfter = list.RenderTree?.FirstOrDefault(t =>
            t.FreezeBindingContext is ChatLikeScene.ChatRow r && r.Index == deepIdx);
        float deepYAfter = deepAfter?.HitRect.Top ?? float.NaN;
        Console.WriteLine($"  5 incoming while reading: anchor msg {deepIdx} y {deepYBefore:0} -> {deepYAfter:0}");
        Console.WriteLine(!float.IsNaN(deepYAfter) && Math.Abs(deepYAfter - deepYBefore) < 3
            ? "  => PASS (viewport glued, badge-able)"
            : "  => FAIL");

        // ---- badge tap: jump to newest ----
        scroll.ScrollToIndex(0, false);
        for (int f = 0; f < 200; f++)
        {
            host.RenderFrame(16);
            Thread.Sleep(3);
            if (!scroll.OrderedScrollToIndexIsSet && f > 30) break;
        }

        double fillNew = host.NonBackgroundFraction(Colors.Black);
        bool newestShown = list.RenderTree?.Any(t =>
            t.FreezeBindingContext is ChatLikeScene.ChatRow r && r.Index == HistoryTop + 5) == true;
        Console.WriteLine($"  badge jump: vis {list.FirstVisibleIndex}..{list.LastVisibleIndex} fill={fillNew:0.00} newestShown={newestShown}");
        Console.WriteLine(list.FirstVisibleIndex <= 1 && fillNew > 0.3 && newestShown
            ? "  => PASS (at newest incl. unread)"
            : "  => FAIL");

        // ---- keep paging deeper after engage ----
        int pagesBefore = pagesLoaded;
        scroll.ScrollToIndex(items.Count - 1, false);
        for (int f = 0; f < 250; f++)
        {
            host.RenderFrame(16);
            Thread.Sleep(3);
            if (f % 10 == 0) robot.Pan(215, 250, 215, 700, durationMs: 60, steps: 4); // press into the old edge
            if (pagesLoaded > pagesBefore) break;
        }

        Console.WriteLine($"  deep paging: pages {pagesBefore} -> {pagesLoaded} items={items.Count} " +
                          $"win [{list.ItemsWindow?.WindowStart}..{list.ItemsWindow?.WindowEnd})");
        Console.WriteLine(pagesLoaded > pagesBefore
            ? "  => PASS (LoadMore still fires at true end)"
            : "  => FAIL");
    }
}
