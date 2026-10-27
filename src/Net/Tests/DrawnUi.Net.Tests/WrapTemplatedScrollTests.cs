using System.Windows.Input;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A templated SkiaWrap in a SkiaScroll: LoadMore fires near the end and ScrollToIndex arrives, as for a Column.
/// </summary>
public class WrapTemplatedScrollTests
{
    private readonly ITestOutputHelper _output;
    public WrapTemplatedScrollTests(ITestOutputHelper output) => _output = output;

    private const int ItemHeight = 50;

    private static (HeadlessCanvasHost host, SkiaScroll scroll, SkiaLayout wrap, Func<int> fired) Scene(int split, int items)
    {
        var host = new HeadlessCanvasHost(200, 200, scale: 1f, background: Colors.Black);
        var count = 0;
        SkiaLayout wrap = null;
        var scroll = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Bounces = false,
            LoadMoreOffset = 50,
            LoadMoreCommand = new Command(() => count++),
            Content = new SkiaLayout
            {
                Type = LayoutType.Wrap,
                Split = split,
                Spacing = 0,
                HorizontalOptions = LayoutOptions.Fill,
                RecyclingTemplate = RecyclingTemplate.Enabled,
                ItemsSource = Enumerable.Range(0, items).ToList(),
                ItemTemplate = new DataTemplate(() => new SkiaShape
                {
                    BackgroundColor = Colors.DarkSlateBlue,
                    HorizontalOptions = LayoutOptions.Fill,
                    HeightRequest = ItemHeight,
                }),
            }.Assign(out wrap)
        };
        host.Canvas.Content = scroll;
        host.AdvanceFrames(8);
        return (host, scroll, wrap, () => count);
    }

    [Fact]
    public void LoadMore_FiresAtTheEnd()
    {
        // 20 items, 2 per row, 50pt rows: 500pt of content in a 200pt viewport
        var (host, scroll, wrap, fired) = Scene(split: 2, items: 20);
        using (host)
        {
            scroll.ViewportOffsetY = -300; // the end
            host.AdvanceFrames(10);
            _output.WriteLine($"content {wrap.MeasuredSize.Units.Height}, LastMeasuredIndex {wrap.LastMeasuredIndex}, fired {fired()}");
            Assert.True(fired() > 0, "LoadMore never fired at the end of a templated Wrap");
        }
    }

    [Fact]
    public void ScrollToIndex_Arrives()
    {
        var (host, scroll, wrap, _) = Scene(split: 2, items: 20);
        using (host)
        {
            scroll.ScrollToIndex(12, false, RelativePositionType.Start); // row 6: y 300, clamped to the 300 range
            host.AdvanceFrames(20);
            _output.WriteLine($"offset {scroll.ViewportOffsetY}, LastMeasuredIndex {wrap.LastMeasuredIndex}");
            Assert.True(scroll.ViewportOffsetY < -250, $"ScrollToIndex did not arrive: offset {scroll.ViewportOffsetY}");
        }
    }
}
