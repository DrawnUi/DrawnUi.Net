using System.Diagnostics;

namespace DrawnUi.Draw;

/// <summary>
/// Warning with CPU-rendering edges will not be blurred: https://issues.skia.org/issues/40036320
/// </summary>
public class SkiaBackdrop : ContentLayout, ISkiaGestureListener
{
    public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
    {
        return base.OnMeasuring(widthConstraint, heightConstraint, scale);
    }

    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        return this;
    }

    protected SKPaint ImagePaint;
    protected SKImageFilter PaintImageFilter;
    protected SKColorFilter PaintColorFilter;

    protected bool NeedInvalidateImageFilter { get; set; }
    protected bool NeedInvalidateColorFilter { get; set; }

    public virtual void InvalidateImageFilter()
    {
        NeedInvalidateImageFilter = true;
    }

    public virtual void InvalidateColorFilter()
    {
        NeedInvalidateColorFilter = true;
    }

    private static void NeedChangeImageFilter(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaBackdrop control)
        {
            control.InvalidateImageFilter();
            NeedDraw(bindable, oldvalue, newvalue);
        }
    }

    private static void NeedChangeColorFilter(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaBackdrop control)
        {
            control.InvalidateColorFilter();
            NeedDraw(bindable, oldvalue, newvalue);
        }
    }

    public static readonly BindableProperty BlurProperty = BindableProperty.Create(
        nameof(Blur),
        typeof(double),
        typeof(SkiaBackdrop),
        5.0,
        propertyChanged: NeedChangeImageFilter);

    public double Blur
    {
        get { return (double)GetValue(BlurProperty); }
        set { SetValue(BlurProperty, value); }
    }

    public static readonly BindableProperty BrightnessProperty = BindableProperty.Create(
        nameof(Brightness),
        typeof(double),
        typeof(SkiaBackdrop),
        1.0,
        propertyChanged: NeedChangeImageFilter);

    public double Brightness
    {
        get { return (double)GetValue(BrightnessProperty); }
        set { SetValue(BrightnessProperty, value); }
    }

    public static readonly BindableProperty UseContextProperty = BindableProperty.Create(nameof(UseContext),
        typeof(bool),
        typeof(SkiaControl),
        true,
        propertyChanged: NeedDraw);

    public bool UseContext
    {
        get { return (bool)GetValue(UseContextProperty); }
        set { SetValue(UseContextProperty, value); }
    }

    public bool HasEffects
    {
        get
        {
            return Blur != 0 || Brightness != 1;
        }
    }

    public override void OnDisposing()
    {
        base.OnDisposing();

        PaintImageFilter?.Dispose();
        PaintImageFilter = null;

        PaintColorFilter?.Dispose();
        PaintColorFilter = null;

        ImagePaint?.Dispose();
        ImagePaint = null;

        Snapshot?.Dispose();
        Snapshot = null;

        if (CacheSource != null)
        {
            CacheSource.CreatedCache -= OnSourceCacheChanged;
            CacheSource = null;
        }
    }

    public override bool CanUseCacheDoubleBuffering
    {
        get
        {
            return false;
        }
    }

    protected override void Paint(DrawingContext ctx)
    {
        if (IsDisposed || IsDisposing)
        {
            return;
        }

        var destination = ctx.Destination;

        SKImageFilter kill1 = null;
        SKColorFilter kill2 = null;

        if (NeedInvalidateImageFilter)
        {
            NeedInvalidateImageFilter = false;
            PaintImageFilter?.Dispose();
            PaintImageFilter = null;
        }
        else
        {
            kill1 = ImagePaint?.ImageFilter;
        }

        if (NeedInvalidateColorFilter)
        {
            NeedInvalidateColorFilter = false;
            PaintColorFilter?.Dispose();
            PaintColorFilter = null;
        }
        else
        {
            kill2 = ImagePaint?.ColorFilter;
        }

        if (destination.Width > 0 && destination.Height > 0)
        {
            DrawViews(ctx);

            PaintTintBackground(ctx.Context.Canvas, destination);

            BuildPaint();

            ImagePaint.ImageFilter = PaintImageFilter;
            ImagePaint.ColorFilter = PaintColorFilter;

            if (!IsGhost)
            {
                if (CacheSource != null)
                {
                    var cache = CacheSource.RenderObject;
                    if (cache != null && !IsGhost && HasEffects)
                    {
                        var offsetX = CacheSource.DrawingRect.Left - this.DrawingRect.Left;
                        var offsetY = CacheSource.DrawingRect.Top - this.DrawingRect.Top;
                        destination.Offset(offsetX, offsetY);

                        cache.Draw(ctx.Context.Surface.Canvas, destination, ImagePaint, FilterQuality.None);
                    }
                }
                else if (IsRecordedIntoPicture(ctx.Context))
                {
                    StopPictureCachesAbove();
                }
                else
                {
                    SKImage snapshot;
                    var canvas = ctx.Context.Canvas;
                    // where the copy is drawn back: the layout rect for the global surface (read while
                    // drawing into a cache), the on-screen area for the context surface
                    SKRect drawAt = destination;
                    var deviceSpace = false;
                    if (UseContext)
                    {
                        canvas.Flush();
                        // copy what is really under the backdrop on this surface: the layout rect mapped through
                        // the canvas matrix (ancestor scale, move, rotation, 3D), clamped to the surface
                        var device = canvas.TotalMatrix.MapRect(destination);
                        var area = new SKRectI((int)Math.Floor(device.Left), (int)Math.Floor(device.Top),
                            (int)Math.Ceiling(device.Right), (int)Math.Ceiling(device.Bottom));
                        snapshot = ctx.Context.Surface.Snapshot(area);
                        if (snapshot != null)
                        {
                            var left = Math.Max(area.Left, 0);
                            var top = Math.Max(area.Top, 0);
                            drawAt = new SKRect(left, top, left + snapshot.Width, top + snapshot.Height);
                            deviceSpace = true;
                        }
                    }
                    else
                    {
                        ctx.Context.Superview.CanvasView.Surface.Canvas.Flush();
                        snapshot = ctx.Context.Superview.CanvasView.Surface.Snapshot(new((int)destination.Left,
                            (int)destination.Top, (int)destination.Right, (int)destination.Bottom));
                    }

                    if (snapshot != null && Snapshot != snapshot)
                    {
                        var kill = Snapshot;
                        if (deviceSpace)
                        {
                            // drawn back 1:1 where it was taken, clipped to the backdrop's own (maybe turned) rect;
                            // the blur stays the same on screen while an ancestor scales
                            var saved = canvas.Save();
                            canvas.ClipRect(destination, SKClipOperation.Intersect, true);
                            canvas.ResetMatrix();
                            canvas.DrawImage(snapshot, drawAt, ImagePaint);
                            canvas.RestoreToCount(saved);
                        }
                        else
                        {
                            canvas.DrawImage(snapshot, drawAt, ImagePaint);
                        }
                        Snapshot = snapshot;
                        if (kill != null)
                        {
                            DisposeObject(kill);
                        }
                    }
                }
            }

            if (kill1 != null && kill1 != ImagePaint.ImageFilter)
                DisposeObject(kill1);

            if (kill2 != null && kill2 != ImagePaint.ColorFilter)
                DisposeObject(kill2);
        }
    }

    protected SKImage Snapshot { get; set; }

    /// <summary>
    /// True when this draw goes into an ancestor's picture cache (Operations, OperationsFull) instead of a surface.
    /// </summary>
    protected static bool IsRecordedIntoPicture(SkiaDrawingContext context)
    {
        return context.IsVirtual && context.Surface != null && context.Canvas != null
               && context.Canvas.Handle != context.Surface.Canvas.Handle;
    }

    /// <summary>
    /// A copy of the screen recorded into a picture would be replayed frozen on every later frame. So instead of
    /// drawing it, mark the picture-cached ancestors: they draw without their picture from the next frame on, and
    /// this backdrop then copies what is under it every frame. Only this one recording frame shows no copy.
    /// </summary>
    protected virtual void StopPictureCachesAbove()
    {
        var parent = Parent as SkiaControl;
        while (parent != null)
        {
            var type = parent.UsingCacheType;
            if (type == SkiaCacheType.Operations || type == SkiaCacheType.OperationsFull)
            {
                parent.HostsBackdrop = true;
                parent.InvalidateCache();
            }
            parent = parent.Parent as SkiaControl;
        }
        Update();
    }

    public static readonly BindableProperty CacheSourceProperty = BindableProperty.Create(
        nameof(SkiaBackdrop),
        typeof(SkiaControl),
        typeof(SkiaBackdrop),
        null,
        propertyChanged: WhenSourceChanged,
        defaultBindingMode: BindingMode.OneTime);

    public SkiaControl CacheSource
    {
        get { return (SkiaControl)GetValue(CacheSourceProperty); }
        set { SetValue(CacheSourceProperty, value); }
    }

    private static void WhenSourceChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaBackdrop control)
        {
            control.AttachSource();
        }
    }

    protected void AttachSource()
    {
        if (CacheSource != null)
        {
            CacheSource.CreatedCache += OnSourceCacheChanged;
        }
    }

    private void OnSourceCacheChanged(object sender, CachedObject e)
    {
        Update();
    }

    public virtual SKImage GetImage()
    {
        var image = Snapshot;
        if (image != null)
        {
            Snapshot = null;
            return image;
        }

        return null;
    }

    public override void OnScaleChanged()
    {
        InvalidateImageFilter();
        InvalidateColorFilter();

        base.OnScaleChanged();
    }

    protected virtual void BuildPaint()
    {
        if (PaintColorFilter == null && Brightness != 1.0)
        {
            PaintColorFilter = SkiaImageEffects.Gamma((float)this.Brightness);
        }

        if (PaintImageFilter == null && Blur > 0)
        {
            PaintImageFilter = SKImageFilter.CreateBlur((float)Blur * RenderingScale, (float)Blur * RenderingScale, SKShaderTileMode.Mirror);
        }

        if (ImagePaint == null)
        {
            ImagePaint = new();
        }
    }
}
