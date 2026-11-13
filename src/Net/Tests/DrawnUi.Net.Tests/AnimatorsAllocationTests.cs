using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Running animators cost no allocation per frame by themselves: the canvas walks its animators without copying their
/// keys into new lists every frame.
/// </summary>
[Collection("Multithreaded")] // counts allocations: never next to the parallel load of other classes
public class AnimatorsAllocationTests
{
    static long BytesPerFrame(int animators)
    {
        using var host = new HeadlessCanvasHost(200, 100, 1f, Colors.Black);
        var layer = new SkiaLayer();
        for (int i = 0; i < animators; i++)
            layer.Children.Add(new SkiaControl { WidthRequest = 4, HeightRequest = 4 }.UpdateNonStop());
        host.Canvas.Content = layer;
        return AllocationMeasure.MinBytesPerFrame(() => host.AdvanceFrames(1));
    }

    [Fact]
    public void RunningAnimators_AllocateAlmostNothingPerFrame()
    {
        long one = BytesPerFrame(1);
        long many = BytesPerFrame(40);
        // what 39 more animators add per frame: nothing that grows with their number
        Assert.True(many - one <= 16, $"40 animators: {many} B/frame, 1 animator: {one} B/frame");
    }
}
