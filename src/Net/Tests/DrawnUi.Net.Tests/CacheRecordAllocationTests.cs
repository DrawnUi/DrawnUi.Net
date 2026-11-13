using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A cached control recorded again every frame (its content changes every frame) allocates no new paint callback per
/// record: what a record costs is only what the recording itself needs.
/// </summary>
[Collection("Multithreaded")] // counts allocations: never next to the parallel load of other classes
public class CacheRecordAllocationTests
{
    private readonly ITestOutputHelper _output;

    public CacheRecordAllocationTests(ITestOutputHelper output) => _output = output;

    static long BytesPerFrame(SkiaCacheType cache, bool recordEveryFrame)
    {
        using var host = new HeadlessCanvasHost(200, 100, 1f, Colors.Black);
        var shape = new SkiaShape { WidthRequest = 40, HeightRequest = 40, BackgroundColor = Colors.Red, UseCache = cache };
        host.Canvas.Content = new SkiaLayer { Children = { shape } };
        return AllocationMeasure.MinBytesPerFrame(() =>
        {
            if (recordEveryFrame)
                shape.Update(); // its cache is recorded again
            host.RenderFrame();
        });
    }

    [Theory]
    [InlineData(SkiaCacheType.Operations)]
    [InlineData(SkiaCacheType.Image)]
    public void RecordEveryFrame_NoPaintCallbackPerRecord(SkiaCacheType cache)
    {
        long still = BytesPerFrame(cache, false);
        long recorded = BytesPerFrame(cache, true);
        _output.WriteLine($"{cache}: {recorded} B/frame recorded every frame, {still} B/frame still");
        // the paint callback was a new 64-byte delegate per record; what remains is the recording's own objects
        Assert.True(recorded - still <= RecordBudget[cache], $"{cache}: a record adds {recorded - still} B");
    }

    // what one record costs after the fix, measured: the picture or surface wrappers SkiaSharp makes
    static readonly Dictionary<SkiaCacheType, long> RecordBudget = new()
    {
        [SkiaCacheType.Operations] = 2480, // 2448 measured (fewest bytes of a frame), 64 more with a new delegate per record
        [SkiaCacheType.Image] = 530, // 504 measured, 64 more with a new delegate per record
    };
}
