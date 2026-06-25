using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SkiaSharp;

namespace DrawnUi.Draw;

/// <summary>
/// SkiaSharp canvas view for pure WebAssembly (no Blazor).
/// Ported from SkiaSharp.Views.Blazor.SKCanvasView + SKGLView.
/// Implements ISkiaDrawable so it can be attached to DrawnView via AttachCanvasView,
/// making both RenderingMode.Default and RenderingMode.Accelerated work.
/// </summary>
[SupportedOSPlatform("browser")]
public class WebSkiaView : ISkiaDrawable, IDisposable
{
    private const int ResourceCacheBytes = 256 * 1024 * 1024; // 256 MB
    private const SKColorType ColorType = SKColorType.Rgba8888;
    private const GRSurfaceOrigin SurfaceOrigin = GRSurfaceOrigin.BottomLeft;

    private readonly string _elementId;
    private readonly Action _renderFrameCallback;

    // GL state
    private SkiaHtmlCanvasInterop.GLInfo? _glInfo;
    private GRGlInterface? _glInterface;
    private GRContext? _context;
    private GRBackendRenderTarget? _renderTarget;
    private SKSize _renderTargetSize;

    // Raster state
    private byte[]? _pixels;
    private GCHandle _pixelsHandle;
    private SKSizeI _pixelSize;

    // Shared
    private SKSurface? _surface;
    private SKCanvas? _canvas;
    private double _dpi = 1.0;
    private SKSize _canvasSize;
    private bool _enableRenderLoop;
    private bool _ignorePixelScaling;

    /// <summary>
    /// Whether to use GPU (WebGL) or raster (CPU) rendering.
    /// </summary>
    public bool IsHardwareAccelerated => _glInfo != null;

    public double Dpi => _dpi;
    public SKSize CanvasSize => _canvasSize;
    public SKSurface? Surface => _surface;
    public SKCanvas? Canvas => _canvas;
    public GRContext? GRContext => _context;

    /// <summary>
    /// Called when the surface needs to be painted. Return true if dirty.
    /// </summary>
    public Func<SKSurface, SKRect, bool>? OnDraw { get; set; }

    /// <summary>
    /// Create a new WebSkiaView for the given canvas element.
    /// </summary>
    /// <param name="elementId">HTML canvas element id</param>
    /// <param name="renderFrameCallback">C# method JS calls each frame (must be [JSExport] or resolvable)</param>
    public WebSkiaView(string elementId, Action renderFrameCallback)
    {
        _elementId = elementId;
        _renderFrameCallback = renderFrameCallback;
    }

    /// <summary>
    /// Initialize GPU (WebGL) rendering. Returns true on success.
    /// Mirrors SKGLView.OnAfterRenderAsync → interop.InitGL().
    /// </summary>
    public bool InitGL()
    {
        _glInfo = SkiaHtmlCanvasInterop.InitGL(_elementId, _renderFrameCallback);
        if (_glInfo == null)
        {
            Console.WriteLine("WebSkiaView: GL init failed, falling back to raster");
            return false;
        }
        Console.WriteLine($"WebSkiaView: GL init success (fbo={_glInfo.FboId}, stencil={_glInfo.Stencils})");
        return true;
    }

    /// <summary>
    /// Initialize raster (CPU) rendering.
    /// Mirrors SKCanvasView.OnAfterRenderAsync → interop.InitRaster().
    /// </summary>
    public bool InitRaster()
    {
        return SkiaHtmlCanvasInterop.InitRaster(_elementId, _renderFrameCallback);
    }

    /// <summary>
    /// Try GPU first, fall back to raster.
    /// </summary>
    public bool Init(bool preferGPU = true)
    {
        if (preferGPU && InitGL())
            return true;
        return InitRaster();
    }

    /// <summary>
    /// Request a frame render. Optionally set render loop + resize.
    /// </summary>
    public void Invalidate()
    {
        if (_canvasSize.Width <= 0 || _canvasSize.Height <= 0 || _dpi <= 0)
            return;

        var rawWidth = (int)(_canvasSize.Width * _dpi);
        var rawHeight = (int)(_canvasSize.Height * _dpi);
        SkiaHtmlCanvasInterop.RequestAnimationFrame(_elementId, _enableRenderLoop, rawWidth, rawHeight);
    }

    /// <summary>
    /// Enable/disable continuous render loop.
    /// </summary>
    public void SetEnableRenderLoop(bool enable)
    {
        _enableRenderLoop = enable;
        SkiaHtmlCanvasInterop.SetEnableRenderLoop(_elementId, enable);
    }

    /// <summary>
    /// Update DPI (called when device pixel ratio changes).
    /// </summary>
    public void SetDpi(double newDpi)
    {
        _dpi = newDpi;
        Invalidate();
    }

    /// <summary>
    /// Update canvas size (called when element resizes).
    /// </summary>
    public void SetSize(SKSize newSize)
    {
        _canvasSize = newSize;
        Invalidate();
    }

    /// <summary>
    /// Called by JS requestAnimationFrame each frame.
    /// Mirrors SKGLView.OnRenderFrame / SKCanvasView.OnRenderFrame.
    /// </summary>
    public void OnRenderFrame()
    {
        if (_canvasSize.Width <= 0 || _canvasSize.Height <= 0 || _dpi <= 0)
            return;

        if (_glInfo != null)
            RenderFrameGL();
        else
            RenderFrameRaster();
    }

    private void RenderFrameGL()
    {
        // Create GRContext (once)
        if (_context == null)
        {
            _glInterface = GRGlInterface.Create();
            _context = GRContext.CreateGl(_glInterface);
            _context?.SetResourceCacheLimit(ResourceCacheBytes);
        }

        var newSize = CreateSize(out var unscaledSize);
        var info = new SKImageInfo(newSize.Width, newSize.Height, ColorType);
        var userVisibleSize = _ignorePixelScaling ? unscaledSize : info.Size;

        // Recreate render target if size changed
        if (_renderTarget == null || _renderTargetSize != newSize || !_renderTarget.IsValid)
        {
            _renderTargetSize = newSize;

            var glInfo = new GRGlFramebufferInfo(_glInfo!.FboId, (uint)ColorType.ToGlSizedFormatWeb());

            _surface?.Dispose();
            _surface = null;
            _canvas = null;

            _renderTarget?.Dispose();
            _renderTarget = new GRBackendRenderTarget(newSize.Width, newSize.Height,
                _glInfo.Samples, _glInfo.Stencils, glInfo);
        }

        // Create surface
        if (_surface == null)
        {
            _surface = SKSurface.Create(_context, _renderTarget, SurfaceOrigin, ColorType);
            _canvas = _surface?.Canvas;
        }

        using (new SKAutoCanvasRestore(_canvas, true))
        {
            if (_ignorePixelScaling)
            {
                _canvas.Scale((float)_dpi);
                _canvas.Save();
            }

            OnDraw?.Invoke(_surface, new SKRect(0, 0, userVisibleSize.Width, userVisibleSize.Height));
        }

        _canvas?.Flush();
        _context.Flush();
    }

    private void RenderFrameRaster()
    {
        var info = CreateBitmap(out var unscaledSize);
        var userVisibleSize = _ignorePixelScaling ? unscaledSize : info.Size;

        using (var surface = SKSurface.Create(info, _pixelsHandle.AddrOfPinnedObject(), info.RowBytes))
        {
            var canvas = surface.Canvas;

            if (_ignorePixelScaling)
            {
                canvas.Scale((float)_dpi);
                canvas.Save();
            }

            OnDraw?.Invoke(surface, new SKRect(0, 0, userVisibleSize.Width, userVisibleSize.Height));
        }

        // Blit to HTML canvas
        SkiaHtmlCanvasInterop.PutImageData(_elementId, _pixels!, info.Width, info.Height);
    }

    private SKImageInfo CreateBitmap(out SKSizeI unscaledSize)
    {
        var size = CreateSize(out unscaledSize);
        var info = new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Opaque);

        if (_pixels == null || _pixelSize.Width != info.Width || _pixelSize.Height != info.Height)
        {
            FreeBitmap();

            _pixels = new byte[info.BytesSize];
            _pixelsHandle = GCHandle.Alloc(_pixels, GCHandleType.Pinned);
            _pixelSize = info.Size;
        }

        return info;
    }

    private SKSizeI CreateSize(out SKSizeI unscaledSize)
    {
        unscaledSize = SKSizeI.Empty;

        var w = _canvasSize.Width;
        var h = _canvasSize.Height;

        if (!IsPositive(w) || !IsPositive(h))
            return SKSizeI.Empty;

        unscaledSize = new SKSizeI((int)w, (int)h);
        return new SKSizeI((int)(w * _dpi), (int)(h * _dpi));

        static bool IsPositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
        }
    }

    private void FreeBitmap()
    {
        if (_pixels != null)
        {
            _pixelsHandle.Free();
            _pixels = null;
        }
    }

    public void Dispose()
    {
        _canvas?.Dispose();
        _surface?.Dispose();
        _renderTarget?.Dispose();
        _context?.Dispose();
        _glInterface?.Dispose();
        FreeBitmap();
        SkiaHtmlCanvasInterop.Deinit(_elementId);
    }

    // --- ISkiaDrawable implementation (so DrawnView.AttachCanvasView works) ---

    public Guid Uid { get; } = Guid.NewGuid();

    // IsHardwareAccelerated already defined above

    public double FPS { get; private set; }

    private long _lastFrameNanos;

    public bool IsDrawing => false;

    public bool HasDrawn => _surface != null;

    public long FrameTime { get; private set; }

    // CanvasSize already defined above

    Func<SKSurface, SKRect, bool> ISkiaDrawable.OnDraw
    {
        get => OnDraw;
        set => OnDraw = value;
    }

    SKSurface ISkiaDrawable.Surface => _surface!;

    public bool Update(long nanos = 0)
    {
        if (nanos > 0)
            FrameTime = nanos;
        Invalidate();
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
}

/// <summary>
/// Extension for GL sized format (replaces SkiaSharp.Views ToGlSizedFormat).
/// </summary>
internal static class SKColorTypeExtensions
{
    /// <summary>
    /// Convert SKColorType to GL sized internal format.
    /// Mirrors SkiaSharp.Views.Shared SKColorTypeExtensions.ToGlSizedFormat.
    /// </summary>
    public static int ToGlSizedFormatWeb(this SKColorType colorType)
    {
        // GL_RGBA8 = 0x8058, GL_RGB8 = 0x8051, GL_BGRA8_EXT = 0x93A1
        return colorType switch
        {
            SKColorType.Rgba8888 => 0x8058,   // GL_RGBA8
            SKColorType.Bgra8888 => 0x93A1,   // GL_BGRA8_EXT
            SKColorType.Rgb888x => 0x8058,    // GL_RGBA8 (treat as RGBA)
            _ => 0x8058,                       // default to GL_RGBA8
        };
    }
}
