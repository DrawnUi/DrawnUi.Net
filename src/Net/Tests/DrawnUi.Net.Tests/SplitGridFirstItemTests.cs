using System.Drawing;
using AppoMobi.Specials;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A templated Split grid (MeasureFirst, recycling) fed like a paged gallery screen: the
/// ObservableRangeCollection is cleared and the first page appended right after, then again
/// on the next visit. One shot in the library must be ONE cell in the grid, not two
/// (FiltersCamera My Shots after the first photo, 2026-09-12).
/// </summary>
public class SplitGridFirstItemTests
{
    private readonly ITestOutputHelper _output;

    public SplitGridFirstItemTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private sealed class Shot
    {
        public string Id { get; init; } = "";
    }

    private sealed class PeekLayout : SkiaLayout
    {
        public int StructureCells => StackStructure?.GetChildren().Count(c => c != null) ?? -1;
    }

    private static (HeadlessCanvasHost host, SkiaScroll scroll, PeekLayout grid, ObservableRangeCollection<Shot> items) Scene()
    {
        var host = new HeadlessCanvasHost(402, 700, scale: 1f, background: Colors.Black);
        var items = new ObservableRangeCollection<Shot>();

        var grid = new PeekLayout
        {
            Type = LayoutType.Column,
            Split = 3,
            Spacing = 2,
            HorizontalOptions = LayoutOptions.Fill,
            ItemsSource = items,
            RecyclingTemplate = RecyclingTemplate.Enabled,
            MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
            ItemTemplate = new DataTemplate(() => new SkiaShape
            {
                Type = ShapeType.Rectangle,
                BackgroundColor = Colors.DarkSlateBlue,
                HorizontalOptions = LayoutOptions.Fill,
                HeightRequest = 134,
            }),
        };

        var scroll = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            LoadMoreOffset = 600,
            Content = grid,
        };

        host.Canvas.Content = scroll;
        host.AdvanceFrames(6);
        return (host, scroll, grid, items);
    }

    /// <summary>
    /// The gallery screen's real order: the page is put into the collection BEFORE the screen is
    /// attached (Refresh() runs, then PushAsync), and on the first layout the tile height is
    /// computed from the viewport width and the grid's ItemsSource structure is force-rebuilt.
    /// </summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(5, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(5, true)]
    public void FilledBeforeAttach_ThenStructureRebuild_OneCellPerItem(int count, bool rebuildInsideFirstLayout)
    {
        var host = new HeadlessCanvasHost(402, 700, scale: 1f, background: Colors.Black);
        var items = new ObservableRangeCollection<Shot>();
        double cellHeight = 0;
        var cells = new List<SkiaShape>();

        var grid = new PeekLayout
        {
            Type = LayoutType.Column,
            Split = 3,
            Spacing = 2,
            HorizontalOptions = LayoutOptions.Fill,
            ItemsSource = items,
            RecyclingTemplate = RecyclingTemplate.Enabled,
            MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
            ItemTemplate = new DataTemplate(() =>
            {
                var cell = new SkiaShape
                {
                    Type = ShapeType.Rectangle,
                    BackgroundColor = Colors.DarkSlateBlue,
                    HorizontalOptions = LayoutOptions.Fill,
                    HeightRequest = cellHeight > 0 ? cellHeight : 160, // bootstrap value like the app
                    UseCache = SkiaCacheType.ImageDoubleBuffered,          // the app's tile cache
                };
                cells.Add(cell);
                return cell;
            }),
        };

        var scroll = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            LoadMoreOffset = 600,
            Content = grid,
        };

        double column = 0;
        void UpdateCellHeight()
        {
            // the app: tile height from the scroll's width, existing cells follow, structure rebuilt
            column = (scroll.DrawingRect.Width / scroll.RenderingScale - 2 * 2) / 3;
            cellHeight = column * 4 / 3;
            foreach (var cell in cells)
                cell.HeightRequest = cellHeight;
            grid.ApplyItemsSource();
        }

        if (rebuildInsideFirstLayout)
        {
            scroll.LayoutIsReady += (s, e) => UpdateCellHeight();
        }

        // Refresh() before the push: collection changes land while nothing is attached
        var shots = Enumerable.Range(0, count).Select(i => new Shot { Id = $"s{i}" }).ToList();
        items.Clear();
        items.AddRange(shots);

        // the push
        host.Canvas.Content = scroll;
        host.AdvanceFrames(3);

        if (!rebuildInsideFirstLayout)
        {
            UpdateCellHeight();
        }
        host.AdvanceFrames(10);

        // what is actually painted: one tile is column x cellHeight of non-background pixels
        var painted = host.NonBackgroundFraction(Colors.Black);
        var oneTile = column * cellHeight / (402.0 * 700.0);
        _output.WriteLine($"count={count}: cells={grid.StructureCells} children={grid.ChildrenFactory.GetChildrenCount()} contentH={scroll.ContentSize.Units.Height:0} cell={cellHeight:0} painted={painted:0.000} oneTile={oneTile:0.000}");
        Assert.Equal(count, grid.StructureCells);
        Assert.Equal(count, grid.ChildrenFactory.GetChildrenCount());
        Assert.InRange(painted, oneTile * count * 0.9, oneTile * count * 1.1);

        var rows = (int)Math.Ceiling(count / 3.0);
        Assert.Equal(rows * cellHeight + (rows - 1) * 2, scroll.ContentSize.Units.Height, 1.0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void ClearThenAppend_OneCellPerItem(int count)
    {
        var (host, scroll, grid, items) = Scene();
        var shots = Enumerable.Range(0, count).Select(i => new Shot { Id = $"s{i}" }).ToList();

        // first visit: empty grid, then the page
        items.Clear();
        items.AddRange(shots);
        host.AdvanceFrames(10);
        _output.WriteLine($"count={count} first visit: cells={grid.StructureCells} children={grid.ChildrenFactory.GetChildrenCount()} contentH={scroll.ContentSize.Units.Height:0}");
        Assert.Equal(count, grid.StructureCells);
        Assert.Equal(count, grid.ChildrenFactory.GetChildrenCount());

        // back to the camera and here again: same page, re-applied
        items.Clear();
        items.AddRange(shots);
        host.AdvanceFrames(10);
        _output.WriteLine($"count={count} second visit: cells={grid.StructureCells} children={grid.ChildrenFactory.GetChildrenCount()} contentH={scroll.ContentSize.Units.Height:0}");
        Assert.Equal(count, grid.StructureCells);
        Assert.Equal(count, grid.ChildrenFactory.GetChildrenCount());

        // a second shot taken: the page is one longer
        var more = shots.Concat(new[] { new Shot { Id = "new" } }).ToList();
        items.Clear();
        items.AddRange(more);
        host.AdvanceFrames(10);
        _output.WriteLine($"count={count} after a new shot: cells={grid.StructureCells} children={grid.ChildrenFactory.GetChildrenCount()}");
        Assert.Equal(count + 1, grid.StructureCells);

        var rows = (int)Math.Ceiling((count + 1) / 3.0);
        Assert.Equal(rows * 134 + (rows - 1) * 2, scroll.ContentSize.Units.Height, 1.0);
    }
}
