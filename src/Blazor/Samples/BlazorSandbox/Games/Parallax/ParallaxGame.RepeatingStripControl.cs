using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;

namespace ParallaxGameLoop.Game
{
    public sealed partial class ParallaxGame
    {
        /// <summary>
        /// Image control that repeats an authored texture segment horizontally across the scene.
        /// </summary>
        private sealed class RepeatingStripControl : SkiaImage
        {
            private static readonly SKSamplingOptions PixelSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);
            private readonly SKPaint _drawPaint = new() { IsAntialias = false };
            private readonly SKPaint _uploadPaint = new() { IsAntialias = false };
            private readonly float _repeatWidth;
            private readonly float _segmentWidth;
            private float _offsetX;
            private SKImage _gpuImage;
            private SKImage _repeatBandImage;
            private int _repeatBandWidthPixels;
            private int _repeatBandHeightPixels;

            /// <summary>
            /// Horizontal scroll offset applied to the repeating strip.
            /// </summary>
            public float OffsetX
            {
                get => _offsetX;
                set
                {
                    if (Math.Abs(_offsetX - value) < 0.001f)
                    {
                        return;
                    }

                    _offsetX = value;
                    Update();
                }
            }

            /// <summary>
            /// Creates a repeating strip with explicit authored repeat width, segment width, and scene placement.
            /// </summary>
            public RepeatingStripControl(string source, float repeatWidth, float segmentWidth, float top, float height)
            {
                _repeatWidth = repeatWidth;
                _segmentWidth = segmentWidth;
                Source = source;
                UseCache = SkiaCacheType.None;
                WidthRequest = -1;
                HeightRequest = height;
                HorizontalOptions = LayoutOptions.Fill;
                VerticalOptions = LayoutOptions.Start;
                Top = top;
            }

            /// <summary>
            /// Releases any GPU image promoted for faster rendering.
            /// </summary>
            public override void OnDisposing()
            {
                _gpuImage?.Dispose();
                _gpuImage = null;
                _repeatBandImage?.Dispose();
                _repeatBandImage = null;
                _drawPaint.Dispose();
                _uploadPaint.Dispose();
                base.OnDisposing();
            }

            /// <summary>
            /// Draws the repeating band by tiling the source image across each authored repeat segment.
            /// </summary>
            protected override void DrawSource(
                DrawingContext ctx,
                LoadedImageSource source,
                TransformAspect stretch,
                DrawImageAlignment horizontal = DrawImageAlignment.Center,
                DrawImageAlignment vertical = DrawImageAlignment.Center,
                SKPaint paint = null)
            {
                var activePaint = paint ?? _drawPaint;

                var dest = ctx.Destination;
                var pixelScale = ctx.Scale;
                var repeatWidth = MathF.Round(_repeatWidth * pixelScale);
                var segmentWidth = MathF.Round(_segmentWidth * pixelScale);
                var pixelOffset = OffsetX * pixelScale;
                var repeatBandWidthPixels = Math.Max(1, (int)repeatWidth);
                var repeatBandHeightPixels = Math.Max(1, (int)MathF.Round(dest.Height));

                if (_gpuImage == null && Superview is DrawnView drawnView)
                {
                    using var surface = drawnView.CreateSurface(source.Width, source.Height, true);
                    if (surface?.Context != null)
                    {
                        var uploadRect = new SKRect(0, 0, source.Width, source.Height);
                        if (source.Image != null)
                        {
                            surface.Canvas.DrawImage(source.Image, uploadRect, PixelSampling, _uploadPaint);
                        }
                        else if (source.Bitmap != null)
                        {
                            surface.Canvas.DrawBitmap(source.Bitmap, uploadRect, _uploadPaint);
                        }

                        surface.Canvas.Flush();
                        _gpuImage = surface.Snapshot();
                        drawnView.ReturnSurface(surface);
                    }
                }

                var activeImage = _gpuImage ?? source.Image;
                EnsureRepeatBandImage(source, activeImage, repeatBandWidthPixels, repeatBandHeightPixels, segmentWidth);

                var activeBand = _repeatBandImage;
                var useOffsetX = -pixelOffset % repeatWidth;
                var offsetX = useOffsetX > 0 ? useOffsetX : repeatWidth + useOffsetX;
                var startX = MathF.Round(dest.Left - offsetX);

                if (activeBand != null)
                {
                    for (var bandX = startX; bandX < dest.Right + repeatWidth; bandX += repeatWidth)
                    {
                        var bandDest = new SKRect(
                            MathF.Round(bandX),
                            dest.Top,
                            MathF.Round(bandX + repeatWidth),
                            dest.Bottom);
                        ctx.Context.Canvas.DrawImage(activeBand, bandDest, PixelSampling, activePaint);
                    }
                    return;
                }

                for (var bandX = startX; bandX < dest.Right + repeatWidth; bandX += repeatWidth)
                {
                    for (var x = bandX; x < bandX + repeatWidth; x += segmentWidth)
                    {
                        var left = MathF.Round(x);
                        var right = MathF.Round(x + segmentWidth);
                        var tileDest = new SKRect(left, dest.Top, right, dest.Bottom);

                        if (activeImage != null)
                        {
                            ctx.Context.Canvas.DrawImage(activeImage, tileDest, PixelSampling, activePaint);
                        }
                        else if (source.Bitmap != null)
                        {
                            ctx.Context.Canvas.DrawBitmap(source.Bitmap, tileDest, activePaint);
                        }
                    }
                }
            }

            private void EnsureRepeatBandImage(
                LoadedImageSource source,
                SKImage activeImage,
                int repeatWidthPixels,
                int repeatHeightPixels,
                float segmentWidthPixels)
            {
                if (_repeatBandImage != null
                    && _repeatBandWidthPixels == repeatWidthPixels
                    && _repeatBandHeightPixels == repeatHeightPixels)
                {
                    return;
                }

                _repeatBandImage?.Dispose();
                _repeatBandImage = null;
                _repeatBandWidthPixels = repeatWidthPixels;
                _repeatBandHeightPixels = repeatHeightPixels;

                if (repeatWidthPixels <= 0
                    || repeatHeightPixels <= 0
                    || segmentWidthPixels <= 0
                    || Superview is not DrawnView drawnView)
                {
                    return;
                }

                using var surface = drawnView.CreateSurface(repeatWidthPixels, repeatHeightPixels, true);
                if (surface?.Context == null)
                {
                    return;
                }

                surface.Canvas.Clear(SKColors.Transparent);

                for (var x = 0f; x < repeatWidthPixels; x += segmentWidthPixels)
                {
                    var left = MathF.Round(x);
                    var right = MathF.Round(Math.Min(x + segmentWidthPixels, repeatWidthPixels));
                    var tileDest = new SKRect(left, 0, right, repeatHeightPixels);

                    if (activeImage != null)
                    {
                        surface.Canvas.DrawImage(activeImage, tileDest, PixelSampling, _drawPaint);
                    }
                    else if (source.Bitmap != null)
                    {
                        surface.Canvas.DrawBitmap(source.Bitmap, tileDest, _drawPaint);
                    }
                }

                surface.Canvas.Flush();
                _repeatBandImage = surface.Snapshot();
                drawnView.ReturnSurface(surface);
            }
        }
    }
}
