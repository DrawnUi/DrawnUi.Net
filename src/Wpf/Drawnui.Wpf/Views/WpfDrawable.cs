using DrawnUi.Draw;
using SkiaSharp;

namespace DrawnUi.Wpf;

/// <summary>
/// The surface DrawnUI paints into on the WPF head. Backed by a
/// <see cref="System.Windows.Media.Imaging.WriteableBitmap"/> owned by <see cref="DrawnUiElement"/>,
/// so this is a CPU raster target: it composites correctly with the rest of the WPF tree, unlike an
/// airspace-bound GL child window.
/// </summary>
public sealed class WpfDrawable : ISkiaDrawable
{
    /// <summary>Not used by this host — the element drives painting directly.</summary>
    public Func<SKSurface, SKRect, bool> OnDraw { get; set; } = static (_, _) => false;

    /// <summary>Surface created over the host bitmap's back buffer.</summary>
    public SKSurface Surface { get; set; }

    SKSurface ISkiaDrawable.Surface => Surface;

    /// <summary>Raster target, so no GPU acceleration is reported.</summary>
    public bool IsHardwareAccelerated => false;

    /// <summary>Smoothed frames per second.</summary>
    public double FPS { get; private set; }

    /// <summary>Painting is synchronous on the UI thread here.</summary>
    public bool IsDrawing => false;

    /// <summary>True once a surface exists.</summary>
    public bool HasDrawn => Surface != null;

    /// <summary>Timestamp of the frame being drawn, in nanoseconds.</summary>
    public long FrameTime { get; private set; }

    /// <summary>Identity of this drawable.</summary>
    public Guid Uid { get; } = Guid.NewGuid();

    /// <summary>Surface size in pixels.</summary>
    public SKSize CanvasSize { get; set; }

    private long _lastFrameNanos;

    /// <summary>Requests another frame. The host renders on every composition tick, so this only records the time.</summary>
    public bool Update(long nanos = 0)
    {
        if (nanos > 0)
            FrameTime = nanos;

        return true;
    }

    /// <summary>Records the frame timestamp and updates the FPS average.</summary>
    public void SignalFrame(long nanoseconds)
    {
        if (_lastFrameNanos > 0 && nanoseconds > _lastFrameNanos)
        {
            var delta = nanoseconds - _lastFrameNanos;
            var instant = 1_000_000_000.0 / delta;
            FPS = FPS * 0.9 + instant * 0.1; // exponential moving average
        }

        _lastFrameNanos = nanoseconds;
        FrameTime = nanoseconds;
    }

    /// <summary>The host owns the surface lifetime, so this does nothing.</summary>
    public void Dispose()
    {
    }
}
