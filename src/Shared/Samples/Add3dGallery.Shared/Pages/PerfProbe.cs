using System.Diagnostics;

namespace Add3dGallery;

/// <summary>
/// Measures the gallery in the browser, results in the console. Address options:
/// <c>?perf</c> = the top of the page at rest, <c>?perf=scroll</c> = the page scrolled down at a steady 600 pt/s,
/// <c>?perf=list</c> = the 3D product list scrolled, <c>?perf=mobs</c> = 100 mobs on the field, scrolled to it
/// (<c>?perf=mobsdrawn</c>: the same in the Drawn look),
/// <c>?perf=globe</c> = the turning globe with its pins at the top (<c>?perf=globechip</c>: with a pin's name chip open; <c>globechipraw</c>: the chip drawn live, not cached).
/// Per frame: the CPU time of drawing (canvas WillDraw to WasDrawn: layout, drawing, the 3D GL calls; Skia's GPU flush
/// comes after it), the managed bytes allocated on the UI thread from one frame to the next, and the frame interval.
/// 2 s warm-up, then 5 s of frames: median, p99, frames over 12 ms, allocations per frame, gen-0 collections.
/// </summary>
public static class PerfProbe
{
    const double WarmupSeconds = 2, MeasureSeconds = 5;

    static readonly double[] Draw = new double[2048], Alloc = new double[2048], At = new double[2048];
    static int _count;
    static long _start, _frameStart, _first, _last, _lastAlloc;
    static int _gcAtFirst;
    static DrawnView _canvas;
    static GalleryPage _page;
    static string _mode;

    /// <param name="query">The address's query string; the word after <c>perf=</c> picks the run.</param>
    public static void Start(GalleryPage page, string query)
    {
        _page = page;
        var at = query.IndexOf("perf=", StringComparison.OrdinalIgnoreCase);
        _mode = at < 0 ? "rest" : query[(at + 5)..].Split('&')[0].ToLowerInvariant();
        if (_mode is "mobs" or "mobsdrawn")
            page.SetMobs(100);
        if (_mode == "mobsdrawn")
            page.SetDrawnMobs(true);
        Tasks.StartDelayed(TimeSpan.FromMilliseconds(1500), Attach);
    }

    static void Attach()
    {
        if (_page.Superview is not DrawnView canvas)
        {
            Tasks.StartDelayed(TimeSpan.FromMilliseconds(500), Attach);
            return;
        }
        _canvas = canvas;
        if (_mode == "globetaps")
        {
            GlobeTaps();
            return;
        }
        if (_mode is "mobs" or "mobsdrawn")
            _page.ProbeScroll(false, -1);
        else if (_mode is "globe" or "globechip" or "globechipraw")
        {
            GalleryPage.ProbeChipsUncached = _mode == "globechipraw";
            _page.ProbeShowGlobe(_mode != "globe");
        }
        _start = Stopwatch.GetTimestamp();
        canvas.WillDraw += OnWillDraw;
        canvas.WasDrawn += OnWasDrawn;
        Console.WriteLine($"[perf] {_mode}: {WarmupSeconds} s warm-up, {MeasureSeconds} s of frames");
    }

    /// <summary>
    /// globetaps: the globe at the top, then batches of taps on the city list (1, 5, 10, 20, 40 in all, a tap every
    /// 0.7 s); after each batch 2 s of frames: FPS, draw median, and the canvas's running animators.
    /// </summary>
    static void GlobeTaps()
    {
        _page.ProbeShowGlobe();
        var frames = new List<double>();
        long start = 0, frameStart = 0;
        var measuring = false;
        _canvas.WillDraw += (_, _) => frameStart = Stopwatch.GetTimestamp();
        _canvas.WasDrawn += (_, _) =>
        {
            if (measuring && frameStart != 0)
                frames.Add(Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds);
        };
        int[] batches = { 1, 5, 10, 20, 40 };
        var taps = 0;
        var batch = 0;
        void Next()
        {
            if (batch >= batches.Length)
            {
                Console.WriteLine("[perf] globetaps done");
                return;
            }
            if (taps < batches[batch])
            {
                _page.ProbeTapCity(taps);
                taps++;
                Tasks.StartDelayed(TimeSpan.FromMilliseconds(700), Next);
                return;
            }
            frames.Clear();
            start = Stopwatch.GetTimestamp();
            measuring = true;
            Tasks.StartDelayed(TimeSpan.FromSeconds(2), () =>
            {
                measuring = false;
                var f = frames.ToArray();
                Array.Sort(f);
                var fps = f.Length / Stopwatch.GetElapsedTime(start).TotalSeconds;
                Console.WriteLine($"[perf] globetaps after {taps} taps: {fps:0.0} FPS, draw median {(f.Length > 0 ? f[f.Length / 2] : 0):0.00} ms, animators {_canvas.AnimatingControls.Count}, {_page.ProbeGlobeCounts()}");
                batch++;
                Next();
            });
        }
        Tasks.StartDelayed(TimeSpan.FromSeconds(2), Next);
    }

    static void OnWillDraw(object sender, SkiaDrawingContext ctx)
    {
        _frameStart = Stopwatch.GetTimestamp();
        var since = Stopwatch.GetElapsedTime(_start, _frameStart).TotalSeconds;
        // Steady scrolling from the end of the warm-up: 600 points a second, 10 per frame at 60 FPS.
        if (since >= WarmupSeconds && _mode is "scroll" or "list")
            _page.ProbeScroll(_mode == "list", (float)(since - WarmupSeconds) * 600);
    }

    static void OnWasDrawn(object sender, SkiaDrawingContext ctx)
    {
        var now = Stopwatch.GetTimestamp();
        var alloc = GC.GetAllocatedBytesForCurrentThread();
        var since = Stopwatch.GetElapsedTime(_start, now).TotalSeconds;
        if (since < WarmupSeconds || _frameStart == 0)
        {
            _lastAlloc = alloc;
            return;
        }
        if (since < WarmupSeconds + MeasureSeconds)
        {
            if (_count == 0)
            {
                _first = now;
                _gcAtFirst = GC.CollectionCount(0);
            }
            _last = now;
            if (_count < Draw.Length)
            {
                Draw[_count] = Stopwatch.GetElapsedTime(_frameStart, now).TotalMilliseconds;
                At[_count] = since - WarmupSeconds;
                Alloc[_count] = alloc - _lastAlloc;
                _count++;
            }
            _lastAlloc = alloc;
            return;
        }
        _canvas.WillDraw -= OnWillDraw;
        _canvas.WasDrawn -= OnWasDrawn;
        var draw = Draw.AsSpan(0, _count).ToArray();
        var alloc2 = Alloc.AsSpan(0, _count).ToArray();
        var over = draw.Count(x => x > 12);
        // The three slowest frames and when they came (seconds into the measured run): one-time costs show here.
        var slowest = string.Join(", ", Enumerable.Range(0, _count).OrderByDescending(i => Draw[i]).Take(3).Select(i => $"{Draw[i]:0.0} ms at {At[i]:0.00} s"));
        Array.Sort(draw);
        Array.Sort(alloc2);
        var fps = (_count - 1) / Stopwatch.GetElapsedTime(_first, _last).TotalSeconds;
        Console.WriteLine($"[perf] {_mode}: frames {_count}, {fps:0.0} FPS, draw median {draw[_count / 2]:0.00} ms, p99 {draw[(int)(_count * 0.99)]:0.00} ms, max {draw[^1]:0.00} ms, over 12 ms {over}, alloc/frame median {alloc2[_count / 2] / 1024:0.0} KB, p99 {alloc2[(int)(_count * 0.99)] / 1024:0.0} KB, GC {GC.CollectionCount(0) - _gcAtFirst}, slowest {slowest}");
    }
}
