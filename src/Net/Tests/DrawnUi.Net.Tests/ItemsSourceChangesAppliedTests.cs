using DrawnUi.Controls;
using AppoMobi.Specials;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaLayout.ItemsSourceChangesApplied fires after every ItemsSource collection change, once per frame,
/// after the frame that applied it. It used to fire only for a new ItemsSource and the full-rebuild path:
/// structure-preserving changes (MeasureVisible, MeasureFirst Add / Remove / Replace / Move, aligned split
/// grid pages) and the built-in window engaging on grow never raised it, so a LoadMore gate waiting for it
/// stalled (FiltersCamera My Shots at 300 items, Racebox scroll-to-bottom per result).
/// </summary>
public class ItemsSourceChangesAppliedTests
{
    const float RowHeight = 134;
    const float Spacing = 2;

    static (HeadlessCanvasHost host, SkiaScroll scroll, SkiaLayout grid, ObservableRangeCollection<int> items) Scene(
        MeasuringStrategy strategy, int split, int initial)
    {
        var host = new HeadlessCanvasHost(402, 700, scale: 1f, background: Colors.Black);
        var items = new ObservableRangeCollection<int>();
        items.AddRange(Enumerable.Range(0, initial));

        var grid = new SkiaLayout
        {
            Type = LayoutType.Column,
            Split = split,
            Spacing = Spacing,
            HorizontalOptions = LayoutOptions.Fill,
            ItemsSource = items,
            RecyclingTemplate = RecyclingTemplate.Enabled,
            MeasureItemsStrategy = strategy,
            ItemTemplate = new DataTemplate(() => new SkiaShape
            {
                Type = ShapeType.Rectangle,
                BackgroundColor = Colors.DarkSlateBlue,
                HorizontalOptions = LayoutOptions.Fill,
                HeightRequest = RowHeight,
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
        Settle(host);
        return (host, scroll, grid, items);
    }

    /// <summary>Renders the way an app does: frames while one is asked for, bounded.</summary>
    static void Settle(HeadlessCanvasHost host, int max = 60)
    {
        host.AdvanceFrames(3);
        for (int i = 0; i < max && host.NeedsFrame; i++)
            host.RenderFrame(16);
        host.AdvanceFrames(3);
    }

    [Theory]
    [InlineData(MeasuringStrategy.MeasureFirst, 1)]
    [InlineData(MeasuringStrategy.MeasureVisible, 1)]
    [InlineData(MeasuringStrategy.MeasureFirst, 3)]
    [InlineData(MeasuringStrategy.MeasureVisible, 3)]
    public void StructurePreservingAdd_RaisesOnce(MeasuringStrategy strategy, int split)
    {
        var (host, _, grid, items) = Scene(strategy, split, 12);
        using (host)
        {
            int raised = 0;
            grid.ItemsSourceChangesApplied += (s, e) => raised++;

            items.AddRange(Enumerable.Range(12, 12)); // aligned for split 3
            Settle(host);

            Assert.Equal(1, raised);
        }
    }

    [Fact]
    public void MeasureFirst_RemoveReplaceMove_EachRaisesOnce()
    {
        var (host, _, grid, items) = Scene(MeasuringStrategy.MeasureFirst, 1, 12);
        using (host)
        {
            int raised = 0;
            grid.ItemsSourceChangesApplied += (s, e) => raised++;

            items.RemoveAt(3);
            Settle(host);
            Assert.Equal(1, raised);

            items[2] = 100;
            Settle(host);
            Assert.Equal(2, raised);

            items.Move(0, 5);
            Settle(host);
            Assert.Equal(3, raised);
        }
    }

    /// <summary>
    /// The per-layout key is made from the Uid; it used to be negative for half the Uids, which SafeAction reads
    /// as "no key", so those layouts raised once per change. 8 layouts: all must merge (KeyedActionTests pins the key).
    /// </summary>
    [Fact]
    public void SeveralChangesInOneFrame_RaiseOnce()
    {
        for (int i = 0; i < 8; i++)
        {
            var (host, _, grid, items) = Scene(MeasuringStrategy.MeasureFirst, 1, 12);
            using (host)
            {
                int raised = 0;
                grid.ItemsSourceChangesApplied += (s, e) => raised++;

                items.Add(12);
                items.Add(13);
                items.Add(14);
                Settle(host);

                Assert.Equal(1, raised);
            }
        }
    }

    /// <summary>
    /// A paged split grid whose handler asks for the next page while the grid is shorter than the viewport
    /// plus the LoadMore distance (FiltersCamera FillViewport). Each raise must see the page it follows
    /// already laid out, else the handler reads a stale height and fetches page after page.
    /// MeasureFirst only: under MeasureVisible appended rows are measured in the background after the frame
    /// that applied the change, so the height grows later (hook MeasurementApplied for that).
    /// </summary>
    [Fact]
    public void AlignedPages_FillViewport_OnePagePerRaise_NoCascade()
    {
        const int page = 12; // 4 rows of 3
        var (host, scroll, grid, items) = Scene(MeasuringStrategy.MeasureFirst, 3, page);
        using (host)
        {
            int raised = 0, appended = 0;
            var heights = new List<double>();
            bool pending = false;

            void FillViewport()
            {
                if (pending || items.Count >= page * 30)
                    return;
                if (grid.MeasuredSize.Units.Height < scroll.Viewport.Units.Height + scroll.LoadMoreOffset)
                {
                    pending = true;
                    appended++;
                    items.AddRange(Enumerable.Range(items.Count, page));
                }
            }

            grid.ItemsSourceChangesApplied += (s, e) =>
            {
                raised++;
                heights.Add(grid.MeasuredSize.Units.Height);
                pending = false;
                FillViewport();
            };

            FillViewport();
            Settle(host, 120);

            // 700 viewport + 600 = 1300 pt: 10 rows of 136 needed, pages hold 4 rows -> 3 pages in all
            Assert.Equal(2, appended);
            Assert.Equal(36, items.Count);
            Assert.Equal(appended, raised);
            // every raise saw its page laid out: 8 rows, then 12 rows
            Assert.Equal(new double[] { 8 * (RowHeight + Spacing) - Spacing, 12 * (RowHeight + Spacing) - Spacing },
                heights.Select(h => Math.Round(h, 1)).ToArray());
        }
    }

    /// <summary>
    /// The source crossing SkiaLayout.WindowSourceThreshold during life engages the built-in window, which
    /// consumes the Add that grew it. The event must still come (FiltersCamera My Shots stalled at 300).
    /// </summary>
    [Theory]
    [InlineData(MeasuringStrategy.MeasureFirst, 3, 10)]
    [InlineData(MeasuringStrategy.MeasureVisible, 1, 20)]
    public void EngageOnGrow_Raises(MeasuringStrategy strategy, int split, int page)
    {
        var start = SkiaLayout.WindowSourceThreshold - page;
        var (host, _, grid, items) = Scene(strategy, split, start);
        using (host)
        {
            int raised = 0;
            grid.ItemsSourceChangesApplied += (s, e) => raised++;

            items.AddRange(Enumerable.Range(start, page));
            Settle(host);

            Assert.Equal(1, raised);
        }
    }

    /// <summary>A page that is not row-aligned takes the full-rebuild path, which always raised: pinned.</summary>
    [Fact]
    public void UnalignedSplitPage_FullRebuild_RaisesOnce()
    {
        var (host, _, grid, items) = Scene(MeasuringStrategy.MeasureFirst, 3, 10);
        using (host)
        {
            int raised = 0;
            grid.ItemsSourceChangesApplied += (s, e) => raised++;

            items.AddRange(Enumerable.Range(10, 10));
            Settle(host);

            Assert.Equal(1, raised);
        }
    }

    /// <summary>
    /// SkiaSpinner and SkiaWheelPicker hand their ItemsSource to an inner wheel and skip the base handlers,
    /// so the event never came for them: a new ItemsSource and a collection change each raise it once.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pickers_RaiseOnNewSourceAndOnChange(bool wheelPicker)
    {
        var host = new HeadlessCanvasHost(402, 700, scale: 1f, background: Colors.Black);
        var items = new System.Collections.ObjectModel.ObservableCollection<string> { "One", "Two", "Three", "Four" };
        SkiaLayout picker = wheelPicker
            ? new SkiaWheelPicker { WidthRequest = 200, HeightRequest = 200 }
            : new SkiaSpinner { WidthRequest = 300, HeightRequest = 300 };
        int raised = 0;
        picker.ItemsSourceChangesApplied += (s, e) => raised++;
        host.Canvas.Content = picker;
        using (host)
        {
            Settle(host);
            picker.ItemsSource = items;
            Settle(host);
            Assert.Equal(1, raised);

            items.Add("Five");
            Settle(host);
            Assert.Equal(2, raised);
        }
    }
}
