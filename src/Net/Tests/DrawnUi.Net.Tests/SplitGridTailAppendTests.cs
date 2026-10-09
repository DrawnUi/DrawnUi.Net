using AppoMobi.Specials;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A templated Split grid that gets items appended at the END keeps its structure even when the page does
/// not fill whole rows: an append moves no existing item, so no column can flip. The new items fill the
/// partial last row, then whole rows. Before, any append whose count or start was not a multiple of Split
/// re-laid and rebound the whole grid (a paged photo grid: 180 binds and an 89 ms frame for the last page).
/// </summary>
public class SplitGridTailAppendTests
{
    const int Split = 3;
    const float RowHeight = 134;
    const float Spacing = 2;

    sealed class Item
    {
        public int Id { get; init; }
    }

    static IEnumerable<Item> Items(int start, int count) => Enumerable.Range(start, count).Select(i => new Item { Id = i });

    sealed class Cell : SkiaShape
    {
        public static readonly List<int> Bound = new();

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is Item item)
                lock (Bound) Bound.Add(item.Id);
        }
    }

    sealed class Grid : SkiaLayout
    {
        public List<ControlInStack> Cells => StackStructure?.GetChildren()
            .Where(c => c != null && c.ControlIndex >= 0).OrderBy(c => c.ControlIndex).ToList() ?? new();
    }

    static (HeadlessCanvasHost host, Grid grid, ObservableRangeCollection<Item> items) Scene(MeasuringStrategy strategy,
        int initial)
    {
        var host = new HeadlessCanvasHost(402, 700, scale: 1f, background: Colors.Black);
        var items = new ObservableRangeCollection<Item>();
        items.AddRange(Items(0, initial));
        var grid = new Grid
        {
            Type = LayoutType.Column,
            Split = Split,
            Spacing = Spacing,
            HorizontalOptions = LayoutOptions.Fill,
            ItemsSource = items,
            RecyclingTemplate = RecyclingTemplate.Enabled,
            MeasureItemsStrategy = strategy,
            ItemTemplate = new DataTemplate(() => new Cell
            {
                Type = ShapeType.Rectangle,
                BackgroundColor = Colors.DarkSlateBlue,
                HorizontalOptions = LayoutOptions.Fill,
                HeightRequest = RowHeight,
            }),
        };
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = grid,
        };
        Settle(host, grid, items.Count);
        return (host, grid, items);
    }

    /// <summary>Frames until every item is in the structure and no frame is asked for (background measurement runs on threads).</summary>
    static void Settle(HeadlessCanvasHost host, Grid grid, int count)
    {
        for (int i = 0; i < 300; i++)
        {
            host.RenderFrame(16);
            if (!host.NeedsFrame && grid.Cells.Count >= count && i > 4)
                break;
            Thread.Sleep(2);
        }
        host.AdvanceFrames(3);
    }

    [Theory]
    [InlineData(MeasuringStrategy.MeasureFirst, 12, 3)] // whole rows: preserved before too
    [InlineData(MeasuringStrategy.MeasureVisible, 12, 3)]
    [InlineData(MeasuringStrategy.MeasureFirst, 12, 1)]
    [InlineData(MeasuringStrategy.MeasureFirst, 12, 2)]
    [InlineData(MeasuringStrategy.MeasureFirst, 12, 4)]
    [InlineData(MeasuringStrategy.MeasureFirst, 12, 13)]
    [InlineData(MeasuringStrategy.MeasureFirst, 13, 1)]
    [InlineData(MeasuringStrategy.MeasureFirst, 13, 2)]
    [InlineData(MeasuringStrategy.MeasureFirst, 13, 3)]
    [InlineData(MeasuringStrategy.MeasureFirst, 13, 5)]
    [InlineData(MeasuringStrategy.MeasureFirst, 13, 13)]
    [InlineData(MeasuringStrategy.MeasureFirst, 14, 1)]
    [InlineData(MeasuringStrategy.MeasureFirst, 14, 2)]
    [InlineData(MeasuringStrategy.MeasureFirst, 14, 4)]
    [InlineData(MeasuringStrategy.MeasureFirst, 14, 13)]
    [InlineData(MeasuringStrategy.MeasureVisible, 12, 1)]
    [InlineData(MeasuringStrategy.MeasureVisible, 12, 4)]
    [InlineData(MeasuringStrategy.MeasureVisible, 12, 13)]
    [InlineData(MeasuringStrategy.MeasureVisible, 13, 1)]
    [InlineData(MeasuringStrategy.MeasureVisible, 13, 2)]
    [InlineData(MeasuringStrategy.MeasureVisible, 13, 5)]
    [InlineData(MeasuringStrategy.MeasureVisible, 13, 13)]
    [InlineData(MeasuringStrategy.MeasureVisible, 14, 1)]
    [InlineData(MeasuringStrategy.MeasureVisible, 14, 4)]
    [InlineData(MeasuringStrategy.MeasureVisible, 14, 13)]
    public void TailAppend_FillsThePartialRow_KeepsEveryCell_RebindsNothingOld(MeasuringStrategy strategy, int initial, int add)
    {
        lock (Cell.Bound) Cell.Bound.Clear();
        var (host, grid, items) = Scene(strategy, initial);
        using (host)
        {
            var before = grid.Cells.ToDictionary(c => c.ControlIndex, c => c.Destination);
            int raised = 0;
            grid.ItemsSourceChangesApplied += (s, e) => raised++;
            lock (Cell.Bound) Cell.Bound.Clear();

            items.AddRange(Items(initial, add));
            Settle(host, grid, initial + add);

            var cells = grid.Cells;
            Assert.Equal(initial + add, cells.Count);

            // the grid of the first cell: column lefts from the first row, rows every RowHeight + Spacing
            var first = cells[0].Destination;
            var colLefts = cells.Take(Split).Select(c => c.Destination.Left).ToArray();
            foreach (var cell in cells)
            {
                int i = cell.ControlIndex, col = i % Split, row = i / Split;
                Assert.True(Math.Abs(cell.Destination.Left - colLefts[col]) <= 1,
                    $"item {i}: left {cell.Destination.Left}, column {col} is at {colLefts[col]}");
                var top = first.Top + row * (RowHeight + Spacing);
                Assert.True(Math.Abs(cell.Destination.Top - top) <= 1,
                    $"item {i}: top {cell.Destination.Top}, row {row} is at {top}");
            }

            // nothing that was there moved
            foreach (var (index, rect) in before)
                Assert.Equal(rect, cells.First(c => c.ControlIndex == index).Destination);

            // no old item was bound again (the full path rebinds every visible cell)
            List<int> bound;
            lock (Cell.Bound) bound = Cell.Bound.ToList();
            Assert.False(bound.Any(item => item < initial), "old items bound again: " + string.Join(",", bound));

            Assert.Equal(1, raised);
        }
    }

    /// <summary>A short last row that stretches (DynamicColumns) changes old cells when it fills: full path, laid out right.</summary>
    [Fact]
    public void TailAppend_DynamicColumns_StillLaysOutEveryRow()
    {
        var (host, grid, items) = Scene(MeasuringStrategy.MeasureFirst, 13);
        using (host)
        {
            grid.DynamicColumns = true;
            Settle(host, grid, 13);
            items.AddRange(Items(13, 4));
            Settle(host, grid, 17);

            var cells = grid.Cells;
            Assert.Equal(17, cells.Count);
            var colLefts = cells.Take(Split).Select(c => c.Destination.Left).ToArray();
            for (int i = 0; i < 15; i++)
                Assert.True(Math.Abs(cells[i].Destination.Left - colLefts[i % Split]) <= 1, $"item {i}");
        }
    }

    sealed class UnevenCell : SkiaShape
    {
        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is Item item)
                HeightRequest = item.Id % 7 == 0 ? 180 : 100; // some rows have one taller cell
        }
    }

    /// <summary>
    /// MeasureVisible measures a Split grid in background batches that end mid-row (10 items, 3 columns). Each
    /// batch used to start a new structure row, so later batches looked rows up at the wrong place: cells a row too
    /// high, overlapping the row above. Every row must start under the tallest cell of the row before it.
    /// </summary>
    [Theory]
    [InlineData(40, false)]
    [InlineData(61, false)]
    [InlineData(100, false)]
    [InlineData(61, true)]
    [InlineData(100, true)]
    public void MeasureVisible_BackgroundBatches_KeepEveryRow(int count, bool uneven)
    {
        var host = new HeadlessCanvasHost(402, 700, scale: 1f, background: Colors.Black);
        var items = new ObservableRangeCollection<Item>();
        items.AddRange(Items(0, count));
        var grid = new Grid
        {
            Type = LayoutType.Column,
            Split = Split,
            Spacing = Spacing,
            HorizontalOptions = LayoutOptions.Fill,
            ItemsSource = items,
            RecyclingTemplate = RecyclingTemplate.Enabled,
            MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
            ItemTemplate = new DataTemplate(() => uneven
                ? new UnevenCell { Type = ShapeType.Rectangle, HorizontalOptions = LayoutOptions.Fill }
                : new Cell { Type = ShapeType.Rectangle, HorizontalOptions = LayoutOptions.Fill, HeightRequest = RowHeight }),
        };
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = grid,
        };
        using (host)
        {
            Settle(host, grid, count);
            var cells = grid.Cells;
            Assert.Equal(count, cells.Count);

            float previousBottom = float.NaN;
            foreach (var row in cells.GroupBy(c => c.ControlIndex / Split).OrderBy(g => g.Key))
            {
                var top = row.First().Destination.Top;
                Assert.All(row, c => Assert.True(Math.Abs(c.Destination.Top - top) <= 1,
                    $"item {c.ControlIndex}: top {c.Destination.Top}, its row starts at {top}"));
                if (!float.IsNaN(previousBottom))
                    Assert.True(Math.Abs(top - (previousBottom + Spacing)) <= 1,
                        $"row {row.Key} starts at {top}, the row above ends at {previousBottom}");
                previousBottom = row.Max(c => c.Destination.Bottom);
            }
        }
    }
}
