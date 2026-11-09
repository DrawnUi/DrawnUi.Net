using System.Diagnostics;

namespace Add3dGallery;

/// <summary>
/// Measures the gallery as DrawnUI for Rust's gallery3d was measured: the whole gallery animating with 100 mobs, the
/// CPU time of each frame's work (animators, game loop, layout, drawing and the 3D GL calls; Skia's GPU flush comes
/// after it and is not counted), median and p95 over 4 s after a 2 s warm-up, logged to the browser console.
/// </summary>
public static class PerfProbe
{
    const double WarmupSeconds = 2, MeasureSeconds = 4;

    static readonly double[] Frames = new double[4096];
    static int _count;
    static long _start, _frameStart, _first, _last;
    static DrawnView _canvas;

    public static void Start(GalleryPage page)
    {
        page.SetMobs(100);
        Tasks.StartDelayed(TimeSpan.FromMilliseconds(500), () => Attach(page));
    }

    static void Attach(GalleryPage page)
    {
        if (page.Superview is not DrawnView canvas)
        {
            Tasks.StartDelayed(TimeSpan.FromMilliseconds(500), () => Attach(page));
            return;
        }
        _canvas = canvas;
        _start = Stopwatch.GetTimestamp();
        canvas.WillDraw += OnWillDraw;
        canvas.WasDrawn += OnWasDrawn;
        Console.WriteLine("[perf] measuring: 100 mobs, 2 s warm-up, 4 s of frames");
    }

    static void OnWillDraw(object sender, SkiaDrawingContext ctx) => _frameStart = Stopwatch.GetTimestamp();

    static void OnWasDrawn(object sender, SkiaDrawingContext ctx)
    {
        var now = Stopwatch.GetTimestamp();
        var since = Stopwatch.GetElapsedTime(_start, now).TotalSeconds;
        if (since < WarmupSeconds || _frameStart == 0)
            return;
        if (since < WarmupSeconds + MeasureSeconds)
        {
            if (_count == 0)
                _first = now;
            _last = now;
            if (_count < Frames.Length)
                Frames[_count++] = Stopwatch.GetElapsedTime(_frameStart, now).TotalMilliseconds;
            return;
        }
        _canvas.WillDraw -= OnWillDraw;
        _canvas.WasDrawn -= OnWasDrawn;
        var sorted = Frames.AsSpan(0, _count).ToArray();
        Array.Sort(sorted);
        var fps = (_count - 1) / Stopwatch.GetElapsedTime(_first, _last).TotalSeconds;
        Console.WriteLine($"[perf] frames {_count}, {fps:0.0} FPS, frame CPU median {sorted[_count / 2]:0.00} ms, p95 {sorted[(int)(_count * 0.95)]:0.00} ms, max {sorted[^1]:0.00} ms");
    }
}
