using System.Collections.Concurrent;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// ImageDoubleBuffered background bakes: a bake action that throws runs once (it used to be retried in a loop),
/// a bake overtaken by a newer one hands its surface back and is disposed (it used to wait for the GC), and
/// CancelOffscreenRendering keeps an in-flight bake from publishing (it used to do nothing).
/// </summary>
public class OffscreenBakeTests
{
    /// <summary>Shape whose background bakes can be held at a gate, recording every cache object it makes.</summary>
    private sealed class GatedBakeShape : SkiaShape
    {
        public readonly ManualResetEventSlim Gate = new(true);
        public readonly ConcurrentQueue<CachedObject> Made = new();

        public override CachedObject CreateRenderingObject(DrawingContext context, SKRect recordingArea,
            CachedObject reuseSurfaceFrom, SkiaCacheType usingCacheType, Action<DrawingContext> action)
        {
            var made = base.CreateRenderingObject(context, recordingArea, reuseSurfaceFrom, usingCacheType, action);
            Gate.Wait(5000);
            Made.Enqueue(made);
            return made;
        }
    }

    private static (HeadlessCanvasHost Host, GatedBakeShape Shape) Create()
    {
        var host = new HeadlessCanvasHost(200, 200, scale: 1f, background: Colors.Black);
        var shape = new GatedBakeShape
        {
            UseCache = SkiaCacheType.ImageDoubleBuffered,
            BackgroundColor = Colors.SteelBlue,
            WidthRequest = 100,
            HeightRequest = 100,
        };
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { shape } };
        Assert.True(WaitFor(host, () => shape.RenderObject != null), "first bake never published");
        return (host, shape);
    }

    /// <summary>Renders frames until the condition holds or 5 s pass.</summary>
    private static bool WaitFor(HeadlessCanvasHost host, Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < until)
        {
            host.RenderFrame();
            if (condition())
                return true;
            Thread.Sleep(5);
        }

        return condition();
    }

    [Fact]
    public void ThrowingBakeAction_RunsOnce()
    {
        var control = new SkiaShape();
        var runs = 0;

        control.PushToOffscreenRendering(() =>
        {
            if (Interlocked.Increment(ref runs) == 1)
                throw new InvalidOperationException("bake failed");
        });

        SpinWait.SpinUntil(() => Volatile.Read(ref runs) > 1, 1000);
        Assert.Equal(1, Volatile.Read(ref runs));
    }

    [Fact]
    public void OvertakenBake_IsDisposed()
    {
        var (host, shape) = Create();
        using var _ = host;
        var first = shape.RenderObject;
        var madeBefore = shape.Made.Count;

        shape.Gate.Reset();
        shape.BackgroundColor = Colors.Orange; // bake A starts and waits at the gate
        Assert.True(SpinWait.SpinUntil(() => shape.DoubleBufferedCacheIsStale, 1000));
        host.RenderFrame();
        shape.BackgroundColor = Colors.Green; // bake B is scheduled while A is in flight
        host.RenderFrame();
        shape.Gate.Set();

        Assert.True(WaitFor(host, () => shape.Made.Count >= madeBefore + 2 && !shape.DoubleBufferedCacheIsStale));
        var made = shape.Made.Skip(madeBefore).ToArray();
        var overtaken = made[0];
        Assert.NotSame(overtaken, shape.RenderObject);
        Assert.Same(made[^1], shape.RenderObject);

        host.AdvanceFrames(6); // disposal waits a few frames
        Assert.True(overtaken.IsDisposed);
        Assert.Null(overtaken.Surface);
        Assert.NotSame(first, shape.RenderObject);
    }

    [Fact]
    public void CancelOffscreenRendering_KeepsInFlightBakeFromPublishing()
    {
        var (host, shape) = Create();
        using var _ = host;
        var first = shape.RenderObject;
        var madeBefore = shape.Made.Count;

        shape.Gate.Reset();
        shape.BackgroundColor = Colors.Orange;
        Assert.True(WaitFor(host, () => shape.DoubleBufferedCacheIsStale));
        shape.CancelOffscreenRendering();
        shape.Gate.Set();

        Assert.True(WaitFor(host, () => shape.Made.Count > madeBefore && !shape.DoubleBufferedCacheIsStale));
        var cancelled = shape.Made.Skip(madeBefore).First();
        Assert.Same(first, shape.RenderObject);

        host.AdvanceFrames(6);
        Assert.True(cancelled.IsDisposed);
    }
}
