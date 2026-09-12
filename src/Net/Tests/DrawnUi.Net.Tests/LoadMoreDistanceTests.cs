using System.Drawing;
using System.Windows.Input;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaScroll.LoadMoreOffset is a distance in POINTS from the end of the content. It used to be
/// multiplied by the rendering scale before being compared with offsets in points, so on a 3x
/// screen the trigger zone was three times bigger than asked and, once bigger than the scroll
/// range, LoadMore fired at any position (a top overscroll included) as soon as it was re-armed.
/// </summary>
public class LoadMoreDistanceTests
{
    private readonly ITestOutputHelper _output;

    public LoadMoreDistanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const int ItemHeight = 50;

    private static (HeadlessCanvasHost host, SkiaScroll scroll, Func<int> fired) Scene(float scale, int items, float loadMoreOffset)
    {
        // 100 x 200 POINTS of viewport whatever the scale
        var host = new HeadlessCanvasHost((int)(100 * scale), (int)(200 * scale), scale: scale, background: Colors.Black);

        var count = 0;
        var scroll = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Bounces = false,
            LoadMoreOffset = loadMoreOffset,
            LoadMoreCommand = new Command(() => count++),
            Content = new SkiaLayout
            {
                Type = LayoutType.Column,
                Spacing = 0,
                HorizontalOptions = LayoutOptions.Fill,
                RecyclingTemplate = RecyclingTemplate.Enabled,
                MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
                ItemsSource = Enumerable.Range(0, items).ToList(),
                ItemTemplate = new DataTemplate(() => new SkiaShape
                {
                    Type = ShapeType.Rectangle,
                    BackgroundColor = Colors.DarkSlateBlue,
                    HorizontalOptions = LayoutOptions.Fill,
                    HeightRequest = ItemHeight,
                }),
            }
        };

        host.Canvas.Content = scroll;
        host.AdvanceFrames(8);
        return (host, scroll, () => count);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    [InlineData(3f)]
    public void LoadMore_FiresOnlyWithinTheDistanceInPoints(float scale)
    {
        // 12 x 50 = 600pt of content in a 200pt viewport: scroll range 400pt.
        // 150pt distance: the trigger zone is the last 150pt of travel, i.e. offsets below -250.
        // Scaled by 3 the zone would be 450pt, wider than the range, and fire at the top.
        var (host, scroll, fired) = Scene(scale, items: 12, loadMoreOffset: 150);

        host.AdvanceFrames(10);
        _output.WriteLine($"scale {scale}: at top offset {scroll.InternalViewportOffset.Units.Y:0.#}, fired {fired()}");
        Assert.Equal(0, fired());

        // 100pt down: still 300pt from the end, outside the zone
        scroll.ViewportOffsetY = -100;
        host.AdvanceFrames(10);
        _output.WriteLine($"scale {scale}: at -100 fired {fired()}");
        Assert.Equal(0, fired());

        // 300pt down: 100pt from the end, inside the zone
        scroll.ViewportOffsetY = -300;
        host.AdvanceFrames(10);
        _output.WriteLine($"scale {scale}: at -300 fired {fired()}");
        Assert.Equal(1, fired());
    }
}
