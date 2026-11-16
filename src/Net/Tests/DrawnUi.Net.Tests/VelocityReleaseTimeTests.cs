using System.Numerics;
using DrawnUi.Draw;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// VelocityAccumulator.CalculateFinalVelocity with the release's own time: samples dated by when the input happened
/// still count when a slow frame handles the release late (pure Wasm dates pointer events back to the event time).
/// </summary>
public class VelocityReleaseTimeTests
{
    [Fact]
    public void A_release_handled_late_still_flings_when_judged_at_its_own_time()
    {
        var acc = new VelocityAccumulator();
        var t0 = Super.GetCurrentTimeNanos() - 1_000_000_000; // the flick happened a second ago
        for (var i = 0; i < 4; i++)
            acc.CaptureVelocity(new Vector2(0, 2000), t0 + i * 16_000_000);
        var release = t0 + 64_000_000;

        Assert.Equal(Vector2.Zero, acc.CalculateFinalVelocity()); // judged at "now": all samples too old
        Assert.Equal(2000, acc.CalculateFinalVelocity(0, release).Y, 1);
    }

    [Fact]
    public void A_finger_held_still_before_release_does_not_fling()
    {
        var acc = new VelocityAccumulator();
        var t0 = Super.GetCurrentTimeNanos() - 2_000_000_000;
        for (var i = 0; i < 4; i++)
            acc.CaptureVelocity(new Vector2(0, 2000), t0 + i * 16_000_000);
        var release = t0 + 48_000_000 + 400_000_000; // held 400 ms after the last move

        Assert.Equal(Vector2.Zero, acc.CalculateFinalVelocity(0, release));
    }
}
