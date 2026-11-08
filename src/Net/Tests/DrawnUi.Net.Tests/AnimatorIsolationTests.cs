using DrawnUi.Draw;
using DrawnUi.Testing;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Every animator of a frame runs on its own: one whose code throws never stops the others, in that frame or later
/// (drawnui-cross 6bg, as DrawnUI for React and Rust).
/// </summary>
public class AnimatorIsolationTests
{
    [Fact]
    public void A_throwing_animator_does_not_stop_the_others()
    {
        using var host = new HeadlessCanvasHost(200, 200);
        var broken = new SkiaShape { WidthRequest = 20, HeightRequest = 20 };
        var healthy = new SkiaShape { WidthRequest = 20, HeightRequest = 20 };
        host.Canvas.Content = new SkiaLayer { Children = { broken, healthy } };
        host.AdvanceFrames(2);

        var brokenTicks = 0;
        var healthyTicks = 0;
        // whatever order the frame runs them in, the healthy one ticks every frame
        new RangeAnimator(broken).Start(_ => { brokenTicks++; throw new InvalidOperationException("app bug"); }, 0, 1, 1000);
        new RangeAnimator(healthy).Start(_ => healthyTicks++, 0, 1, 1000);

        host.AdvanceFrames(10);

        Assert.True(brokenTicks > 0, "the failing animator ran");
        Assert.True(healthyTicks >= 8, $"the healthy animator ticked every frame ({healthyTicks})");
    }
}
