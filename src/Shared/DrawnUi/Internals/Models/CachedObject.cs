using Newtonsoft.Json.Linq;

namespace DrawnUi.Draw;

public class CachedObject : ISkiaDisposable
{
    public int DataContext { get; set; }

    public SKPoint TranslateInputCoords(SKRect drawingRect)
    {
        // Use LastDestination (actual screen position where cache was drawn) if available
        var current = LastDestination.IsEmpty ? drawingRect : LastDestination;

        var offsetCacheX = current.Left - Bounds.Left;
        var offsetCacheY = current.Top - Bounds.Top;

        return new SKPoint(-offsetCacheX, -offsetCacheY);
    }

    public SKPoint CalculatePositionOffset(SKPoint drawingRect)
    {
        var offsetCacheX = drawingRect.X - Bounds.Left;
        var offsetCacheY = drawingRect.Y - Bounds.Top;

        return new SKPoint(offsetCacheX, offsetCacheY);
    }

    public SKPoint Test(SKRect drawingRect)
    {

        var offsetCacheX = Math.Abs(drawingRect.Left - Bounds.Left);
        var offsetCacheY = Math.Abs(drawingRect.Top - Bounds.Top);

        return new SKPoint(offsetCacheX, offsetCacheY);
    }

    /// <summary>
    /// Calculate offset where cache should be drawn
    /// </summary>
    /// <param name="destination"></param>
    /// <returns></returns>
    public SKPoint CalculateDrawingOffset(SKRect destination)
    {
        if (Type == SkiaCacheType.Operations)
        {
            var moveY = Bounds.Top - RecordingArea.Top;
            var moveX = Bounds.Left - RecordingArea.Left;
            var x = (float)(destination.Left - Bounds.Left + moveX);
            var y = (float)(destination.Top - Bounds.Top + moveY);
            return new SKPoint(x, y);
        }
        else
        {
            var moveY = Bounds.Top - RecordingArea.Top;
            var moveX = Bounds.Left - RecordingArea.Left;
            var x = (float)(destination.Left + moveX);
            var y = (float)(destination.Top + moveY);
            return new SKPoint(x, y);
        }
    }

    /// <summary>
    /// This will draw with destination corrected by offset that it had when was recorded
    /// </summary>
    /// <param name="canvas"></param>
    /// <param name="destination"></param>
    /// <param name="paint"></param>
    public void Draw(SKCanvas canvas, SKRect destination, SKPaint paint, FilterQuality quality)
    {
        Draw(canvas, destination, paint, SkiaSamplingOptions.GetSamplingOptions(quality, false));
    }

    /// <summary>
    /// This will draw with destination corrected by offset that it had when was recorded.
    /// Sampling applies to image-backed caches only; pass SkiaSamplingOptions.LinearNoMip when the canvas has scale/rotation transforms.
    /// </summary>
    /// <param name="canvas"></param>
    /// <param name="destination"></param>
    /// <param name="paint"></param>
    /// <param name="sampling"></param>
    public void Draw(SKCanvas canvas, SKRect destination, SKPaint paint, in SKSamplingOptions sampling)
    {
        LastDestination = destination;
        var drawAt = CalculateDrawingOffset(destination);

        try
        {
            if (Picture != null)
            {
                canvas.DrawPicture(Picture, drawAt.X, drawAt.Y, paint);
                LastDrawnAt = new(drawAt.X, drawAt.Y, Bounds.Width + drawAt.X, Bounds.Height + drawAt.Y);
            }
            else
            if (Image != null)
            {
                canvas.DrawImage(Image, drawAt.X, drawAt.Y, sampling, paint);
                LastDrawnAt = new(drawAt.X, drawAt.Y, Bounds.Width + drawAt.X, Bounds.Height + drawAt.Y);
            }
        }
        catch (Exception e)
        {
            Super.Log(e);
        }
    }

    /// <summary>
    /// Stores the canvas-relative draw position(x, y), not the actual destination rect, these are relative offsets for the drawing operation, not absolute screen position.
    /// </summary>
    public SKRect LastDrawnAt;

    /// <summary>
    /// Actual destination rect where cache was drawn (for gesture coordinate translation)
    /// </summary>
    public SKRect LastDestination;

    /// <summary>
    /// Will draw at exact x,y coordinated without any adjustments
    /// </summary>
    /// <param name="canvas"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="paint"></param>
    public void Draw(SKCanvas canvas, float x, float y, SKPaint paint, FilterQuality quality)
    {
        Draw(canvas, x, y, paint, SkiaSamplingOptions.GetSamplingOptions(quality, false));
    }

    /// <summary>
    /// Will draw at exact x,y coordinated without any adjustments.
    /// Sampling applies to image-backed caches only; pass SkiaSamplingOptions.LinearNoMip when the canvas has scale/rotation transforms.
    /// </summary>
    /// <param name="canvas"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="paint"></param>
    /// <param name="sampling"></param>
    public void Draw(SKCanvas canvas, float x, float y, SKPaint paint, in SKSamplingOptions sampling)
    {
        var destination = new SKRect(x, y, Bounds.Width + x, Bounds.Height + y);
        LastDestination = destination;

        try
        {
            if (Picture != null)
            {
                canvas.DrawPicture(Picture, x, y, paint);
                LastDrawnAt = new(x, y, Bounds.Width + x, Bounds.Height + y);
            }
            else
            if (Image != null)
            {
                canvas.DrawImage(Image, x, y, sampling, paint);
                LastDrawnAt = new(x, y, Bounds.Width + x, Bounds.Height + y);


                if (Surface != null && Surface.Context != null)
                {
                    //GPU
                    //canvas.Flush();
                }
            }
        }
        catch (Exception e)
        {
            Super.Log(e);
        }
    }

    public CachedObject(SkiaCacheType type, SKPicture picture, SKRect bounds, SKRect recordingArea)
    {
        Type = type;
        Bounds = bounds;
        RecordingArea = recordingArea;
        Picture = picture;
    }

    public CachedObject(SkiaCacheType type, SKSurface surface, SKRect bounds, SKRect recordingArea)
    {
        Type = type;
        Surface = surface;
        Bounds = bounds;
        RecordingArea = recordingArea;
        Image = surface.Snapshot();
    }

    /// <summary>
    /// Image-only cache (no surface, no picture) — e.g. a plane rasterized OFF-thread into a worker-owned
    /// reusable surface whose snapshot is handed over; the object owns and disposes the image.
    /// </summary>
    public CachedObject(SkiaCacheType type, SKImage image, SKRect bounds, SKRect recordingArea)
    {
        Type = type;
        Image = image;
        Bounds = bounds;
        RecordingArea = recordingArea;
    }

    public Guid Id = Guid.NewGuid();
    private SKSurface surface;

    /// <summary>
    /// An existing surface was reused for creating this object
    /// </summary>
    public bool SurfaceIsRecycled { get; set; }

    public SKPicture Picture { get; set; }

    public SKImage Image { get; set; }

    public SKRect Bounds { get; set; }

    public SKRect RecordingArea { get; set; }

    public SkiaCacheType Type { get; protected set; }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;

            IsAlive = ObjectAliveType.Disposed;

            if (!PreserveSourceFromDispose)
            {
                Surface?.Dispose();
            }

            Picture?.Dispose();
            Image?.Dispose(); // todo crash was here
 
            Surface = null;
            Picture = null;
            Image = null;
        }
    }

    public bool IsDisposed { get; protected set; }

    public string Tag { get; set; }

    public bool PreserveSourceFromDispose { get; set; }

    public SKBitmap GetBitmap()
    {
        return SKBitmap.FromImage(Image);
    }

    public SKSurface Surface { get; set; }
   
    public List<VisualLayer> Children { get; set; }

    public ObjectAliveType IsAlive { get; set; }
}
