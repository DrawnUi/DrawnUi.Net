using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The frame-rate label draws every frame: while the shown value stays the same it must not make a new text (a string
/// per frame) nor lay its text out again.
/// </summary>
[Collection("Multithreaded")] // counts allocations: never next to the parallel load of other classes
public class SkiaLabelFpsAllocationTests
{
    static long BytesPerFrame(bool withLabel)
    {
        using var host = new HeadlessCanvasHost(200, 100, 1f, Colors.Black);
        host.Canvas.Content = new SkiaLayer
        {
            Children = withLabel
                ? new List<SkiaControl> { new SkiaLabelFps { HorizontalOptions = LayoutOptions.Start } }
                : new List<SkiaControl>(),
        };
        host.AdvanceFrames(30);
        const int frames = 200;
        long before = GC.GetAllocatedBytesForCurrentThread();
        host.AdvanceFrames(frames);
        return (GC.GetAllocatedBytesForCurrentThread() - before) / frames;
    }

    [Fact]
    public void UnchangedValue_AllocatesNothingPerFrame()
    {
        long without = BytesPerFrame(false);
        long with = BytesPerFrame(true);
        Assert.True(with - without <= 8, $"label adds {with - without} bytes per frame ({with} with, {without} without)");
    }
}
