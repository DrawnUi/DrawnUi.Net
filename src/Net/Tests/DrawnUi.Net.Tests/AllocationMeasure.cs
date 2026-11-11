namespace DrawnUi.Net.Tests;

/// <summary>
/// Bytes one frame allocates on the test thread, robust to noise: the fewest bytes any single frame allocated after a
/// warm-up. A frame that also pays for something unrelated now and then (a timer, a first-use cache, a log line) never
/// lowers the minimum, so the steady cost of a frame is compared, not an average two runs disagree on.
/// </summary>
static class AllocationMeasure
{
    public static long MinBytesPerFrame(Action frame, int warmup = 60, int frames = 120)
    {
        for (int i = 0; i < warmup; i++)
            frame();
        long min = long.MaxValue;
        for (int i = 0; i < frames; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            frame();
            min = Math.Min(min, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        return min;
    }
}
