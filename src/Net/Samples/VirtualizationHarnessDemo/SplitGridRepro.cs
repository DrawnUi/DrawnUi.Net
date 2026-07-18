using AppoMobi.Specials;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// 2-COLUMN GRID (Split=2) + MeasureFirst + fixed-height cells, mimicking the DrawnCells shop grid:
/// device 2026-07-17 renders the region beyond the first viewport as a SINGLE column — structure dump
/// shows cells with FULL row width, right edge at their slot ([-212..202]/[0..414] signature), i.e.
/// Destination = [slotRight - fullWidth .. slotRight]. Reproduce headless: measure, settle, dump the
/// structure and assert every cell sits in its proper column slot (col0.Left=0, col1.Left=colW+spacing,
/// width=colW). Then LoadMore-append and assert again.
/// </summary>
public static class SplitGridRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= SPLIT GRID (2 columns, MeasureFirst, fixed rows) =========");
        try { RunCore(); }
        catch (Exception ex) { Console.WriteLine($"  CRASH: {ex}"); }
        Console.WriteLine("====================================================================");
    }

    class Item
    {
        public int Id { get; init; }
        public string Title { get; init; } = "";
    }

    static void RunCore()
    {
        var bg = Colors.Black;
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: bg);

        var items = new List<Item>(Enumerable.Range(1, 40).Select(i => new Item { Id = i, Title = $"Product {i}" }));

        SkiaLayout grid = null;
        SkiaScroll scroll = null;
        host.Canvas.Content = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaScroll
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Content = new SkiaLayout
                    {
                        Type = LayoutType.Column,
                        Split = 2,
                        Spacing = 10,
                        Padding = new Thickness(10, 12),
                        HorizontalOptions = LayoutOptions.Fill,
                        RecyclingTemplate = RecyclingTemplate.Enabled,
                        MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
                        Virtualisation = VirtualisationType.Enabled,
                        ItemsSource = items,
                        ItemTemplate = new DataTemplate(() => new SkiaShape
                        {
                            Type = ShapeType.Rectangle,
                            CornerRadius = 8,
                            BackgroundColor = Colors.White,
                            HeightRequest = 270,
                            HorizontalOptions = LayoutOptions.Fill,
                        }),
                    }.Assign(out grid)
                }.Assign(out scroll)
            }
        };

        for (int i = 0; i < 300 && grid.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(3); }
        // settle: MeasureFirst frontier catches up per frame
        for (int i = 0; i < 200; i++)
        {
            host.RenderFrame(16);
            Thread.Sleep(2);
            if (grid.LastMeasuredIndex >= items.Count - 1) break;
        }
        Console.WriteLine($"  measured up to {grid.LastMeasuredIndex}/{items.Count - 1} vis=[{grid.FirstVisibleIndex}..{grid.LastVisibleIndex}]");

        int bad = AssertSlots(grid, "initial");

        // ---- LoadMore-style append (single AddRange-like Add event via List + manual invalidate is not
        // the app's path; mimic the app: ObservableCollection Add event) — use a fresh source with INCC.
        // The app uses ObservableRangeCollection.AddRange => ONE Add event with 40 items.
        var obs = new ObservableRangeCollection<Item>(items);
        grid.ItemsSource = obs;
        for (int i = 0; i < 100; i++) { host.RenderFrame(16); Thread.Sleep(2); if (grid.LastMeasuredIndex >= obs.Count - 1) break; }

        obs.AddRange(Enumerable.Range(41, 40).Select(i => new Item { Id = i, Title = $"Product {i}" }));
        for (int i = 0; i < 300; i++) { host.RenderFrame(16); Thread.Sleep(2); if (grid.LastMeasuredIndex >= obs.Count - 1) break; }
        Console.WriteLine($"  after append: measured up to {grid.LastMeasuredIndex}/{obs.Count - 1}");

        // scroll deep so far cells realize/heal if they would on device
        var robot = new GestureRobot(host);
        for (int f = 0; f < 8; f++) { robot.Pan(220, 800, 220, 200, durationMs: 60, steps: 5); host.AdvanceFrames(6, 16); Thread.Sleep(5); }

        bad += AssertSlots(grid, "after append+scroll");

        Console.WriteLine(bad == 0
            ? "=> PASS (every cell in its proper column slot)"
            : $"=> FAIL ({bad} cells out of their column slot — single-column collapse reproduced)");
    }

    /// <summary>Walk the structure; verify each cell's Destination sits in its column slot.</summary>
    static int AssertSlots(SkiaLayout grid, string phase)
    {
        var s = grid.GetType().GetProperty("StackStructure",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(grid) as LayoutStructure;
        if (s == null) { Console.WriteLine($"  {phase}: NO STRUCTURE"); return 1; }

        // slots from the first (known-good) row
        float colW = -1, col1Left = -1;
        int bad = 0, total = 0, shown = 0;
        foreach (var c in s.GetChildren())
        {
            if (c == null || !c.WasMeasured) continue;
            total++;
            int col = c.ControlIndex % 2;
            if (colW < 0 && col == 0) colW = c.Destination.Width;
            if (col1Left < 0 && col == 1) col1Left = c.Destination.Left;

            bool wrong =
                (col == 0 && Math.Abs(c.Destination.Left) > 1f) ||
                (col == 1 && c.Destination.Left < 10f) || // col1 must start at ~colW+spacing, never at 0
                c.Destination.Width > (grid.MeasuredSize.Pixels.Width * 0.7f); // full-width cell in a 2-col grid

            if (wrong)
            {
                bad++;
                if (++shown <= 8)
                    Console.WriteLine($"   BAD i={c.ControlIndex} col={col} dest=[{c.Destination.Left:0},{c.Destination.Top:0},{c.Destination.Right:0},{c.Destination.Bottom:0}] w={c.Destination.Width:0}");
            }
        }

        Console.WriteLine($"  {phase}: cells={total} badSlots={bad} (colW~{colW:0}, col1Left~{col1Left:0})");
        return bad;
    }
}
