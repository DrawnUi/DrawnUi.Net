using System.Drawing;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaScroll.ScrollToIndex on a Split (items grid) layout: the index is an ITEM index and must
/// land on that item's row. It used to be read as a row index, so on a 3-column grid any item
/// past the first rows made the order silently invalid and the scroll never moved.
/// A plain column (Split = 1) must keep working exactly as before.
/// </summary>
public class ScrollToIndexSplitTests
{
    private readonly ITestOutputHelper _output;

    public ScrollToIndexSplitTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const int CellHeight = 100;

    private static (HeadlessCanvasHost host, SkiaScroll scroll) Scene(int split, int count)
    {
        var host = new HeadlessCanvasHost(300, 600, scale: 1f, background: Colors.Black);

        var scroll = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout
            {
                Type = LayoutType.Column,
                Split = split,
                Spacing = 0,
                HorizontalOptions = LayoutOptions.Fill,
                RecyclingTemplate = RecyclingTemplate.Enabled,
                MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
                ItemsSource = Enumerable.Range(0, count).ToList(),
                ItemTemplate = new DataTemplate(() => new SkiaShape
                {
                    Type = ShapeType.Rectangle,
                    BackgroundColor = Colors.DarkSlateBlue,
                    HorizontalOptions = LayoutOptions.Fill,
                    HeightRequest = CellHeight,
                }),
            }
        };

        host.Canvas.Content = scroll;
        host.AdvanceFrames(8);
        return (host, scroll);
    }

    [Theory]
    [InlineData(3, 90, 26, 8)]   // 3 columns: item 26 sits in row 8
    [InlineData(3, 90, 1, 0)]    // first row, no travel
    [InlineData(3, 90, 30, 10)]  // first item of a row
    [InlineData(1, 40, 8, 8)]    // plain column: index is the row
    [InlineData(2, 60, 15, 7)]   // 2 columns: item 15 sits in row 7
    public void ScrollToIndex_LandsOnItemsRow(int split, int count, int index, int expectedRow)
    {
        var (host, scroll) = Scene(split, count);

        scroll.ScrollToIndex(index, false, RelativePositionType.Start);
        host.AdvanceFrames(12);

        var offset = scroll.InternalViewportOffset.Units.Y;
        _output.WriteLine($"split={split} index={index}: offset {offset:0.#}, expected {-expectedRow * CellHeight}");

        Assert.Equal(-expectedRow * CellHeight, offset, 1.0);
    }
}
