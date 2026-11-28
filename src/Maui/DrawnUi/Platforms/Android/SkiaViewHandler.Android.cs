using Android.Content;
using Android.Graphics;
using SkiaSharp.Views.Maui.Handlers;
using AndroidCanvas = Android.Graphics.Canvas;
using AndroidSKCanvasView = SkiaSharp.Views.Android.SKCanvasView;
using AndroidPaintArgs = SkiaSharp.Views.Android.SKPaintSurfaceEventArgs;

namespace DrawnUi.Views;

/// <summary>
/// Handler of the raster <see cref="SkiaView"/> on Android: SkiaSharp's own handler with a platform view that keeps
/// its bitmap for the whole frame.
/// </summary>
public class SkiaViewHandler : SKCanvasViewHandler
{
    protected override AndroidSKCanvasView CreatePlatformView() => new FrameSafeCanvasView(Context);
}

/// <summary>
/// SkiaSharp's Android canvas view, drawn into a bitmap it owns. The paint handler may detach, dispose or resize the
/// view inside the frame: a tap drawn on this canvas runs on the UI thread, and navigating from it removes the page
/// at once. SkiaSharp's view frees its bitmap right there and then shows the frame from the freed bitmap (a
/// NullReferenceException in SurfaceFactory.DrawSurface); this one frees it after the frame.
/// </summary>
public class FrameSafeCanvasView : AndroidSKCanvasView
{
    private Bitmap? _bitmap;
    private bool _drawing, _freeAfterFrame;
    private readonly float _density;

    public FrameSafeCanvasView(Context context) : base(context)
    {
        _density = Resources?.DisplayMetrics?.Density ?? 1f;
    }

    protected override void OnDraw(AndroidCanvas canvas)
    {
        // the base view would draw its own bitmap: everything happens here instead

        if (Visibility != Android.Views.ViewStates.Visible || Width == 0 || Height == 0)
        {
            FreeBitmap();
            return;
        }

        var info = new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        if (_bitmap == null || _bitmap.Handle == IntPtr.Zero || _bitmap.Width != info.Width || _bitmap.Height != info.Height)
        {
            FreeBitmap();
            _bitmap = Bitmap.CreateBitmap(info.Width, info.Height, Bitmap.Config.Argb8888!);
        }

        var bitmap = _bitmap!;
        var visible = IgnorePixelScaling
            ? new SKSizeI((int)(info.Width / _density), (int)(info.Height / _density))
            : info.Size;

        _drawing = true;
        try
        {
            using (var surface = SKSurface.Create(info, bitmap.LockPixels(), info.RowBytes))
            {
                if (IgnorePixelScaling)
                {
                    surface.Canvas.Scale(_density);
                    surface.Canvas.Save();
                }

                OnPaintSurface(new AndroidPaintArgs(surface, info.WithSize(visible), info));
                surface.Canvas.Flush();
            }
        }
        finally
        {
            bitmap.UnlockPixels();
            _drawing = false;
        }

        if (_freeAfterFrame)
        {
            // detached or disposed during the frame: nothing to show any more
            _freeAfterFrame = false;
            FreeBitmap();
            return;
        }

        canvas.DrawBitmap(bitmap, new Android.Graphics.Rect(0, 0, info.Width, info.Height), new Android.Graphics.RectF(0, 0, canvas.Width, canvas.Height), null);
    }

    protected override void OnDetachedFromWindow()
    {
        FreeBitmapAfterFrame();
        base.OnDetachedFromWindow();
    }

    protected override void Dispose(bool disposing)
    {
        FreeBitmapAfterFrame();
        base.Dispose(disposing);
    }

    private void FreeBitmapAfterFrame()
    {
        if (_drawing)
            _freeAfterFrame = true;
        else
            FreeBitmap();
    }

    private void FreeBitmap()
    {
        var bitmap = _bitmap;
        if (bitmap == null)
            return;
        _bitmap = null;
        if (bitmap.Handle != IntPtr.Zero && !bitmap.IsRecycled)
            bitmap.Recycle();
        bitmap.Dispose();
    }
}
