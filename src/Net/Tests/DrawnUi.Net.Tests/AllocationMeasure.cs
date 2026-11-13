using SkiaSharp;

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

    /// <summary>
    /// SkiaSharp's uniform writer allocates 48 B per write (a boxed Enum.HasFlag) until the JIT optimizes it, about a
    /// second after its first calls; then nothing. A count that includes uniform writes calls this first: else it
    /// depends on whether the optimized writer arrived during the warm-up.
    /// </summary>
    public static void OptimizeUniformWrites()
    {
        using var effect = SKRuntimeEffect.CreateShader("uniform float4 u; half4 main(float2 p) { return half4(u); }", out var errors);
        using var holder = new SKRuntimeEffectUniforms(effect ?? throw new InvalidOperationException(errors));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; clock.ElapsedMilliseconds < 10000; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            holder["u"] = (ReadOnlySpan<float>)stackalloc float[] { i, 0, 0, 1 };
            if (GC.GetAllocatedBytesForCurrentThread() == before)
                return;
            if (i > 30)
                Thread.Sleep(1); // past the calls the JIT counts: give its background thread the time
        }
    }
}
