using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Controls;
using DrawnUi.Views;
using DrawnUi.Testing;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// iTime keeps its precision on a device that has been on for days: frame times count from the device's boot, and
/// iTime is the time since the first shader frame, taken in whole nanoseconds before it becomes a float. Taken as a
/// float of the uptime, 18 days moved in steps of 0.125 s, so an animated shader (the Dungeon's dialog frame on an
/// iPhone) jumped 8 times a second.
/// </summary>
[Collection("Multithreaded")] // sets the process-wide iTime origin: never next to other shader tests
public class ShaderTimeTests
{
    private const string Animated = """
uniform float2 iResolution;
uniform float2 iImageResolution;
uniform float iTime;
uniform float2 iOffset;
uniform float4 iMouse;

half4 main(float2 fragCoord)
{
    return half4(fract(iTime), 0.0, 0.0, 1.0);
}
""";

    [Fact]
    public void ITime_AdvancesByTheFrameStep_AfterDaysOfUptime()
    {
        const long uptime = 1_580_000L * 1_000_000_000L; // about 18 days, in nanoseconds
        var was = SkiaShaderEffect.ShaderTimeOriginNanos;
        SkiaShaderEffect.ShaderTimeOriginNanos = long.MinValue; // the next shader frame sets the origin, as in an app
        try
        {
            using var host = new HeadlessCanvasHost(40, 30, 1f, null, uptime);
            var effect = new SkiaShaderEffect { ShaderCode = Animated, UseBackground = PostRendererEffectUseBackgroud.Never, AutoCreateInputTexture = false };
            host.Canvas.Content = new SkiaLayer
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                VisualEffects = { effect },
            }.UpdateNonStop();

            host.AdvanceFrames(3, 16.0);
            var times = new List<float>();
            for (var i = 0; i < 8; i++)
            {
                host.RenderFrame(16.0);
                times.Add(effect.TimeSeconds);
            }

            for (var i = 1; i < times.Count; i++)
                Assert.InRange(times[i] - times[i - 1], 0.0155f, 0.0165f);
            Assert.InRange(times[0], 0f, 1f); // counted from the first shader frame, not from the boot
        }
        finally
        {
            SkiaShaderEffect.ShaderTimeOriginNanos = was;
        }
    }
}
