using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A long templated Column measuring every item (past SkiaLayout.WindowSourceThreshold, so the built-in items window
/// slides) scrolled down with the desktop wheel: every notch moves a few rows, through the window slides. The way
/// back up is checked in a real app: here MainThread runs inline, inside the draw, where a backward slide's
/// invalidation is dropped.
/// </summary>
public class WindowWheelScrollTests
{
    private readonly ITestOutputHelper _output;
    public WindowWheelScrollTests(ITestOutputHelper output) => _output = output;

    private class Row : SkiaShape
    {
        public Row()
        {
            HorizontalOptions = LayoutOptions.Fill;
            BackgroundColor = Colors.Gray;
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is int i)
                HeightRequest = 40 + (i * 37) % 60;
        }
    }

    [Fact]
    public void MeasureAll_WheelDown_ThroughWindowSlides_MovesAFewRowsPerNotch()
    {
        using var host = new HeadlessCanvasHost(400, 800, scale: 1f, background: Colors.Black);
        SkiaLayout column = null;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout
            {
                Type = LayoutType.Column,
                MeasureItemsStrategy = MeasuringStrategy.MeasureAll,
                HorizontalOptions = LayoutOptions.Fill,
                ItemsSource = Enumerable.Range(0, 1000).ToList(),
                ItemTemplate = new DataTemplate(() => new Row()),
            }.Assign(out column)
        };
        host.AdvanceFrames(10);

        var slid = false;
        void Run(int notches, int delta)
        {
            var last = column.FirstVisibleIndex;
            for (var i = 0; i < notches; i++)
            {
                host.Canvas.HandleDesktopWheel(200, 400, delta, 400, 800);
                host.AdvanceFrames(4);
                var first = column.FirstVisibleIndex;
                slid |= column.ItemsWindow?.WindowStart > 0;
                Assert.True(first >= 0, $"notch {i}: nothing visible");
                Assert.True(Math.Abs(first - last) <= 8,
                    $"notch {i} ({delta}): first visible {last} -> {first}, window {column.ItemsWindow?.WindowStart}..{column.ItemsWindow?.WindowEnd}");
                last = first;
            }
            _output.WriteLine($"wheel {delta}: ended at {last}");
        }

        Run(100, -120);
        Assert.True(slid, "the window never slid: the test did not reach a window edge");
    }
}
