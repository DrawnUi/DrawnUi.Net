using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// ScrollToIndex across the built-in source window (the hello apps' "Recycled cells" page:
/// 100 000 items, RecyclingTemplate Enabled, MeasureFirst, HOME / MIDDLE / END buttons).
/// A jump to a non-resident item rebases the window around it; the viewport must then land on the
/// requested item, whatever position it starts from.
/// </summary>
public class WindowJumpTests
{
    private readonly ITestOutputHelper _output;

    public WindowJumpTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const int Count = 100_000;
    private const int Middle = Count / 2;

    private static (HeadlessCanvasHost host, SkiaScroll scroll, SkiaLayout feed) Scene(float scale = 1f, int w = 430, int h = 760)
    {
        var host = new HeadlessCanvasHost(w, h, scale: scale, background: Colors.Black);

        var scroll = new SkiaScroll
        {
            Orientation = ScrollOrientation.Vertical,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Margin = new Thickness(0, 36, 0, 0),
            Content = new SkiaStack
            {
                // values 1..Count like CellsPage: item at index i is i + 1
                ItemsSource = Enumerable.Range(1, Count).ToList(),
                ItemTemplate = new DataTemplate(() => new SkiaShape
                {
                    Type = ShapeType.Rectangle,
                    BackgroundColor = Colors.DarkSlateBlue,
                    HorizontalOptions = LayoutOptions.Fill,
                    HeightRequest = 62,
                    UseCache = SkiaCacheType.Image,
                }),
                RecyclingTemplate = RecyclingTemplate.Enabled,
                MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
                Spacing = 8,
                Padding = new Thickness(16, 8),
            }
        };

        host.Canvas.Content = scroll;
        host.AdvanceFrames(8);
        return (host, scroll, (SkiaLayout)scroll.Content);
    }

    private static void Jump(SkiaScroll scroll, int index, bool animate,
        RelativePositionType option = RelativePositionType.Start)
    {
        scroll.ScrollToIndex(Math.Clamp(index, 0, Count - 1), animate, option);
    }

    private static void Settle(HeadlessCanvasHost host, SkiaScroll scroll)
    {
        int quiet = 0;
        for (int i = 0; i < 400 && quiet < 10; i++)
        {
            host.RenderFrame(16);
            quiet = scroll.OrderedScrollToIndexIsSet || scroll.IsScrolling ? 0 : quiet + 1;
        }
    }

    /// <summary>Item (source value) of the cell drawn at the top edge of the viewport.</summary>
    private static int TopItem(SkiaScroll scroll, SkiaLayout feed)
    {
        float top = scroll.DrawingRect.Top;
        SkiaControl best = null;
        foreach (var cell in feed.ChildrenFactory.GetCellsInUse())
        {
            if (cell.DrawingRect.Bottom <= top + 1)
                continue;
            if (best == null || cell.DrawingRect.Top < best.DrawingRect.Top)
                best = cell;
        }

        return best?.BindingContext is int item ? item : -1;
    }

    private int JumpAndReport(HeadlessCanvasHost host, SkiaScroll scroll, SkiaLayout feed, string name,
        int index, bool animate, RelativePositionType option = RelativePositionType.Start)
    {
        Jump(scroll, index, animate, option);
        Settle(host, scroll);
        var top = TopItem(scroll, feed);
        var w = feed.ItemsWindow;
        _output.WriteLine(
            $"{name}: top item {top}, first visible {feed.FirstVisibleIndex}, window [{w?.WindowStart}..{w?.WindowEnd}), offset {scroll.ViewportOffsetY:0.#}");
        return top;
    }

    [Theory]
    [InlineData(false, 1f, 430, 760)]
    [InlineData(true, 1f, 430, 760)]
    [InlineData(true, 1.25f, 1200, 1000)]
    [InlineData(true, 1.5f, 1200, 1300)]
    [InlineData(false, 1.5f, 1200, 1300)]
    [InlineData(true, 2f, 800, 1600)]
    [InlineData(true, 1f, 1000, 1400)]
    public void Jumps_LandOnRequestedItem_EveryDirection(bool animate, float scale, int w, int h)
    {
        var (host, scroll, feed) = Scene(scale, w, h);
        Assert.NotNull(feed.ItemsWindow);

        // END -> MIDDLE (the reported case)
        JumpAndReport(host, scroll, feed, "END", Count, animate, RelativePositionType.End);
        Assert.Equal(Middle + 1, JumpAndReport(host, scroll, feed, "END->MIDDLE", Middle, animate));

        // MIDDLE -> END: last item at the bottom, so the top is the item a viewport above it
        JumpAndReport(host, scroll, feed, "MIDDLE->END", Count, animate, RelativePositionType.End);
        Assert.Equal(Count, feed.LastVisibleIndex + 1);

        // END -> HOME
        Assert.Equal(1, JumpAndReport(host, scroll, feed, "END->HOME", 0, animate));

        // HOME -> MIDDLE
        Assert.Equal(Middle + 1, JumpAndReport(host, scroll, feed, "HOME->MIDDLE", Middle, animate));

        // MIDDLE -> HOME
        Assert.Equal(1, JumpAndReport(host, scroll, feed, "MIDDLE->HOME", 0, animate));
    }
}
