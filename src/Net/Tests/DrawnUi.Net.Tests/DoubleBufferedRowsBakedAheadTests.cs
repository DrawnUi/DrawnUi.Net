using System.Collections.ObjectModel;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Recycled double-buffered rows are baked by the preparation worker ahead of the viewport, so a list scrolled at a
/// steady speed shows its rows with their bitmap, not placeholders, also across the built-in window's slides.
/// </summary>
public class DoubleBufferedRowsBakedAheadTests
{
    private sealed class Row : SkiaDynamicDrawnCell
    {
        public static int Placeholders, Drawn;
        private readonly SkiaLabel _t = new() { FontSize = 15 };

        public Row(bool uneven)
        {
            HorizontalOptions = LayoutOptions.Fill;
            UseCache = SkiaCacheType.ImageDoubleBuffered;
            if (!uneven)
                HeightRequest = 80;
            Children = new List<SkiaControl> { _t };
        }

        public override void DrawPlaceholder(DrawingContext context)
        {
            Interlocked.Increment(ref Placeholders);
            base.DrawPlaceholder(context);
        }

        public override void Render(DrawingContext context)
        {
            Interlocked.Increment(ref Drawn);
            base.Render(context);
        }

        protected override void SetContent(object ctx)
        {
            base.SetContent(ctx);
            var i = (int)ctx;
            _t.Text = string.Concat(Enumerable.Repeat("word ", 4 + (i % 5) * 12));
        }
    }

    [Theory]
    [InlineData(MeasuringStrategy.MeasureFirst)]
    [InlineData(MeasuringStrategy.MeasureVisible)]
    public void RowsArriveBaked(MeasuringStrategy strategy)
    {
        var uneven = strategy == MeasuringStrategy.MeasureVisible;
        var host = new HeadlessCanvasHost(411, 800, 2.625f, Colors.Black);
        var list = new SkiaStack
        {
            Spacing = 0,
            RecyclingTemplate = RecyclingTemplate.Enabled,
            MeasureItemsStrategy = strategy,
            ItemTemplate = new DataTemplate(() => new Row(uneven)),
        };
        var scroll = new SkiaScroll { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, Content = list };
        host.Canvas.Content = scroll;
        list.ItemsSource = new ObservableCollection<int>(Enumerable.Range(0, 10000));
        for (var i = 0; i < 20; i++)
        {
            host.RenderFrame(16);
            Thread.Sleep(10); // background preparation and bakes run on real threads
        }

        Row.Placeholders = 0;
        Row.Drawn = 0;
        for (var f = 0; f < 260; f++)
        {
            scroll.ViewportOffsetY -= 30f; // by steps, like a fling: the window shifts the offset when it slides
            host.RenderFrame(11);
            Thread.Sleep(11);
        }

        Assert.True(Row.Drawn > 0);
        Assert.True(Row.Placeholders * 100 <= Row.Drawn * 2, $"placeholders {Row.Placeholders} of {Row.Drawn} rows drawn");
    }
}
