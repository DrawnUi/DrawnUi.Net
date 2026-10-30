using DrawnUi.Draw;
using SkiaSharp;

namespace DrawnUi.Slint;

/// <summary>The raster surface that <see cref="SlintCanvasHost"/> renders the canvas into.</summary>
internal sealed class SlintDrawable : ISkiaDrawable
{
    public Func<SKSurface, SKRect, bool> OnDraw { get; set; } = static (_, _) => false;
    public SKSurface Surface { get; set; } = null!;
    public bool IsHardwareAccelerated => false;
    public double FPS => Super.RefreshRate;
    public bool IsDrawing { get; set; }
    public bool HasDrawn { get; set; }
    public long FrameTime { get; private set; }
    public Guid Uid { get; } = Guid.NewGuid();
    public SKSize CanvasSize { get; set; }

    // The host draws through Canvas.RenderExternalSurface on its own frame timer.
    public bool Update(long nanos = 0) => true;

    public void SignalFrame(long nanoseconds) => FrameTime = nanoseconds;

    public void Dispose() { }
}
