using System.Collections.Concurrent;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// ImageDoubleBuffered background bakes: a bake action that throws runs once (it used to be retried in a loop),
/// a cancelled bake hands its surface back and is disposed (it used to wait for the GC),
/// CancelOffscreenRendering keeps an in-flight bake from publishing (it used to do nothing), the control stays busy
/// while a newer bake runs, a control changing every frame still shows its bakes, the placeholder shows until the
/// first bitmap and never over one, and GPU-cached children paint live inside a bake.
/// </summary>
public class OffscreenBakeTests
{
    /// <summary>Shape whose background bakes can be held at a gate, recording every cache object it makes.</summary>
    private sealed class GatedBakeShape : SkiaShape
    {
        public readonly ManualResetEventSlim Gate = new(true);
        public readonly ConcurrentQueue<CachedObject> Made = new();

        /// <summary>When set, each bake waits for one release of it instead of the gate.</summary>
        public SemaphoreSlim Steps;

        public int Placeholders;

        /// <summary>Bakes that painted and now wait at the gate or for a step.</summary>
        public int Started;

        public override CachedObject CreateRenderingObject(DrawingContext context, SKRect recordingArea,
            CachedObject reuseSurfaceFrom, SkiaCacheType usingCacheType, Action<DrawingContext> action)
        {
            var made = base.CreateRenderingObject(context, recordingArea, reuseSurfaceFrom, usingCacheType, action);
            Interlocked.Increment(ref Started);
            var steps = Steps;
            if (steps != null)
                steps.Wait(5000);
            else
                Gate.Wait(5000);
            Made.Enqueue(made);
            return made;
        }

        public override void DrawPlaceholder(DrawingContext context)
        {
            Placeholders++;
            base.DrawPlaceholder(context);
        }
    }

    /// <summary>Records whether it was asked to make a cache on a bake worker thread.</summary>
    private sealed class ThreadProbeShape : SkiaShape
    {
        public volatile bool CachedOnBakeThread;

        public override CachedObject CreateRenderingObject(DrawingContext context, SKRect recordingArea,
            CachedObject reuseSurfaceFrom, SkiaCacheType usingCacheType, Action<DrawingContext> action)
        {
            if (Thread.CurrentThread.Name?.StartsWith("DrawnUi-OffscreenBake") == true)
                CachedOnBakeThread = true;
            return base.CreateRenderingObject(context, recordingArea, reuseSurfaceFrom, usingCacheType, action);
        }
    }

    /// <summary>Opens every gate and waits until no bake of the shape runs, so the host can be disposed.</summary>
    private static void Drain(HeadlessCanvasHost host, GatedBakeShape shape, SemaphoreSlim steps = null)
    {
        shape.Steps = null;
        steps?.Release(100);
        shape.Gate.Set();
        WaitFor(host, () => !shape.DoubleBufferedCacheIsStale);
        Thread.Sleep(100);
        WaitFor(host, () => !shape.DoubleBufferedCacheIsStale);
    }

    /// <summary>Renders frames until a bake of the shape is in flight (painted, waiting), so a newer one queues behind it.</summary>
    private static void WaitBakeStarted(HeadlessCanvasHost host, GatedBakeShape shape, int startedBefore)
        => Assert.True(WaitFor(host, () => Volatile.Read(ref shape.Started) > startedBefore), "bake never started");

    private static SKColor PixelAt(HeadlessCanvasHost host, int x, int y)
    {
        using var image = host.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.GetPixel(x, y);
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
    public void OvertakenBake_IsShownThenReplacedByTheNewerOne()
    {
        var (host, shape) = Create();
        using var _ = host;
        var madeBefore = shape.Made.Count;

        shape.Gate.Reset();
        var started = shape.Started;
        shape.BackgroundColor = Colors.Orange; // bake A starts and waits at the gate
        WaitBakeStarted(host, shape, started);
        shape.BackgroundColor = Colors.Green; // bake B is scheduled while A is in flight
        host.RenderFrame();
        shape.Gate.Set();

        Assert.True(WaitFor(host, () => shape.Made.Count >= madeBefore + 2 && !shape.DoubleBufferedCacheIsStale));
        var made = shape.Made.Skip(madeBefore).ToArray();
        Assert.Same(made[^1], shape.RenderObject);
        Assert.Same(made[0], shape.RenderObjectPrevious); // A was shown before B replaced it
    }

    [Fact]
    public void CancelOffscreenRendering_KeepsInFlightBakeFromPublishing()
    {
        var (host, shape) = Create();
        using var _ = host;
        var first = shape.RenderObject;
        var madeBefore = shape.Made.Count;

        shape.Gate.Reset();
        var started = shape.Started;
        shape.BackgroundColor = Colors.Orange;
        WaitBakeStarted(host, shape, started);
        shape.CancelOffscreenRendering();
        shape.Gate.Set();

        Assert.True(WaitFor(host, () => shape.Made.Count > madeBefore && !shape.DoubleBufferedCacheIsStale));
        var cancelled = shape.Made.Skip(madeBefore).First();
        Assert.Same(first, shape.RenderObject);

        host.AdvanceFrames(6);
        Assert.True(cancelled.IsDisposed);
    }

    [Fact]
    public void OlderBakeFinishing_KeepsTheControlBusyWhileANewerOneRuns()
    {
        var (host, shape) = Create();
        using var _ = host;
        var steps = shape.Steps = new SemaphoreSlim(0);

        var started = shape.Started;
        shape.BackgroundColor = Colors.Orange; // bake A starts and waits
        WaitBakeStarted(host, shape, started);
        shape.BackgroundColor = Colors.Green; // bake B is scheduled behind A
        host.RenderFrame();
        var madeBefore = shape.Made.Count;
        steps.Release(); // A finishes, B starts and waits

        Assert.True(SpinWait.SpinUntil(() => shape.Made.Count > madeBefore, 2000));
        Thread.Sleep(50);
        Assert.True(shape.DoubleBufferedCacheIsStale, "the control was marked idle while the newer bake runs");

        Drain(host, shape, steps);
    }

    [Fact]
    public void ControlChangingEveryFrame_StillShowsItsBakes()
    {
        var (host, shape) = Create();
        using var _ = host;
        var first = shape.RenderObject;
        var steps = shape.Steps = new SemaphoreSlim(0);

        var started = shape.Started;
        shape.BackgroundColor = Colors.Orange; // a bake starts and waits
        WaitBakeStarted(host, shape, started);
        foreach (var color in new[] { Colors.Green, Colors.Red, Colors.Yellow, Colors.Purple })
        {
            started = shape.Started;
            shape.BackgroundColor = color;
            host.RenderFrame(); // the next bake is scheduled while one is in flight
            steps.Release(); // the one in flight finishes, the next one starts
            WaitBakeStarted(host, shape, started);
        }

        host.RenderFrame();
        var shown = shape.RenderObject;
        Drain(host, shape, steps);
        Assert.NotSame(first, shown);
    }

    [Fact]
    public void Placeholder_DrawnEveryFrameUntilTheFirstBitmap()
    {
        using var host = new HeadlessCanvasHost(200, 200, scale: 1f, background: Colors.Black);
        var shape = new GatedBakeShape
        {
            UseCache = SkiaCacheType.ImageDoubleBuffered,
            BackgroundColor = Colors.SteelBlue,
            WidthRequest = 100,
            HeightRequest = 100,
        };
        shape.Gate.Reset();
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { shape } };

        host.AdvanceFrames(4);

        try
        {
            Assert.True(shape.Placeholders >= 3, $"placeholder drawn {shape.Placeholders} time(s) in 4 frames");
            var pixel = PixelAt(host, 50, 50);
            Assert.True(pixel.Blue > 100, $"no placeholder at the control: {pixel}");
        }
        finally
        {
            Drain(host, shape);
        }
    }

    [Fact]
    public void BindingContextChange_ShowsTheCache_NoPlaceholderOverIt()
    {
        var (host, shape) = Create();
        using var _ = host;
        var placeholders = shape.Placeholders;
        shape.Gate.Reset();

        shape.BindingContext = new object();
        shape.BackgroundColor = Colors.Orange;
        host.AdvanceFrames(3);

        try
        {
            Assert.Equal(placeholders, shape.Placeholders);
        }
        finally
        {
            Drain(host, shape);
        }
    }

    [Fact]
    public void GpuCachedChild_PaintsLiveInsideABake()
    {
        using var host = new HeadlessCanvasHost(200, 200, scale: 1f, background: Colors.Black);
        ThreadProbeShape child = null;
        var parent = new SkiaLayer
        {
            UseCache = SkiaCacheType.ImageDoubleBuffered,
            WidthRequest = 100,
            HeightRequest = 100,
            Children =
            {
                new ThreadProbeShape
                {
                    UseCache = SkiaCacheType.GPU,
                    BackgroundColor = Colors.Orange,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                }.Assign(out child),
            }
        };
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { parent } };

        Assert.True(WaitFor(host, () => parent.RenderObject != null));
        host.AdvanceFrames(2);

        Assert.False(child.CachedOnBakeThread, "a GPU cache was made on a bake thread");
        var pixel = PixelAt(host, 50, 50);
        Assert.True(pixel.Red > 200 && pixel.Green > 100, $"the child is missing from the baked parent: {pixel}");
    }
}

[CollectionDefinition("Multithreaded", DisableParallelization = true)]
public class MultithreadedCollection
{
}

/// <summary>
/// Super.Multithreaded (experimental, off by default) bakes every cache off the frame thread; an OperationsFull
/// control there must still paint at its own place, not at its record area (the canvas clip).
/// </summary>
[Collection("Multithreaded")]
public class MultithreadedOperationsFullTests
{
    [Fact]
    public void OperationsFull_BakedOffThread_PaintsAtTheControl()
    {
        var was = Super.Multithreaded;
        Super.Multithreaded = true;
        try
        {
            using var host = new HeadlessCanvasHost(300, 300, scale: 1f, background: Colors.Black);
            var shape = new SkiaLayout
            {
                UseCache = SkiaCacheType.OperationsFull,
                BackgroundColor = Colors.Orange,
                WidthRequest = 60,
                HeightRequest = 60,
                Margin = new Thickness(150, 150, 0, 0),
            };
            host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { shape } };

            var until = DateTime.UtcNow.AddSeconds(5);
            while (shape.RenderObject == null && DateTime.UtcNow < until)
            {
                host.RenderFrame();
                Thread.Sleep(5);
            }
            host.AdvanceFrames(3);

            Assert.Equal(SkiaCacheType.OperationsFull, shape.UsingCacheType);
            using var image = host.Snapshot();
            using var bitmap = SKBitmap.FromImage(image);
            var inside = bitmap.GetPixel(180, 180);
            var outside = bitmap.GetPixel(20, 20);
            Assert.True(inside.Red > 200 && inside.Green > 100, $"control not painted at its place: {inside}");
            Assert.True(outside.Red < 50, $"painted outside the control: {outside}");
        }
        finally
        {
            Super.Multithreaded = was;
        }
    }
}
