using Newtonsoft.Json.Linq;

namespace DrawnUi.Draw;

/// <summary>
/// A texture together with WHERE it sits, in canvas pixels. The two halves are inseparable:
/// a cache surface is recorded over <see cref="CachedObject.Bounds"/>, which can be inflated
/// beyond the control box by effects/shadow margins, and <see cref="SkiaImage"/> hands over
/// its rescaled source, rasterized at DISPLAY size — neither starts at DrawingRect. Consumers
/// that sample the texture by screen coordinates (post-renderer effects) get it wrong unless
/// the origin travels with the image.
/// </summary>
public readonly struct CachedTexture
{
    public CachedTexture(SKImage image, SKRect bounds)
    {
        Image = image;
        Bounds = bounds;
    }

    public readonly SKImage Image;

    /// <summary>Canvas-space rect the texture was rasterized over. Its top-left is the texel (0,0).</summary>
    public readonly SKRect Bounds;

    public bool IsValid => Image != null && Image.Handle != 0;

    public static CachedTexture None => default;
}

public class CachedObject : ISkiaDisposable
{
    public int DataContext { get; set; }

    /// <summary>
    /// Logical (unexpanded) rect the content was recorded for. For image-backed caches
    /// <see cref="Bounds"/> is the surface area inflated by effects/shadow margins
    /// (see AggregatedEffectsMarginPixels) — any gesture translation or position-delta math
    /// must use THIS as the recorded origin, otherwise coordinates shift by the margin.
    /// For Operations caches Bounds is already logical (the destination), kept as-is.
    /// </summary>
    public SKRect LogicalBounds => Picture != null ? Bounds : RecordingArea;

    public SKPoint TranslateInputCoords(SKRect drawingRect)
    {
        // Use LastDestination (actual screen position where cache was drawn) if available
        var current = LastDestination.IsEmpty ? drawingRect : LastDestination;

        var offsetCacheX = current.Left - LogicalBounds.Left;
        var offsetCacheY = current.Top - LogicalBounds.Top;

        return new SKPoint(-offsetCacheX, -offsetCacheY);
    }

    public SKPoint CalculatePositionOffset(SKPoint drawingRect)
    {
        var offsetCacheX = drawingRect.X - LogicalBounds.Left;
        var offsetCacheY = drawingRect.Y - LogicalBounds.Top;

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
        // Picture-backed caches (Operations AND OperationsFull) record in ABSOLUTE coords
        // (no -recordArea translate, unlike image caches), so the recorded origin Bounds.Left
        // must be subtracted here. Keying on Picture != null covers both — OperationsFull was
        // previously excluded (Type == Operations only) and fell into the image branch, so its
        // absolute origin was never removed and content drew at ~0,0.
        if (Picture != null)
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

    /// <summary>
    /// A picture recorded to be drawn distorted (scaled, turned, skewed, projected): the images in it use smooth sampling.
    /// </summary>
    public bool PlaybackDistorted { get; set; }

    /// <summary>
    /// A picture that holds image blits, whose sampling was fixed when it was recorded.
    /// </summary>
    public bool HasImageBlits { get; set; }

    public SKPicture Picture { get; set; }

    public SKImage Image { get; set; }

    /// <summary>
    /// The picture of a picture-backed cache (Operations, OperationsFull) drawn into an image, made on the first
    /// call and kept as long as this cache lives. Shader effects read it as their input texture, so on such a
    /// control they work on its own content, as on an Image cache. Null for image-backed caches.
    /// </summary>
    /// <param name="context">GPU context to make the image on, null for a CPU image</param>
    public SKImage GetPictureImage(GRContext context)
    {
        if (_pictureImage != null || Picture == null || IsDisposed)
            return _pictureImage;

        var width = (int)Math.Ceiling(Bounds.Width);
        var height = (int)Math.Ceiling(Bounds.Height);
        if (width <= 0 || height <= 0)
            return null;

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = (context != null ? SKSurface.Create(context, false, info) : null) ?? SKSurface.Create(info);
        if (surface == null)
            return null;

        // drawn as Draw() would at a destination starting at 0,0: the image's origin is the control's top-left
        var drawAt = CalculateDrawingOffset(new SKRect(0, 0, Bounds.Width, Bounds.Height));
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawPicture(Picture, drawAt.X, drawAt.Y);
        _pictureImage = surface.Snapshot();
        return _pictureImage;
    }

    private SKImage _pictureImage;

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
            _pictureImage?.Dispose();
            _pictureImage = null;
 
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
