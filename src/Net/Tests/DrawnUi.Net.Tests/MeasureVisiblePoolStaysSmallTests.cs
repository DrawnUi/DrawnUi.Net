using System.Collections.ObjectModel;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Scrolling a long MeasureVisible list of uneven rows reuses cells far from the screen instead of creating a new cell
/// for every prepared row: the number of cells stays near what the viewport and its preparation need.
/// </summary>
public class MeasureVisiblePoolStaysSmallTests
{
    private sealed class Row : SkiaDynamicDrawnCell
    {
        [ThreadStatic] public static int Created;
        private readonly SkiaLabel _title = new() { FontSize = 15 }, _bio = new() { FontSize = 12 };

        public Row()
        {
            Interlocked.Increment(ref _all);
            HorizontalOptions = LayoutOptions.Fill;
            Padding = new Thickness(16, 4);
            Children = new List<SkiaControl> { new SkiaStack { Spacing = 3, Children = new List<SkiaControl> { _title, _bio } } };
        }

        public static int _all;

        protected override void SetContent(object ctx)
        {
            base.SetContent(ctx);
            var i = (int)ctx;
            _title.Text = "Item " + i;
            _bio.Text = string.Concat(Enumerable.Repeat("word bio text ", 3 + (i % 6) * 8));
        }
    }

    [Fact]
    public void CellsStopGrowingWhileScrolling()
    {
        var host = new HeadlessCanvasHost(411, 800, 2.625f, Colors.Black);
        var list = new SkiaStack
        {
            Spacing = 0,
            RecyclingTemplate = RecyclingTemplate.Enabled,
            MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
            ItemTemplate = new DataTemplate(() => new Row()),
        };
        var scroll = new SkiaScroll { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, Content = list };
        host.Canvas.Content = scroll;
        list.ItemsSource = new ObservableCollection<int>(Enumerable.Range(0, 10000));
        host.AdvanceFrames(10);

        int atHalf = 0;
        for (var f = 0; f <= 450; f++)
        {
            scroll.ViewportOffsetY = -f * 44f;
            host.RenderFrame(11);
            if (f % 50 == 0)
                Thread.Sleep(30); // let background preparation run
            if (f == 225)
                atHalf = Volatile.Read(ref Row._all);
        }

        var atEnd = Volatile.Read(ref Row._all);
        Assert.True(atEnd - atHalf <= 6, $"cells created: {atHalf} at half way, {atEnd} at the end");
    }
}
