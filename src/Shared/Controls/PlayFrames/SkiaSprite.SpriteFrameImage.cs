namespace DrawnUi.Controls
{
    public partial class SkiaSprite
    {
        public class SpriteFrameImage : SkiaImage
        {
            public SpriteFrameImage()
            {
          
            }
            protected override void SetAspectScale(int pxWidth, int pxHeight, SKRect dest, TransformAspect stretch, float scale)
            {
                base.SetAspectScale(pxWidth, pxHeight, dest, stretch, scale);

                Debug.WriteLine($"[SetAspectScale] {pxWidth}x{pxHeight}, {dest}, {stretch}, {scale}, {Source}");
            }

            public int FrameX { get; set; }
            public int FrameY { get; set; }
            public int FrameWidthPixels { get; set; }
            public int FrameHeightPixels { get; set; }

            SKPoint _lastAspect = SKPoint.Empty;

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

                if (_lastAspect != AspectScale)
                {
                    _lastAspect = AspectScale;
                    Super.Log($"[SpriteFrameImage] AspectScale changed: {_lastAspect}");
                }

                var dest = ctx.Destination;
                var scale = ctx.Scale;
                var aspectScaleX = AspectScale.X * (float)ZoomX;
                var aspectScaleY = AspectScale.Y * (float)ZoomY;

                var display = CalculateDisplayRect(
                    dest,
                    aspectScaleX * FrameWidthPixels,
                    aspectScaleY * FrameHeightPixels,
                    horizontal,
                    vertical);

                display.Inflate(new SKSize((float)InflateAmount, (float)InflateAmount));
                display.Offset((float)Math.Round(scale * HorizontalOffset), (float)Math.Round(scale * VerticalOffset));

                DisplayRect = display;
                TextureScale = new(dest.Width / display.Width, dest.Height / display.Height);

                var activePaint = paint ?? ImagePaint;
                var srcRect = new SKRect(FrameX, FrameY, FrameX + FrameWidthPixels, FrameY + FrameHeightPixels);

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
                if (FrameWidthPixels > 0 && FrameHeightPixels > 0)
                {
                    SetAspectScale(FrameWidthPixels, FrameHeightPixels, new SKRect(0, 0, widthRequest, heightRequest), this.Aspect, dscale);
                }

                return base.OnMeasuring(widthRequest, heightRequest, dscale);
            }
        }
    }
}
