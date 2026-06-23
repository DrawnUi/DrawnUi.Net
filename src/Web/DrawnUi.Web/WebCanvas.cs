using System.Runtime.InteropServices;
using SkiaSharp;

namespace DrawnUi.Draw;

/// <summary>
/// Canvas host for DrawnUi.Web - software rendering via pinned pixel buffer.
/// Mirrors SkiaSharp.Views.Blazor.SKCanvasView: pin a byte[], create SKSurface
/// directly on the pinned pointer, draw, then putImageData to the HTML canvas.
/// </summary>
public class WebCanvas : IDisposable
{
    private SKSurface? _surface;
    private SKImageInfo _imageInfo;
    private bool _disposed;

    // Pinned pixel buffer (SKCanvasView pattern)
    private byte[]? _pixels;
    private GCHandle _pixelsHandle;
    private SKSizeI _pixelSize;

    public SKCanvas? Canvas => _surface?.Canvas;
    public SKSurface? Surface => _surface;
    public int Width => _imageInfo.Width;
    public int Height => _imageInfo.Height;
    public double DevicePixelRatio { get; private set; }

    /// <summary>
    /// Pointer to the pinned pixel buffer (for JS putImageData).
    /// </summary>
    public IntPtr PixelPointer => _pixelsHandle.AddrOfPinnedObject();

    /// <summary>
    /// The pinned pixel buffer (for JS putImageData when passing byte[] directly).
    /// </summary>
    public byte[]? Pixels => _pixels;

    public WebCanvas(int cssWidth, int cssHeight, double devicePixelRatio = 1.0)
    {
        DevicePixelRatio = devicePixelRatio;
        Resize(cssWidth, cssHeight);
    }

    public void Resize(int cssWidth, int cssHeight)
    {
        var physicalWidth = Math.Max(1, (int)(cssWidth * DevicePixelRatio));
        var physicalHeight = Math.Max(1, (int)(cssHeight * DevicePixelRatio));

        var newSize = new SKSizeI(physicalWidth, physicalHeight);

        // Reuse buffer if size unchanged
        if (_pixels != null && _pixelSize == newSize)
            return;

        // Free old buffer
        if (_pixels != null)
        {
            _pixelsHandle.Free();
            _pixels = null;
        }

        _imageInfo = new SKImageInfo(physicalWidth, physicalHeight, SKImageInfo.PlatformColorType, SKAlphaType.Opaque);

        // Allocate + pin (SKCanvasView.CreateBitmap pattern)
        _pixels = new byte[_imageInfo.BytesSize];
        _pixelsHandle = GCHandle.Alloc(_pixels, GCHandleType.Pinned);
        _pixelSize = newSize;

        // Create surface directly on the pinned buffer (no copy!)
        _surface?.Dispose();
        _surface = SKSurface.Create(_imageInfo, _pixelsHandle.AddrOfPinnedObject(), _imageInfo.RowBytes);

        // Scale so drawing uses CSS pixels
        _surface?.Canvas.Scale((float)DevicePixelRatio);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _surface?.Dispose();
            if (_pixels != null)
            {
                _pixelsHandle.Free();
                _pixels = null;
            }
            _disposed = true;
        }
    }
}
