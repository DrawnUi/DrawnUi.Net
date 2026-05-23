using DrawnUi.Draw;
using SkiaSharp;

namespace DrawnUi.OpenTk;

public sealed class GpuDrawable : ISkiaDrawable
{
    public Func<SKSurface, SKRect, bool> OnDraw { get; set; } = static (_, _) => false;

    public SKSurface? Surface { get; set; }

    SKSurface ISkiaDrawable.Surface => Surface!;

    public bool IsHardwareAccelerated => true;

    public double FPS { get; private set; }

    public bool IsDrawing => false;

    public bool HasDrawn => Surface != null;

    public long FrameTime { get; private set; }

    public Guid Uid { get; } = Guid.NewGuid();

    public SKSize CanvasSize { get; set; }

    private long _lastFrameNanos;

    public bool Update(long nanos = 0)
    {
        if (nanos > 0)
            FrameTime = nanos;

        return true;
    }

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

    public void Dispose() { }
}
