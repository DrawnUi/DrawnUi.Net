using System.Collections.ObjectModel;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A templated SkiaRow in a horizontal SkiaScroll: every measuring strategy lays its items along X, scrolls with
/// the wheel, follows ItemsSource changes, and reports the items really visible.
/// </summary>
[Collection("Multithreaded")] // background measurement on real threads
public class RowTemplatedTests
{
    private readonly ITestOutputHelper _output;
    public RowTemplatedTests(ITestOutputHelper output) => _output = output;

    private class Cell : SkiaShape
    {
        public Cell()
        {
            VerticalOptions = LayoutOptions.Fill;
            BackgroundColor = Colors.Gray;
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is int width)
                WidthRequest = width;
        }
    }

    private static int Width(int i, MeasuringStrategy strategy)
        => strategy == MeasuringStrategy.MeasureFirst ? 50 : 40 + (i * 37) % 60; // MeasureFirst = one size

    private static (HeadlessCanvasHost host, SkiaScroll scroll, SkiaLayout row) Scene(IList<int> items, MeasuringStrategy strategy)
    {
        var host = new HeadlessCanvasHost(800, 200, scale: 1f, background: Colors.Black);
        SkiaLayout row = null;
        SkiaScroll scroll = null;
        host.Canvas.Content = new SkiaScroll
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout
            {
                Type = LayoutType.Row,
                Spacing = 0,
                MeasureItemsStrategy = strategy,
                VerticalOptions = LayoutOptions.Fill,
                ItemsSource = (System.Collections.IList)items,
                ItemTemplate = new DataTemplate(() => new Cell()),
            }.Assign(out row)
        }.Assign(out scroll);
        Settle(host, row, items.Count);
        return (host, scroll, row);
    }

    /// <summary>Renders until every item is measured (background measurement runs on real threads).</summary>
    private static void Settle(HeadlessCanvasHost host, SkiaLayout row, int count)
    {
        host.AdvanceFrames(2); // the change is applied
        for (var f = 0; f < 300; f++)
        {
            if (row.GetStackStructure().GetCount() == count && row.LastMeasuredIndex == count - 1 && !row.IsBackgroundMeasuring)
                break;
            host.RenderFrame(16);
            if (f % 4 == 3)
                Thread.Sleep(5);
        }
        host.AdvanceFrames(4);
    }

    private void AssertLaidOut(SkiaLayout row, IList<int> items, string step)
    {
        var structure = row.GetStackStructure();
        Assert.Equal(items.Count, structure.GetCount());
        float left = structure.GetForIndex(0).Destination.Left;
        for (var i = 0; i < items.Count; i++)
        {
            var c = structure.GetForIndex(i);
            Assert.True(c != null && Math.Abs(c.Destination.Left - left) < 1 && Math.Abs(c.Destination.Width - items[i]) < 1,
                $"{step}: #{i} at {c?.Destination}, expected left {left} width {items[i]}");
            left += items[i];
        }
        Assert.Equal(left, row.MeasuredSize.Pixels.Width, 1);
    }

    [Theory]
    [InlineData(MeasuringStrategy.MeasureAll)]
    [InlineData(MeasuringStrategy.MeasureFirst)]
    [InlineData(MeasuringStrategy.MeasureVisible)]
    public void LongRow_Wheel_ShowsTheItemsUnderTheViewport(MeasuringStrategy strategy)
    {
        var items = Enumerable.Range(0, 1000).Select(i => Width(i, strategy)).ToList();
        var (host, scroll, row) = Scene(items, strategy);
        using (host)
        {
            Assert.Null(row.ItemsWindow); // a Row never slides an items window
            AssertLaidOut(row, items, "laid out");

            for (var i = 0; i < 60; i++)
            {
                host.Canvas.HandleDesktopWheel(400, 100, -120, 800, 200, horizontal: true);
                host.AdvanceFrames(4);
            }
            host.AdvanceFrames(30);

            // the items really visible: the cells crossing [-offset, -offset + 800)
            var start = (float)-scroll.ViewportOffsetX;
            var structure = row.GetStackStructure();
            var expectedFirst = structure.GetChildren().First(c => c.Destination.Right > start).ControlIndex;
            var expectedLast = structure.GetChildren().Last(c => c.Destination.Left < start + 800).ControlIndex;
            _output.WriteLine($"{strategy}: offset {scroll.ViewportOffsetX:0}, visible {row.FirstVisibleIndex}..{row.LastVisibleIndex}, expected {expectedFirst}..{expectedLast}");
            Assert.True(start > 3000, $"the wheel did not scroll the Row: offset {scroll.ViewportOffsetX}");
            Assert.Equal(expectedFirst, row.FirstVisibleIndex);
            Assert.Equal(expectedLast, row.LastVisibleIndex);
            Assert.True(host.NonBackgroundFraction(Colors.Black) > 0.9, "the Row drew nothing at its offset");
        }
    }

    [Theory]
    [InlineData(MeasuringStrategy.MeasureAll)]
    [InlineData(MeasuringStrategy.MeasureFirst)]
    [InlineData(MeasuringStrategy.MeasureVisible)]
    public void ItemsSourceChanges_LayOutAlongX(MeasuringStrategy strategy)
    {
        var items = new ObservableCollection<int>(Enumerable.Range(0, 40).Select(i => Width(i, strategy)));
        var (host, scroll, row) = Scene(items, strategy);
        using (host)
        {
            AssertLaidOut(row, items, "start");

            for (var i = 0; i < 10; i++)
                items.Add(Width(100 + i, strategy)); // LoadMore page
            Settle(host, row, items.Count);
            AssertLaidOut(row, items, "append");

            items.Insert(5, Width(7, strategy));
            Settle(host, row, items.Count);
            AssertLaidOut(row, items, "insert");

            items.RemoveAt(0);
            items.RemoveAt(10);
            Settle(host, row, items.Count);
            AssertLaidOut(row, items, "remove");

            items.Insert(0, Width(3, strategy));
            Settle(host, row, items.Count);
            AssertLaidOut(row, items, "head insert");
        }
    }
}
