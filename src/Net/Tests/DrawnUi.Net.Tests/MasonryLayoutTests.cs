using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// LayoutType.Masonry: Split columns, every item at the bottom of the shortest column, children or ItemsSource.
/// </summary>
public class MasonryLayoutTests
{
    private readonly ITestOutputHelper _output;
    public MasonryLayoutTests(ITestOutputHelper output) => _output = output;

    // Wrap rows take the tallest item of each row: (100,20) (20,100) (100,20) = 300.
    // Masonry: col0 100, col1 20 -> item2 col1 (20..40) -> item3 col1 (40..140) -> item4 col0 (100..200)
    // -> item5 col1 (140..160): 200 tall.
    private static readonly int[] Heights = { 100, 20, 20, 100, 100, 20 };

    private class HeightCell : SkiaShape
    {
        public HeightCell()
        {
            BackgroundColor = Colors.DarkSlateBlue;
            HorizontalOptions = LayoutOptions.Fill;
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is int h)
                HeightRequest = h;
        }
    }

    private static SkiaLayout Children(LayoutType type) => new()
    {
        Type = type,
        Split = 2,
        Spacing = 0,
        HorizontalOptions = LayoutOptions.Fill,
        Children = Heights.Select(h => (SkiaControl)new SkiaShape
        {
            BackgroundColor = Colors.DarkSlateBlue,
            HorizontalOptions = LayoutOptions.Fill,
            HeightRequest = h,
        }).ToList()
    };

    [Fact]
    public void Children_FillTheShortestColumn()
    {
        using var host = new HeadlessCanvasHost(200, 400, scale: 1f, background: Colors.Black);
        SkiaLayout wrap = null, masonry = null;
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                Children(LayoutType.Wrap).Assign(out wrap),
                Children(LayoutType.Masonry).Assign(out masonry),
            }
        };
        host.AdvanceFrames(6);

        _output.WriteLine($"wrap {wrap.MeasuredSize.Units.Height}, masonry {masonry.MeasuredSize.Units.Height}");
        Assert.Equal(300, wrap.MeasuredSize.Units.Height, 1);
        Assert.Equal(200, masonry.MeasuredSize.Units.Height, 1);

        var expected = new (float left, float top)[] { (0, 0), (100, 0), (100, 20), (100, 40), (0, 100), (100, 140) };
        var views = masonry.Views;
        for (var i = 0; i < expected.Length; i++)
        {
            var r = views[i].DrawingRect;
            _output.WriteLine($"{i}: {r}");
            Assert.Equal(expected[i].left, r.Left - masonry.DrawingRect.Left, 1);
            Assert.Equal(expected[i].top, r.Top - masonry.DrawingRect.Top, 1);
            Assert.Equal(100, r.Width, 1);
            Assert.Equal(Heights[i], r.Height, 1);
        }
    }

    private static (HeadlessCanvasHost host, SkiaScroll scroll, SkiaLayout masonry, Func<int> fired, Func<int> tapped)
        Templated(int items)
    {
        var host = new HeadlessCanvasHost(200, 200, scale: 1f, background: Colors.Black);
        var count = 0;
        var tap = -1;
        SkiaLayout masonry = null;
        var scroll = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Bounces = false,
            LoadMoreOffset = 50,
            LoadMoreCommand = new Command(() => count++),
            Content = new SkiaLayout
            {
                Type = LayoutType.Masonry,
                Split = 2,
                Spacing = 0,
                HorizontalOptions = LayoutOptions.Fill,
                RecyclingTemplate = RecyclingTemplate.Enabled,
                ItemsSource = Enumerable.Range(0, items).Select(i => Heights[i % Heights.Length]).ToList(),
                ItemTemplate = new DataTemplate(() => new HeightCell().OnTapped(me => tap = me.ContextIndex)),
            }.Assign(out masonry)
        };
        host.Canvas.Content = scroll;
        host.AdvanceFrames(8);
        return (host, scroll, masonry, () => count, () => tap);
    }

    /// <summary>
    /// Reference placement: two 100 pt columns, each item to the shortest one (the first on a tie).
    /// </summary>
    private static (float left, float top)[] Expected(int items, out float height)
    {
        var bottoms = new float[2];
        var result = new (float, float)[items];
        for (var i = 0; i < items; i++)
        {
            var col = bottoms[1] < bottoms[0] ? 1 : 0;
            result[i] = (col * 100, bottoms[col]);
            bottoms[col] += Heights[i % Heights.Length];
        }
        height = bottoms.Max();
        return result;
    }

    [Fact]
    public void Templated_SameLayoutAsChildren_DrawsCells()
    {
        var (host, scroll, masonry, _, _) = Templated(30);
        using (host)
        {
            var expected = Expected(30, out var height);
            _output.WriteLine($"content {masonry.MeasuredSize.Units.Height}, LastMeasuredIndex {masonry.LastMeasuredIndex}");
            Assert.Equal(height, masonry.MeasuredSize.Units.Height, 1);
            Assert.Equal(29, masonry.LastMeasuredIndex);

            var structure = masonry.GetStackStructure();
            for (var i = 0; i < 30; i++)
            {
                var cell = structure.GetForIndex(i);
                Assert.Equal(expected[i].left, cell.Destination.Left, 1);
                Assert.Equal(expected[i].top, cell.Destination.Top, 1);
                Assert.Equal(Heights[i % Heights.Length], cell.Destination.Height, 1);
            }

            Assert.True(host.NonBackgroundFraction(Colors.Black) > 0.9, "cells were not drawn");
        }
    }

    [Fact]
    public void Templated_LoadMore_FiresAtTheEnd()
    {
        var (host, scroll, masonry, fired, _) = Templated(30);
        using (host)
        {
            scroll.ViewportOffsetY = -720; // the end: 920 - 200
            host.AdvanceFrames(10);
            _output.WriteLine($"fired {fired()}");
            Assert.True(fired() > 0, "LoadMore never fired at the end of a templated Masonry");
        }
    }

    [Fact]
    public void Templated_ScrollToIndex_Arrives()
    {
        var (host, scroll, masonry, _, _) = Templated(30);
        using (host)
        {
            var top = Expected(30, out _)[15].top; // 400, a right column item
            scroll.ScrollToIndex(15, false, RelativePositionType.Start);
            host.AdvanceFrames(20);
            _output.WriteLine($"offset {scroll.ViewportOffsetY}");
            Assert.Equal(-top, scroll.ViewportOffsetY, 1);
        }
    }

    [Fact]
    public void Templated_TapHitsTheCellUnderTheFinger()
    {
        var (host, scroll, masonry, _, tapped) = Templated(30);
        using (host)
        {
            var robot = new GestureRobot(host);
            robot.Tap(150, 90); // right column, y 40..140 = item 3
            host.AdvanceFrames(6);
            Assert.Equal(3, tapped());

            robot.Tap(50, 150); // left column, y 100..200 = item 4
            host.AdvanceFrames(6);
            Assert.Equal(4, tapped());
        }
    }

    /// <summary>
    /// Reference placement for any heights: two 100 pt columns, each item to the shortest one (the first on a tie).
    /// </summary>
    private static (float left, float top)[] Expected(IReadOnlyList<int> heights, out float height)
    {
        var bottoms = new float[2];
        var result = new (float, float)[heights.Count];
        for (var i = 0; i < heights.Count; i++)
        {
            var col = bottoms[1] < bottoms[0] ? 1 : 0;
            result[i] = (col * 100, bottoms[col]);
            bottoms[col] += heights[i];
        }
        height = bottoms.Max();
        return result;
    }

    [Fact]
    public void Children_ChildGrows_LayoutFollows()
    {
        using var host = new HeadlessCanvasHost(200, 400, scale: 1f, background: Colors.Black);
        SkiaLayout masonry = null;
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children = { Children(LayoutType.Masonry).Assign(out masonry) }
        };
        host.AdvanceFrames(6);

        var heights = Heights.ToArray();
        heights[1] = 80;
        masonry.Views[1].HeightRequest = 80;
        host.AdvanceFrames(6);

        var expected = Expected(heights, out var height);
        _output.WriteLine($"masonry {masonry.MeasuredSize.Units.Height}, expected {height}");
        Assert.Equal(height, masonry.MeasuredSize.Units.Height, 1);
        for (var i = 0; i < heights.Length; i++)
        {
            var r = masonry.Views[i].DrawingRect;
            Assert.Equal(expected[i].left, r.Left - masonry.DrawingRect.Left, 1);
            Assert.Equal(expected[i].top, r.Top - masonry.DrawingRect.Top, 1);
            Assert.Equal(heights[i], r.Height, 1);
        }
    }

    private class Item : INotifyPropertyChanged
    {
        private int _height;
        public int Height
        {
            get => _height;
            set { _height = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Height))); }
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>A cell whose height follows its item, as a photo card does when its image arrives.</summary>
    private class GrowingCell : SkiaShape
    {
        private Item _item;

        public GrowingCell()
        {
            BackgroundColor = Colors.DarkSlateBlue;
            HorizontalOptions = LayoutOptions.Fill;
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (_item != null)
                _item.PropertyChanged -= OnItemChanged;
            _item = BindingContext as Item;
            if (_item != null)
            {
                _item.PropertyChanged += OnItemChanged;
                HeightRequest = _item.Height;
            }
        }

        private void OnItemChanged(object sender, PropertyChangedEventArgs e) => HeightRequest = _item.Height;
    }

    [Fact]
    public void Templated_CellGrows_ItemsArePlacedAgain()
    {
        using var host = new HeadlessCanvasHost(200, 400, scale: 1f, background: Colors.Black);
        var items = Enumerable.Range(0, 12).Select(i => new Item { Height = Heights[i % Heights.Length] }).ToList();
        SkiaLayout masonry = null;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout
            {
                Type = LayoutType.Masonry,
                Split = 2,
                Spacing = 0,
                HorizontalOptions = LayoutOptions.Fill,
                RecyclingTemplate = RecyclingTemplate.Enabled,
                ItemsSource = items,
                ItemTemplate = new DataTemplate(() => new GrowingCell()),
            }.Assign(out masonry)
        };
        host.AdvanceFrames(8);

        var structure = masonry.GetStackStructure();
        var before = Enumerable.Range(0, 12).Select(i => structure.GetForIndex(i).Destination).ToArray();
        var contentBefore = masonry.MeasuredSize.Units.Height;

        items[1].Height = 80; // right column, 20 -> 80
        for (var frame = 0; frame < 8; frame++)
        {
            host.RenderFrame(16);
            // no two drawn cells of one column overlap, on any frame
            var cells = masonry.GetStackStructure().GetChildren().Where(c => c.WasMeasured).ToList();
            foreach (var a in cells)
            foreach (var b in cells)
                if (a.ControlIndex < b.ControlIndex && a.Column == b.Column)
                    Assert.True(a.Destination.Bottom <= b.Destination.Top + 0.5f,
                        $"frame {frame}: cell {a.ControlIndex} {a.Destination} overlaps {b.ControlIndex} {b.Destination}");
        }

        // the same placement a fresh layout makes with the new heights
        var heights = items.Select(i => i.Height).ToList();
        var expected = Expected(heights, out var height);
        structure = masonry.GetStackStructure();
        for (var i = 0; i < 12; i++)
        {
            var c = structure.GetForIndex(i);
            _output.WriteLine($"{i}: col {c.Column} {before[i]} -> {c.Destination}");
            Assert.Equal(expected[i].left, c.Destination.Left, 1);
            Assert.Equal(expected[i].top, c.Destination.Top, 1);
            Assert.Equal(heights[i], c.Destination.Height, 1);
        }
        _output.WriteLine($"content {contentBefore} -> {masonry.MeasuredSize.Units.Height}");
        Assert.Equal(height, masonry.MeasuredSize.Units.Height, 1);
    }

    [Fact]
    public void Templated_NewItemsSourceOfSameCount_LaysOutFresh()
    {
        var (host, scroll, masonry, _, _) = Templated(30);
        using (host)
        {
            var reversed = Enumerable.Range(0, 30).Select(i => Heights[(29 - i) % Heights.Length]).ToList();
            masonry.ItemsSource = reversed;
            host.AdvanceFrames(8);

            var expected = Expected(reversed, out var height);
            Assert.Equal(height, masonry.MeasuredSize.Units.Height, 1);
            var structure = masonry.GetStackStructure();
            for (var i = 0; i < 30; i++)
            {
                var c = structure.GetForIndex(i);
                Assert.Equal(expected[i].left, c.Destination.Left, 1);
                Assert.Equal(expected[i].top, c.Destination.Top, 1);
            }
        }
    }

    private static (HeadlessCanvasHost host, SkiaScroll scroll, SkiaLayout masonry, ObservableCollection<int> items, Func<int> fired)
        Observable(int items, Action<ObservableCollection<int>> loadMore = null)
    {
        var host = new HeadlessCanvasHost(200, 200, scale: 1f, background: Colors.Black);
        var source = new ObservableCollection<int>(Enumerable.Range(0, items).Select(i => Heights[i % Heights.Length]));
        var count = 0;
        SkiaLayout masonry = null;
        var scroll = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Bounces = false,
            LoadMoreOffset = 50,
            LoadMoreCommand = new Command(() => { count++; loadMore?.Invoke(source); }),
            Content = new SkiaLayout
            {
                Type = LayoutType.Masonry,
                Split = 2,
                Spacing = 0,
                HorizontalOptions = LayoutOptions.Fill,
                RecyclingTemplate = RecyclingTemplate.Enabled,
                ItemsSource = source,
                ItemTemplate = new DataTemplate(() => new HeightCell()),
            }.Assign(out masonry)
        };
        host.Canvas.Content = scroll;
        host.AdvanceFrames(8);
        return (host, scroll, masonry, source, () => count);
    }

    private void AssertPlacedAsFresh(SkiaLayout masonry, IReadOnlyList<int> heights)
    {
        var expected = Expected(heights, out var height);
        _output.WriteLine($"content {masonry.MeasuredSize.Units.Height}, expected {height}, LastMeasuredIndex {masonry.LastMeasuredIndex}");
        Assert.Equal(height, masonry.MeasuredSize.Units.Height, 1);
        Assert.Equal(heights.Count - 1, masonry.LastMeasuredIndex);
        var structure = masonry.GetStackStructure();
        Assert.Equal(heights.Count, structure.GetCount());
        for (var i = 0; i < heights.Count; i++)
        {
            var c = structure.GetForIndex(i);
            Assert.Equal(expected[i].left, c.Destination.Left, 1);
            Assert.Equal(expected[i].top, c.Destination.Top, 1);
            Assert.Equal(heights[i], c.Destination.Height, 1);
        }
    }

    [Fact]
    public void Templated_Append_PlacesNewItemsUnderTheColumns()
    {
        var (host, scroll, masonry, items, _) = Observable(30);
        using (host)
        {
            foreach (var h in new[] { 140, 20, 60, 90, 30, 110, 50, 20, 80, 40, 130, 70 })
                items.Add(h); // one change per item, as ObservableCollection raises them
            host.AdvanceFrames(8);
            AssertPlacedAsFresh(masonry, items);
            Assert.True(host.NonBackgroundFraction(Colors.Black) > 0.9, "cells were not drawn");
        }
    }

    [Fact]
    public void Templated_InsertAndRemove_LayOutFresh()
    {
        var (host, scroll, masonry, items, _) = Observable(30);
        using (host)
        {
            items.Insert(5, 140);
            host.AdvanceFrames(8);
            AssertPlacedAsFresh(masonry, items);

            items.RemoveAt(0);
            items.Add(90); // a remove and an append before one measure
            host.AdvanceFrames(8);
            AssertPlacedAsFresh(masonry, items);
        }
    }

    [Fact]
    public void Templated_LoadMorePages_KeepTheLayout()
    {
        var page = 0;
        var (host, scroll, masonry, items, fired) = Observable(30, source =>
        {
            page++;
            for (var i = 0; i < 10; i++)
                source.Add(Heights[(i + page) % Heights.Length] + 10 * page);
        });
        using (host)
        {
            for (var round = 0; round < 3; round++)
            {
                scroll.ViewportOffsetY = -(masonry.MeasuredSize.Units.Height - 200); // the end
                host.AdvanceFrames(12);
            }
            _output.WriteLine($"fired {fired()}, items {items.Count}");
            Assert.True(fired() >= 3, $"LoadMore fired {fired()} times");
            AssertPlacedAsFresh(masonry, items);
        }
    }

    [Fact]
    public void Templated_BigSource_LaysOutEveryItem()
    {
        // past SkiaLayout.WindowSourceThreshold a Column works on a slice; a masonry needs every item above
        var count = SkiaLayout.WindowSourceThreshold + 100;
        var (host, scroll, masonry, items, _) = Observable(count);
        using (host)
        {
            Assert.Null(masonry.ItemsWindow);
            AssertPlacedAsFresh(masonry, items);
        }
    }

    [Fact]
    public void Templated_VisibleCellGrowsAgainAndAgain_ItemsArePlacedAgain()
    {
        // a recycled view stops telling its layout after a few invalidations (IsParentIndependent): the masonry
        // must still see it grow, from its draw pass
        using var host = new HeadlessCanvasHost(200, 400, scale: 1f, background: Colors.Black);
        var items = Enumerable.Range(0, 12).Select(i => new Item { Height = Heights[i % Heights.Length] }).ToList();
        SkiaLayout masonry = null;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout
            {
                Type = LayoutType.Masonry,
                Split = 2,
                Spacing = 0,
                HorizontalOptions = LayoutOptions.Fill,
                RecyclingTemplate = RecyclingTemplate.Enabled,
                ItemsSource = items,
                ItemTemplate = new DataTemplate(() => new GrowingCell()),
            }.Assign(out masonry)
        };
        host.AdvanceFrames(8);

        for (var round = 0; round < 6; round++)
        {
            items[2].Height += 20; // on screen, right column
            host.AdvanceFrames(4);

            var heights = items.Select(i => i.Height).ToList();
            var expected = Expected(heights, out var height);
            var structure = masonry.GetStackStructure();
            for (var i = 0; i < items.Count; i++)
            {
                var c = structure.GetForIndex(i);
                Assert.True(Math.Abs(expected[i].top - c.Destination.Top) < 1 && Math.Abs(heights[i] - c.Destination.Height) < 1,
                    $"round {round}: #{i} at {c.Destination}, expected top {expected[i].top} height {heights[i]}");
            }
            Assert.Equal(height, masonry.MeasuredSize.Units.Height, 1);
        }
    }

    // Padding 10,20,30,40 and Spacing 4 in a 200 wide layout: columns of (200 - 10 - 30 - 4) / 2 = 78 at x 10 and 92,
    // items start 20 down and 4 apart. col0: #0 20..120, #4 124..224; col1: #1 20..40, #2 44..64, #3 68..168, #5 172..192.
    // Height: tallest column 204 + padding 20 + 40 = 264.
    private static readonly (float left, float top)[] PaddedExpected =
        { (10, 20), (92, 20), (92, 44), (92, 68), (10, 124), (92, 172) };

    private static readonly Thickness PaddedPadding = new(10, 20, 30, 40);

    private static void AssertPaddedPixels(HeadlessCanvasHost host, SkiaLayout masonry)
    {
        using var image = host.Snapshot();
        using var bitmap = SkiaSharp.SKBitmap.FromImage(image);
        var ox = masonry.DrawingRect.Left;
        var oy = masonry.DrawingRect.Top;
        bool Filled(float x, float y) => bitmap.GetPixel((int)(ox + x), (int)(oy + y)).Blue > 0x40;
        for (var i = 0; i < PaddedExpected.Length; i++)
        {
            var (left, top) = PaddedExpected[i];
            Assert.True(Filled(left + 39, top + Heights[i] / 2f), $"item {i} not drawn at {left},{top}");
        }
        Assert.False(Filled(5, 60), "left padding is drawn over");
        Assert.False(Filled(40, 10), "top padding is drawn over");
        Assert.False(Filled(90, 60), "spacing between the columns is drawn over");
        Assert.False(Filled(185, 60), "right padding is drawn over");
        Assert.False(Filled(40, 122), "spacing between items is drawn over");
    }

    [Fact]
    public void Children_PaddingAndSpacing_PlaceItemsInsideThePadding()
    {
        using var host = new HeadlessCanvasHost(200, 400, scale: 1f, background: Colors.Black);
        SkiaLayout masonry = null;
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children = { Children(LayoutType.Masonry).Assign(out masonry) }
        };
        masonry.Padding = PaddedPadding;
        masonry.Spacing = 4;
        host.AdvanceFrames(6);

        _output.WriteLine($"masonry {masonry.MeasuredSize.Units.Height}");
        Assert.Equal(264, masonry.MeasuredSize.Units.Height, 1);
        for (var i = 0; i < PaddedExpected.Length; i++)
        {
            var r = masonry.Views[i].DrawingRect;
            _output.WriteLine($"{i}: {r}");
            Assert.Equal(PaddedExpected[i].left, r.Left - masonry.DrawingRect.Left, 1);
            Assert.Equal(PaddedExpected[i].top, r.Top - masonry.DrawingRect.Top, 1);
            Assert.Equal(78, r.Width, 1);
            Assert.Equal(Heights[i], r.Height, 1);
        }
        AssertPaddedPixels(host, masonry);
    }

    [Fact]
    public void Templated_PaddingAndSpacing_SameAsChildren()
    {
        using var host = new HeadlessCanvasHost(200, 400, scale: 1f, background: Colors.Black);
        SkiaLayout masonry = null;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout
            {
                Type = LayoutType.Masonry,
                Split = 2,
                Spacing = 4,
                Padding = PaddedPadding,
                HorizontalOptions = LayoutOptions.Fill,
                RecyclingTemplate = RecyclingTemplate.Enabled,
                ItemsSource = Heights.ToList(),
                ItemTemplate = new DataTemplate(() => new HeightCell()),
            }.Assign(out masonry)
        };
        host.AdvanceFrames(8);

        _output.WriteLine($"masonry {masonry.MeasuredSize.Units.Height}");
        Assert.Equal(264, masonry.MeasuredSize.Units.Height, 1);
        AssertPaddedPixels(host, masonry);
    }
}
