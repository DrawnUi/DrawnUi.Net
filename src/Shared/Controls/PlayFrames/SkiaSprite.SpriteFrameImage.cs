namespace DrawnUi.Controls
{
    public partial class SkiaSprite
    {
        public class SpriteFrameImage : SkiaImage
        {
            private SKRect _backgroundDestination;

            public SpriteFrameImage()
            {
                LoadSourceOnFirstDraw = false;
            }

            public int FrameX { get; set; }
            public int FrameY { get; set; }
            public int FrameWidthPixels { get; set; }
            public int FrameHeightPixels { get; set; }
            public int ContentOffsetXPixels { get; set; }
            public int ContentOffsetYPixels { get; set; }
            public int ContentWidthPixels { get; set; }
            public int ContentHeightPixels { get; set; }
            public float RenderUnitsPerPixel { get; set; } = -1f;
            public float RenderWidthUnits { get; set; } = -1f;
            public float RenderHeightUnits { get; set; } = -1f;
            public float RenderAnchorX { get; set; } = 0.5f;
            public float RenderAnchorY { get; set; } = 0.5f;

            protected override bool SetupBackgroundPaint(SKPaint paint, SKRect destination)
            {
                var backgroundDestination = _backgroundDestination == SKRect.Empty
                    ? destination
                    : _backgroundDestination;

                return base.SetupBackgroundPaint(paint, backgroundDestination);
            }

            protected override void Paint(DrawingContext ctx)
            {
                var savedBackground = Background;
                var savedBackgroundColor = BackgroundColor;
                var savedFillGradient = FillGradient;
                var hasBackground = savedBackground != null || (savedBackgroundColor?.Alpha ?? 0) > 0 || savedFillGradient != null;

                if (FrameWidthPixels > 0 && FrameHeightPixels > 0 && AspectScale != SKPoint.Empty)
                {
                    var aspectScaleX = AspectScale.X * (float)ZoomX;
                    var aspectScaleY = AspectScale.Y * (float)ZoomY;
                    ResolveFrameMetrics(ctx.Destination, ctx.Scale, aspectScaleX, aspectScaleY, out _, out var backgroundDestination);
                    backgroundDestination.Inflate(new SKSize((float)InflateAmount, (float)InflateAmount));
                    backgroundDestination.Offset(
                        (float)Math.Round(ctx.Scale * HorizontalOffset),
                        (float)Math.Round(ctx.Scale * VerticalOffset));
                    _backgroundDestination = backgroundDestination;
                }
                else
                {
                    _backgroundDestination = SKRect.Empty;
                }

                if (hasBackground && _backgroundDestination != SKRect.Empty)
                {
                    if (PaintSystem == null)
                    {
                        PaintSystem = new SKPaint();
                    }

                    if (base.SetupBackgroundPaint(PaintSystem, _backgroundDestination))
                    {
                        ctx.Context.Canvas.DrawRect(_backgroundDestination, PaintSystem);
                    }
                }

                Background = null;
                BackgroundColor = null;
                FillGradient = null;

                try
                {
                    base.Paint(ctx);
                }
                finally
                {
                    Background = savedBackground;
                    BackgroundColor = savedBackgroundColor;
                    FillGradient = savedFillGradient;
                    _backgroundDestination = SKRect.Empty;
                }
            }

            private void ResolveFrameMetrics(
                SKRect dest,
                float scale,
                float aspectScaleX,
                float aspectScaleY,
                out SKRect sourceRect,
                out SKRect displayRect)
            {
                var logicalWidthPixels = Math.Max(1, FrameWidthPixels);
                var logicalHeightPixels = Math.Max(1, FrameHeightPixels);
                var contentOffsetX = Math.Clamp(ContentOffsetXPixels, 0, logicalWidthPixels - 1);
                var contentOffsetY = Math.Clamp(ContentOffsetYPixels, 0, logicalHeightPixels - 1);
                var contentWidthPixels = ContentWidthPixels > 0 ? Math.Min(ContentWidthPixels, logicalWidthPixels - contentOffsetX) : logicalWidthPixels;
                var contentHeightPixels = ContentHeightPixels > 0 ? Math.Min(ContentHeightPixels, logicalHeightPixels - contentOffsetY) : logicalHeightPixels;

                var logicalDisplayWidth = aspectScaleX * logicalWidthPixels;
                var logicalDisplayHeight = aspectScaleY * logicalHeightPixels;

                if (RenderUnitsPerPixel > 0)
                {
                    logicalDisplayWidth = logicalWidthPixels * RenderUnitsPerPixel * scale;
                    logicalDisplayHeight = logicalHeightPixels * RenderUnitsPerPixel * scale;
                }

                if (RenderWidthUnits > 0 || RenderHeightUnits > 0)
                {
                    if (RenderWidthUnits > 0)
                    {
                        logicalDisplayWidth = RenderWidthUnits * scale;
                    }

                    if (RenderHeightUnits > 0)
                    {
                        logicalDisplayHeight = RenderHeightUnits * scale;
                    }

                    if (RenderWidthUnits > 0 && RenderHeightUnits <= 0)
                    {
                        logicalDisplayHeight = logicalDisplayWidth * logicalHeightPixels / logicalWidthPixels;
                    }
                    else if (RenderHeightUnits > 0 && RenderWidthUnits <= 0)
                    {
                        logicalDisplayWidth = logicalDisplayHeight * logicalWidthPixels / logicalHeightPixels;
                    }
                }

                var pixelsToDisplayX = logicalDisplayWidth / logicalWidthPixels;
                var pixelsToDisplayY = logicalDisplayHeight / logicalHeightPixels;
                var displayWidth = contentWidthPixels * pixelsToDisplayX;
                var displayHeight = contentHeightPixels * pixelsToDisplayY;
                var anchorX = dest.Left + dest.Width * RenderAnchorX;
                var anchorY = dest.Top + dest.Height * RenderAnchorY;
                var left = anchorX - logicalDisplayWidth * RenderAnchorX + contentOffsetX * pixelsToDisplayX;
                var top = anchorY - logicalDisplayHeight * RenderAnchorY + contentOffsetY * pixelsToDisplayY;

                displayRect = new SKRect(left, top, left + displayWidth, top + displayHeight);
                sourceRect = new SKRect(
                    FrameX + contentOffsetX,
                    FrameY + contentOffsetY,
                    FrameX + contentOffsetX + contentWidthPixels,
                    FrameY + contentOffsetY + contentHeightPixels);
            }

            protected override void DrawSource(
                DrawingContext ctx,
                LoadedImageSource source,
                TransformAspect stretch,
                DrawImageAlignment horizontal = DrawImageAlignment.Center,
                DrawImageAlignment vertical = DrawImageAlignment.Center,
                SKPaint paint = null)
            {
                if (FrameWidthPixels <= 0 || FrameHeightPixels <= 0)
                {
                    base.DrawSource(ctx, source, stretch, horizontal, vertical, paint);
                    return;
                }

                if (AspectScale == SKPoint.Empty)
                {
                    throw new ApplicationException("AspectScale is not set");
                }

                var dest = ctx.Destination;
                var scale = ctx.Scale;

                var aspectScaleX = AspectScale.X * (float)ZoomX;
                var aspectScaleY = AspectScale.Y * (float)ZoomY;

                ResolveFrameMetrics(dest, scale, aspectScaleX, aspectScaleY, out var srcRect, out var display);

                display.Inflate(new SKSize((float)InflateAmount, (float)InflateAmount));
                display.Offset((float)Math.Round(scale * HorizontalOffset), (float)Math.Round(scale * VerticalOffset));

                DisplayRect = display;
                TextureScale = new(dest.Width / display.Width, dest.Height / display.Height);

                var activePaint = paint ?? ImagePaint;

                if (source.Bitmap != null)
                {
                    ctx.Context.Canvas.DrawBitmap(source.Bitmap, srcRect, display, activePaint);
                }
                else if (source.Image != null)
                {
                    if (RescalingQuality != SKFilterQuality.None)
                    {
                        ctx.Context.Canvas.DrawImage(source.Image, srcRect, display, GetSamplingOptions(RescalingQuality, false), activePaint);
                    }
                    else
                    {
                        ctx.Context.Canvas.DrawImage(source.Image, srcRect, display, activePaint);
                    }
                }
            }

            protected override ScaledSize SetMeasuredAsEmpty(float scale)
            {
                AspectScale = SKPoint.Empty;
                return base.SetMeasuredAsEmpty(scale);
            }

            public override ScaledSize OnMeasuring(float widthRequest, float heightRequest, float dscale)
            {
                var measured = base.OnMeasuring(widthRequest, heightRequest, dscale);

                if (FrameWidthPixels > 0 && FrameHeightPixels > 0)
                {
                    var renderWidth = widthRequest;
                    var renderHeight = heightRequest;

                    if (RenderUnitsPerPixel > 0)
                    {
                        renderWidth = FrameWidthPixels * RenderUnitsPerPixel;
                        renderHeight = FrameHeightPixels * RenderUnitsPerPixel;
                    }

                    if (RenderWidthUnits > 0 || RenderHeightUnits > 0)
                    {
                        if (RenderWidthUnits > 0)
                        {
                            renderWidth = RenderWidthUnits;
                        }

                        if (RenderHeightUnits > 0)
                        {
                            renderHeight = RenderHeightUnits;
                        }

                        if (RenderWidthUnits > 0 && RenderHeightUnits <= 0)
                        {
                            renderHeight = renderWidth * FrameHeightPixels / FrameWidthPixels;
                        }
                        else if (RenderHeightUnits > 0 && RenderWidthUnits <= 0)
                        {
                            renderWidth = renderHeight * FrameWidthPixels / FrameHeightPixels;
                        }
                    }

                    SetAspectScale(FrameWidthPixels, FrameHeightPixels, new SKRect(0, 0, renderWidth, renderHeight), TransformAspect.AspectFit, dscale);
                }

                return measured;
            }
        }
    }
}
