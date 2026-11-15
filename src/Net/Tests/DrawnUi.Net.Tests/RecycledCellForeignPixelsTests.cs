using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A recycled ImageDoubleBuffered cell rebound to another item never shows the previous item's cached picture while
/// its new one bakes: it shows its placeholder (Aromarket Top 100 grid, 2026-11-15: new cells at a LoadMore showed the
/// pictures the reused views held before for a few frames).
/// </summary>
public class RecycledCellForeignPixelsTests
{
    private readonly ITestOutputHelper _out;
    public RecycledCellForeignPixelsTests(ITestOutputHelper o) { _out = o; }

    private sealed class Item
    {
        public int Index { get; init; }
        public Color Color { get; init; }
    }

    private sealed class Cell : SkiaShape
    {
        public Cell()
        {
            Type = ShapeType.Rectangle;
            HorizontalOptions = LayoutOptions.Fill;
            HeightRequest = 100;
            UseCache = SkiaCacheType.ImageDoubleBuffered;
            // a white mark only the baked picture has (the placeholder is the plain background)
            AddSubView(new SkiaShape { Type = ShapeType.Rectangle, BackgroundColor = Colors.White, WidthRequest = 20, HeightRequest = 20, Margin = new Thickness(10, 40, 0, 0) });
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is Item item)
                BackgroundColor = item.Color;
        }
    }

    private static Color ColorOf(int i) => Color.FromHsla(i / 60f, 0.9f, 0.5f);

    [Fact]
    public void RecycledCell_NeverShowsThePreviousItemsPicture()
    {
        using var host = new HeadlessCanvasHost(300, 400, scale: 1f, background: Colors.Black);
        var items = Enumerable.Range(0, 60).Select(i => new Item { Index = i, Color = ColorOf(i) }).ToList();
        var list = new SkiaLayout
        {
            Type = LayoutType.Column,
            Spacing = 0,
            HorizontalOptions = LayoutOptions.Fill,
            ItemsSource = items,
            RecyclingTemplate = RecyclingTemplate.Enabled,
            MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
            ItemTemplate = new DataTemplate(() => new Cell()),
        };
        var scroll = new SkiaScroll { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, Content = list };
        host.Canvas.Content = scroll;
        host.AdvanceFrames(30, 16);

        var wrong = 0;
        foreach (var offset in new[] { 1000, 2000, 3000, 1500, 500 })
        {
            scroll.ScrollTo(0, -offset, 0, true);
            // the first frames after the jump: the reused views were just rebound, their new pictures still baking
            for (var frame = 0; frame < 3; frame++)
            {
                host.RenderFrame(16);
                using var image = host.Snapshot();
                using var bmp = SKBitmap.FromImage(image);
                for (var y = 50; y < 400; y += 100)
                {
                    var index = (offset + y) / 100;
                    var expected = ColorOf(index).ToSKColor();
                    var px = bmp.GetPixel(150, y);
                    if (px == SKColors.Black)
                        continue; // not drawn yet: not another item's picture
                    var close = Math.Abs(px.Red - expected.Red) < 12 && Math.Abs(px.Green - expected.Green) < 12 && Math.Abs(px.Blue - expected.Blue) < 12;
                    // the placeholder is the cell's own background color: the new item's color, so it passes
                    if (!close)
                    {
                        wrong++;
                        _out.WriteLine($"offset {offset} frame {frame} y {y}: item {index} expected {expected} got {px}");
                    }
                }
            }
        }

        Assert.Equal(0, wrong);

        // the placeholder does not stay: every visible cell gets its own picture back
        host.AdvanceFrames(30, 16);
        using (var settled = host.Snapshot())
        using (var bmp = SKBitmap.FromImage(settled))
        {
            for (var y = 50; y < 400; y += 100)
            {
                var mark = bmp.GetPixel(20, y);
                Assert.True(mark.Red > 240 && mark.Green > 240 && mark.Blue > 240, $"cell at y {y} still shows no picture: {mark}");
            }
        }
    }
}
