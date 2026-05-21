using DrawnUi.Draw;
using SkiaSharp;
using System.Diagnostics;

namespace DrawnUi.Blazor.Server;

internal sealed class HeadlessDrawable : ISkiaDrawable
{
    public HeadlessDrawable(SKSurface surface)
    {
        Surface = surface;
        CanvasSize = new SKSize(surface.Canvas.LocalClipBounds.Width, surface.Canvas.LocalClipBounds.Height);
        OnDraw = static (_, _) => false;
    }

    public SKSurface Surface { get; }

    public bool IsHardwareAccelerated => false;

    public double FPS => 60;

    public bool IsDrawing { get; private set; }

    public bool HasDrawn { get; private set; }

    public Guid Uid { get; } = Guid.NewGuid();

    public SKSize CanvasSize { get; }

    public GRContext GRContext => null!;

    public long FrameTime { get; private set; }

    public Func<SKSurface, SKRect, bool> OnDraw { get; set; }

    public void PrepareFrame()
    {
        PrepareFrame(0);
    }

    public void Dispose()
    {
    }

    public void Invalidate()
    {
    }

    public bool Update(long nanos = 0)
    {
        PrepareFrame(nanos);
        IsDrawing = true;
        try
        {
            HasDrawn = OnDraw?.Invoke(Surface, new SKRect(0, 0, CanvasSize.Width, CanvasSize.Height)) ?? false;
            return HasDrawn;
        }
        finally
        {
            IsDrawing = false;
        }
    }

    public void SignalFrame(long nanoseconds)
    {
        FrameTime = nanoseconds;
    }

    public void PrepareFrame(long nanos)
    {
        FrameTime = nanos > 0 ? nanos : GetFrameTimestampNanos();
    }

    private static long GetFrameTimestampNanos()
    {
        var timestamp = Stopwatch.GetTimestamp();
        return (long)(1_000_000_000.0 * timestamp / Stopwatch.Frequency);
    }
}