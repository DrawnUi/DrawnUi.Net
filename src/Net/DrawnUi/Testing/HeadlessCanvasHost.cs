using System.Diagnostics;
using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;

namespace DrawnUi.Testing;

/// <summary>
/// Headless DrawnUI host for automated testing. Renders a <see cref="Canvas"/> to an off-screen
/// SkiaSharp surface and pumps frames synchronously on a deterministic monotonic clock, so gesture
/// delivery (flushed in <c>ExecuteBeforeDraw</c>) and animations/fling (ticked in <c>ExecuteAnimators</c>
/// off <c>FrameTimeNanos</c>) advance predictably without a window or render loop.
///
/// Pair with <see cref="GestureRobot"/> to simulate taps, panning, flinging and wheel scrolling.
/// </summary>
public sealed class HeadlessCanvasHost : IDisposable
{
    private static readonly object _superLock = new();
    private static bool _superInitialized;

    private readonly SKSurface _surface;
    private readonly HeadlessDrawable _drawable;
    private long _frameNanos;

    /// <param name="width">Surface width in pixels.</param>
    /// <param name="height">Surface height in pixels.</param>
    /// <param name="scale">Rendering scale (density). 1 means points == pixels.</param>
    /// <param name="background">Canvas background color. Defaults to transparent.</param>
    public HeadlessCanvasHost(int width, int height, float scale = 1f, DrawnUi.Color? background = null)
    {
        lock (_superLock)
        {
            if (!_superInitialized)
            {
                Super.Init();
                _superInitialized = true;
            }
        }

        _surface = SKSurface.Create(new SKImageInfo(width, height));
        _drawable = new HeadlessDrawable(_surface);

        Canvas = new Canvas
        {
            WidthRequest = width / scale,
            HeightRequest = height / scale,
            BackgroundColor = background ?? Colors.Transparent
        };

        Canvas.AttachCanvasView(_drawable);
        Canvas.ConnectedHandler();

        if (scale > 0)
            Canvas.RenderingScale = scale;
    }

    /// <summary>The hosted canvas. Set <see cref="Canvas.Content"/> or <c>Children</c> then call <see cref="RenderFrame"/>.</summary>
    public Canvas Canvas { get; }

    /// <summary>Current synthetic frame timestamp in nanoseconds.</summary>
    public long FrameTimeNanos => _frameNanos;

    /// <summary>Rendering scale (density) of the hosted canvas.</summary>
    public float Scale => (float)Canvas.RenderingScale;

    /// <summary>
    /// Advances the synthetic clock by <paramref name="advanceMs"/> and renders one frame.
    /// Flushes any pending gesture delivery and ticks one animation step.
    /// </summary>
    public void RenderFrame(double advanceMs = 16.0)
    {
        if (advanceMs > 0)
            _frameNanos += (long)(advanceMs * 1_000_000.0);

        _drawable.PrepareFrame(_frameNanos);
        Canvas.RenderExternalSurface(
            _surface,
            new SKRect(0, 0, _drawable.CanvasSize.Width, _drawable.CanvasSize.Height),
            _frameNanos);
    }

    /// <summary>Renders <paramref name="count"/> frames, each advancing the clock by <paramref name="advanceMs"/>.</summary>
    public void AdvanceFrames(int count, double advanceMs = 16.0)
    {
        for (var i = 0; i < count; i++)
            RenderFrame(advanceMs);
    }

    /// <summary>Saves the current surface contents to a PNG file (useful for visual diffing).</summary>
    public void SavePng(string filePath)
    {
        using var image = _surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }

    public void Dispose()
    {
        Canvas.Dispose();
        _drawable.Dispose();
        _surface.Dispose();
    }

    private sealed class HeadlessDrawable : ISkiaDrawable
    {
        public HeadlessDrawable(SKSurface surface)
        {
            Surface = surface;
            CanvasSize = new SKSize(surface.Canvas.LocalClipBounds.Width, surface.Canvas.LocalClipBounds.Height);
            OnDraw = static (_, _) => false;
        }

        public Func<SKSurface, SKRect, bool> OnDraw { get; set; }
        public SKSurface Surface { get; }
        public bool IsHardwareAccelerated => false;
        public double FPS => 60;
        public bool IsDrawing { get; private set; }
        public bool HasDrawn { get; private set; }
        public long FrameTime { get; private set; }
        public Guid Uid { get; } = Guid.NewGuid();
        public SKSize CanvasSize { get; }

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

        public void SignalFrame(long nanoseconds) => FrameTime = nanoseconds;

        public void PrepareFrame(long nanos = 0) =>
            FrameTime = nanos > 0 ? nanos : GetFrameTimestampNanos();

        public void Dispose() { }

        private static long GetFrameTimestampNanos()
        {
            var timestamp = Stopwatch.GetTimestamp();
            return (long)(1_000_000_000.0 * timestamp / Stopwatch.Frequency);
        }
    }
}
