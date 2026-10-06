using AppoMobi.Gestures;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A drag whose moves arrive in bursts flings as far as the same drag delivered evenly. WSLg / X11 sends a drag's
/// moves in pairs 0.01 ms apart every ~15 ms: measured move by move, the second of each pair read millions of px/s and
/// every fling left at the velocity limit, about twice as far as on Windows (DrawnUi.Rust 6d525f7 found and fixed the
/// same on its host).
/// </summary>
public class DesktopFlingBurstTests
{
    private readonly ITestOutputHelper _out;

    public DesktopFlingBurstTests(ITestOutputHelper output) => _out = output;

    private static (HeadlessCanvasHost host, SkiaScroll scroll) Scroll()
    {
        var host = new HeadlessCanvasHost(300, 600, scale: 1f, background: Colors.Black);
        SkiaScroll scroll = null;
        host.Canvas.Content = new SkiaScroll
        {
            Orientation = ScrollOrientation.Vertical,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout
            {
                Type = LayoutType.Column,
                HorizontalOptions = LayoutOptions.Fill,
                Children = Enumerable.Range(0, 400).Select(i => (SkiaControl)new SkiaShape
                {
                    HeightRequest = 50,
                    HorizontalOptions = LayoutOptions.Fill,
                    BackgroundColor = i % 2 == 0 ? Colors.DarkGray : Colors.Gray,
                }).ToList(),
            },
        }.Assign(out scroll);
        host.AdvanceFrames(5);
        return (host, scroll);
    }

    /// <summary>Upward drag at 1000 px/s: 15 px every 15 ms, as pairs (6 px, then 9 px 0.01 ms later) or evenly (5 px every 5 ms).</summary>
    private static List<(double, double, double)> Drag(bool bursts)
    {
        var path = new List<(double, double, double)> { (0, 150, 500) };
        var y = 500.0;
        if (bursts)
        {
            for (var k = 1; k <= 12; k++)
            {
                y -= 6;
                path.Add((k * 15.0, 150, y));
                y -= 9;
                path.Add((k * 15.0 + 0.01, 150, y));
            }
        }
        else
        {
            for (var k = 1; k <= 36; k++)
            {
                y -= 5;
                path.Add((k * 5.0, 150, y));
            }
        }

        return path;
    }

    private float FlingDistance(Action<GestureRobot> drag, string what)
    {
        var (host, scroll) = Scroll();
        using var _ = host;
        var robot = new GestureRobot(host) { Device = PointerDeviceType.Mouse };
        drag(robot);
        var released = scroll.ViewportOffsetY;
        robot.SettleFling(scroll);
        var flung = Math.Abs(scroll.ViewportOffsetY - released);
        _out.WriteLine($"{what}: dragged to {released:0}, flung {flung:0} px");
        return flung;
    }

    /// <summary>
    /// The same 1000 px/s drag through the desktop pointer path, as pairs and evenly, flings like the robot's own timed
    /// pan (the reference: its events carry the robot's clock). Before, the burst drag measured each pair's second move
    /// at millions of px/s.
    /// </summary>
    [Fact]
    public void BurstDrag_FlingsLikeAnEvenDrag()
    {
        var reference = FlingDistance(r => r.Pan(new System.Drawing.PointF(150, 500), new System.Drawing.PointF(150, 320), 180, 36), "robot pan");
        var even = FlingDistance(r => r.DesktopDrag(Drag(bursts: false), releaseAfterMs: 1), "desktop even");
        var bursts = FlingDistance(r => r.DesktopDrag(Drag(bursts: true), releaseAfterMs: 1), "desktop bursts");

        Assert.True(reference > 100, $"the reference drag flings ({reference:0} px)");
        Assert.InRange(even / reference, 0.85f, 1.15f);
        Assert.InRange(bursts / reference, 0.85f, 1.15f);
    }
}
