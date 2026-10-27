using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A long templated Column, Grid or Wrap of Split columns (past SkiaLayout.WindowSourceThreshold, so
/// the built-in items window slides) scrolled down with the desktop wheel: every notch moves a few rows, through the
/// window slides, both the item drawn at the top edge and FirstVisibleIndex. The way
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

        public bool Uniform { get; set; }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is int i)
                HeightRequest = Uniform ? 50 : 40 + (i * 37) % 60;
        }
    }

    [Theory]
    [InlineData(LayoutType.Column, 1, MeasuringStrategy.MeasureAll)]
    [InlineData(LayoutType.Column, 2, MeasuringStrategy.MeasureAll)]
    [InlineData(LayoutType.Column, 3, MeasuringStrategy.MeasureAll)]
    [InlineData(LayoutType.Wrap, 2, MeasuringStrategy.MeasureAll)]
    [InlineData(LayoutType.Wrap, 3, MeasuringStrategy.MeasureFirst)]
    [InlineData(LayoutType.Column, 1, MeasuringStrategy.MeasureFirst)]
    [InlineData(LayoutType.Column, 2, MeasuringStrategy.MeasureFirst)]
    [InlineData(LayoutType.Grid, 2, MeasuringStrategy.MeasureAll)]
    public void WheelDown_ThroughWindowSlides_MovesAFewRowsPerNotch(LayoutType type, int split, MeasuringStrategy strategy)
    {
        using var host = new HeadlessCanvasHost(400, 800, scale: 1f, background: Colors.Black);
        SkiaLayout column = null;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout
            {
                Type = type,
                Split = split,
                MeasureItemsStrategy = strategy,
                HorizontalOptions = LayoutOptions.Fill,
                ItemsSource = Enumerable.Range(0, 1000).ToList(),
                ItemTemplate = new DataTemplate(() => new Row { Uniform = strategy == MeasuringStrategy.MeasureFirst }),
            }.Assign(out column)
        };
        host.AdvanceFrames(10);

        var slid = false;
        void Run(int notches, int delta)
        {
            var last = column.FirstVisibleIndex;
            var lastDrawn = -1;
            for (var i = 0; i < notches; i++)
            {
                host.Canvas.HandleDesktopWheel(200, 400, delta, 400, 800);
                host.AdvanceFrames(4);
                var first = column.FirstVisibleIndex;

                // the item really drawn at the top edge moves a few rows too
                var top = column.RenderTree?.Where(t => t.Rect.Bottom > 0).OrderBy(t => t.Rect.Top).FirstOrDefault();
                var drawnTop = top?.Control.BindingContext is int item ? item : -1;
                Assert.True(drawnTop >= 0, $"notch {i}: nothing drawn");
                Assert.True(lastDrawn < 0 || Math.Abs(drawnTop - lastDrawn) <= 8 * split,
                    $"notch {i} ({delta}): drawn top item {lastDrawn} -> {drawnTop}");
                lastDrawn = drawnTop;
                slid |= column.ItemsWindow?.WindowStart > 0;
                Assert.True(first >= 0, $"notch {i}: nothing visible");
                Assert.True(Math.Abs(first - last) <= 8 * split,
                    $"notch {i} ({delta}): first visible {last} -> {first}, window {column.ItemsWindow?.WindowStart}..{column.ItemsWindow?.WindowEnd}");
                last = first;
            }
            _output.WriteLine($"wheel {delta}: ended at {last}");
        }

        Run(100, -120);
        Assert.True(slid, "the window never slid: the test did not reach a window edge");
    }
}
