using DrawnUi.Draw;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The frame loop keeps ticking when a frame listener throws: one error must not end every later frame
/// (the per-frame loop went silent while the app still drew on invalidation, seen on Mac Catalyst and iPhone).
/// </summary>
public class LooperSurvivesFrameErrorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Looper_KeepsTicking_AfterAFrameThrows(bool legacy)
    {
        var calls = 0;
        using var looper = new Looper(() =>
        {
            calls++;
            if (calls == 3)
                throw new InvalidOperationException("a frame listener failed");
        });

        looper.Start(60, legacy);
        // wait for the frames, not a fixed time: under a loaded test run the loop ticks slower
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (Volatile.Read(ref calls) <= 10 && DateTime.UtcNow < deadline)
            await Task.Delay(50);
        looper.Stop();

        Assert.True(calls > 10, $"the loop stopped after the error: {calls} frames");
    }
}
