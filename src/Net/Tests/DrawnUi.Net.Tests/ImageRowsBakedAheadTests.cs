using System.Collections.ObjectModel;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Recycled rows with a plain Image cache get their bitmap recorded by the preparation worker ahead of the viewport:
/// the first time a row is drawn for a new item its bitmap is already there, instead of being rasterized in that frame.
/// </summary>
public class ImageRowsBakedAheadTests
{
    private sealed class Row : SkiaDynamicDrawnCell
    {
        public static int FirstDrawReady, FirstDrawCold, ColdButBaked, ColdBakedNull;
        private object _bakedFor;
        protected override void OnCacheCreated() { base.OnCacheCreated(); if (!IsRendering) _bakedFor = BindingContext; }
        private object _lastDrawnContext;
        private readonly SkiaLabel _t = new() { FontSize = 15 };

        public Row(bool uneven)
        {
            HorizontalOptions = LayoutOptions.Fill;
            UseCache = SkiaCacheType.Image;
            if (!uneven)
                HeightRequest = 80;
            Children = new List<SkiaControl> { _t };
        }

        public override void Render(DrawingContext context)
        {
            if (!ReferenceEquals(BindingContext, _lastDrawnContext))
            {
                _lastDrawnContext = BindingContext;
                if (RenderObject != null && !RenderObjectNeedsUpdate)
                    Interlocked.Increment(ref FirstDrawReady);
                else
                {
                    Interlocked.Increment(ref FirstDrawCold);
                    if (ReferenceEquals(_bakedFor, BindingContext)) { Interlocked.Increment(ref ColdButBaked); if (RenderObject == null) Interlocked.Increment(ref ColdBakedNull); }
                }
            }
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
    public void RowsArriveWithTheirBitmap(MeasuringStrategy strategy)
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
            Thread.Sleep(10);
        }

        Row.FirstDrawReady = 0;
        Row.FirstDrawCold = 0; Row.ColdButBaked = 0; Row.ColdBakedNull = 0;
        for (var f = 0; f < 260; f++)
        {
            scroll.ViewportOffsetY -= 30f;
            host.RenderFrame(11);
            Thread.Sleep(11);
        }

        var total = Row.FirstDrawReady + Row.FirstDrawCold;
        Assert.True(total > 0);
        Assert.True(Row.FirstDrawCold * 100 <= total * 5, $"rows drawn without their bitmap: {Row.FirstDrawCold} of {total}, of those baked ahead then lost: {Row.ColdButBaked} (cache gone {Row.ColdBakedNull})");
    }
}
