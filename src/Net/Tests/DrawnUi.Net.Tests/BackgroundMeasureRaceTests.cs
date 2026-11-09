using System.Diagnostics;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// MeasureVisible measures the items past the screen in background batches while the frames append the batches
/// already measured to the layout's structure. A batch starts where the previous one of its pass ended: it used to
/// read the structure the frame thread was appending to, which threw "Collection was modified" and ended the pass.
/// Inside a SkiaScroll, which caches itself, nothing then drew the layout again to start the next pass, and the
/// frontier stopped for good (a 1000-item Row stuck at 924).
/// </summary>
[Collection("Multithreaded")] // background measurement on real threads, a trace listener
public class BackgroundMeasureRaceTests
{
    private sealed class Item
    {
        public int Index;
        public float Width;
    }

    private sealed class Cell : SkiaShape
    {
        public Cell()
        {
            HeightRequest = 100;
            BackgroundColor = Colors.Gray;
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is Item item)
                WidthRequest = item.Width;
        }
    }

    private sealed class ErrorCapture : TraceListener
    {
        public int Errors;
        public override void Write(string message) => Count(message);
        public override void WriteLine(string message) => Count(message);

        void Count(string message)
        {
            if (message != null && message.Contains("Background measurement error"))
                Interlocked.Increment(ref Errors);
        }
    }

    private static List<Item> Items(int count) => Enumerable.Range(0, count)
        .Select(i => new Item { Index = i, Width = 40 + (i * 37) % 60 }).ToList();

    private static (HeadlessCanvasHost host, SkiaLayout row) Scene(List<Item> items, int batch)
    {
        var host = new HeadlessCanvasHost(800, 200, scale: 1f, background: Colors.Black);
        SkiaLayout row = null;
        host.Canvas.Content = new SkiaScroll
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout
            {
                Type = LayoutType.Row,
                Spacing = 0,
                MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
                BackgroundMeasurementBatchSize = batch,
                ItemsSource = items,
                ItemTemplate = new DataTemplate(() => new Cell()),
            }.Assign(out row)
        };
        return (host, row);
    }

    /// <summary>Frames back to back until every item is in the structure, or until it did not grow for a second.</summary>
    private static void RunUntilMeasured(HeadlessCanvasHost host, SkiaLayout row, int count)
    {
        var wait = Stopwatch.StartNew();
        int last = -1;
        long lastChange = 0;
        while (wait.ElapsedMilliseconds < 20000)
        {
            host.RenderFrame(16);
            var now = row.GetStackStructure().GetCount();
            if (now == count && row.LastMeasuredIndex == count - 1 && !row.IsBackgroundMeasuring)
                break;
            if (now != last)
            {
                last = now;
                lastChange = wait.ElapsedMilliseconds;
            }
            else if (wait.ElapsedMilliseconds - lastChange > 1000)
                break; // stopped
        }
        host.AdvanceFrames(2);
    }

    [Fact]
    public void BatchesWhileFramesAppend_NoPassFails_EveryItemInPlace()
    {
        var capture = new ErrorCapture();
        Trace.Listeners.Add(capture);
        try
        {
            for (var n = 0; n < 3; n++)
            {
                // one item per batch: a batch for every frame to append while the next one starts
                var items = Items(2000);
                var (host, row) = Scene(items, 1);
                using (host)
                {
                    RunUntilMeasured(host, row, items.Count);

                    var structure = row.GetStackStructure();
                    Assert.Equal(items.Count, structure.GetCount());
                    float left = structure.GetForIndex(0).Destination.Left;
                    foreach (var item in items)
                    {
                        var cell = structure.GetForIndex(item.Index);
                        Assert.True(Math.Abs(cell.Destination.Left - left) < 1,
                            $"#{item.Index} at {cell.Destination.Left}, expected {left}");
                        left += item.Width;
                    }
                }
            }

            Assert.Equal(0, capture.Errors);
        }
        finally
        {
            Trace.Listeners.Remove(capture);
        }
    }
}
