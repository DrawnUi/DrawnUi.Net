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

            private const string SeeThroughShader = """
                uniform shader band;
                uniform float2 uOrigin;
                uniform float2 uCenter;
                uniform float2 uRadius;
                uniform float uKeep;

                half4 main(float2 p) {
                    half4 c = band.eval(p - uOrigin);
                    float r = length((p - uCenter) / uRadius);
                    float w = 1.0 - smoothstep(0.35, 1.0, r);
                    return c * half(mix(1.0, uKeep, w));
                }
                """;

            private static SKRuntimeEffect _seeThroughEffect;
            private SKRuntimeEffectUniforms _seeThroughUniforms;
            private SKRuntimeEffectChildren _seeThroughChildren;
            private readonly SKPaint _seeThroughPaint = new() { IsAntialias = false };
            private readonly float[] _origin = new float[2];
            private readonly float[] _center = new float[2];
            private readonly float[] _radius = new float[2];
            private float _opaqueFrom = -1f;
            private float _opaqueTo = -1f;
            private SKShader _bandShader;
            private SKImage _bandShaderImage;
            private SKSize _bandShaderSize;

            /// <summary>
            /// When set, returns the box to see through (the player's body), in scene units relative to this strip:
            /// where the strip covers it, the strip fades smoothly to partly transparent in a soft oval around it.
            /// </summary>
            public Func<SKRect> SeeThroughWindow { get; set; }

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
                _seeThroughPaint.Dispose();
                _seeThroughUniforms?.Dispose();
                _bandShader?.Dispose();
                _seeThroughChildren?.Dispose();
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
                    var window = SKRect.Empty;
                    if (SeeThroughWindow != null)
                    {
                        var body = SeeThroughWindow();
                        window = new SKRect(
                            dest.Left + body.Left * pixelScale,
                            dest.Top + body.Top * pixelScale,
                            dest.Left + body.Right * pixelScale,
                            dest.Top + body.Bottom * pixelScale);
                    }
                    for (var bandX = startX; bandX < dest.Right + repeatWidth; bandX += repeatWidth)
                    {
                        var bandDest = new SKRect(
                            MathF.Round(bandX),
                            dest.Top,
                            MathF.Round(bandX + repeatWidth),
                            dest.Bottom);
                        if (!window.IsEmpty && bandDest.IntersectsWith(window))
                        {
                            // An oval a bit larger than the body; the shader runs only inside its bounds,
                            // and only when an opaque part of the strip (a pillar) is actually there.
                            var radiusX = window.Width * 0.5f + 34f * pixelScale;
                            var radiusY = window.Height * 0.5f + 30f * pixelScale;
                            var area = new SKRect(
                                MathF.Floor(window.MidX - radiusX),
                                MathF.Floor(window.MidY - radiusY),
                                MathF.Ceiling(window.MidX + radiusX),
                                MathF.Ceiling(window.MidY + radiusY));
                            area.Intersect(bandDest);

                            if (!area.IsEmpty && CoversOpaque(source, bandDest, area, segmentWidth) && EnsureSeeThrough())
                            {
                                var canvas = ctx.Context.Canvas;
                                canvas.Save();
                                canvas.ClipRect(area, SKClipOperation.Difference);
                                canvas.DrawImage(activeBand, bandDest, PixelSampling, activePaint);
                                canvas.Restore();
                                DrawSeeThrough(canvas, activeBand, bandDest, area, new SKPoint(window.MidX, window.MidY), radiusX, radiusY, activePaint);
                                continue;
                            }
                        }

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

            private bool EnsureSeeThrough()
            {
                if (_seeThroughEffect == null)
                {
                    _seeThroughEffect = SKRuntimeEffect.CreateShader(SeeThroughShader, out var errors);
                    if (_seeThroughEffect == null)
                    {
                        Console.WriteLine($"[ParallaxGame] see-through shader: {errors}");
                        SeeThroughWindow = null;
                        return false;
                    }
                }

                _seeThroughUniforms ??= new SKRuntimeEffectUniforms(_seeThroughEffect);
                _seeThroughChildren ??= new SKRuntimeEffectChildren(_seeThroughEffect);
                return true;
            }

            /// <summary>
            /// True when an opaque column of the source (a pillar) falls inside the area. The opaque column span
            /// of the source is read once from its pixels.
            /// </summary>
            private bool CoversOpaque(LoadedImageSource source, SKRect bandDest, SKRect area, float segmentWidth)
            {
                if (_opaqueFrom < 0f)
                {
                    FindOpaqueColumns(source);
                    if (_opaqueFrom < 0f)
                    {
                        return true;
                    }
                }

                for (var x = bandDest.Left; x < bandDest.Right; x += segmentWidth)
                {
                    var from = x + _opaqueFrom * segmentWidth;
                    var to = x + _opaqueTo * segmentWidth;
                    if (to > area.Left && from < area.Right)
                    {
                        return true;
                    }
                }

                return false;
            }

            private void FindOpaqueColumns(LoadedImageSource source)
            {
                var bitmap = source.Bitmap;
                using var fromImage = bitmap == null && source.Image != null ? SKBitmap.FromImage(source.Image) : null;
                bitmap ??= fromImage;
                if (bitmap == null || bitmap.Width == 0)
                {
                    return;
                }

                int first = -1, last = -1;
                for (var x = 0; x < bitmap.Width; x++)
                {
                    for (var y = 0; y < bitmap.Height; y++)
                    {
                        if (bitmap.GetPixel(x, y).Alpha > 8)
                        {
                            if (first < 0)
                            {
                                first = x;
                            }
                            last = x;
                            break;
                        }
                    }
                }

                if (first >= 0)
                {
                    _opaqueFrom = first / (float)bitmap.Width;
                    _opaqueTo = (last + 1) / (float)bitmap.Width;
                }
            }

            /// <summary>
            /// Draws the oval area of one band through the see-through shader: one pass over a small rect,
            /// no offscreen layer. The band's image shader is made once per band image and moved by a uniform.
            /// </summary>
            private void DrawSeeThrough(SKCanvas canvas, SKImage band, SKRect bandDest, SKRect area, SKPoint center, float radiusX, float radiusY, SKPaint paint)
            {
                var size = new SKSize(bandDest.Width, bandDest.Height);
                if (_bandShader == null || _bandShaderImage != band || _bandShaderSize != size)
                {
                    _bandShader?.Dispose();
                    _bandShader = band.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, PixelSampling,
                        SKMatrix.CreateScale(size.Width / band.Width, size.Height / band.Height));
                    _bandShaderImage = band;
                    _bandShaderSize = size;
                    _seeThroughChildren["band"] = _bandShader;
                }

                _origin[0] = bandDest.Left;
                _origin[1] = bandDest.Top;
                _center[0] = center.X;
                _center[1] = center.Y;
                _radius[0] = radiusX;
                _radius[1] = radiusY;
                _seeThroughUniforms["uOrigin"] = _origin;
                _seeThroughUniforms["uCenter"] = _center;
                _seeThroughUniforms["uRadius"] = _radius;
                _seeThroughUniforms["uKeep"] = 0.3f;

                using var shader = _seeThroughEffect.ToShader(_seeThroughUniforms, _seeThroughChildren);
                _seeThroughPaint.Shader = shader;
                _seeThroughPaint.Color = paint.Color;
                canvas.DrawRect(area, _seeThroughPaint);
                _seeThroughPaint.Shader = null;
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
