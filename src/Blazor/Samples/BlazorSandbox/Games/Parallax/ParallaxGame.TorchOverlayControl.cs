using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;

namespace ParallaxGameLoop.Game
{
    public sealed partial class ParallaxGame
    {
        /// <summary>
        /// Repeating animated torch overlay that uses the same scroll math as the near corridor strip.
        /// </summary>
        private sealed class TorchOverlayControl : SkiaControl
        {
            private static readonly SKSamplingOptions PixelSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);
            private readonly SKPaint _drawPaint = new() { IsAntialias = false };
            private readonly SKPaint _uploadPaint = new() { IsAntialias = false };
            private readonly string _source;
            private float _offsetX;
            private SKBitmap _bitmap;
            private SKImage _image;
            private SKImage _gpuImage;

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

            public TorchOverlayControl(string source)
            {
                _source = source.StartsWith('/') ? source : '/' + source;
                WidthRequest = -1;
                HeightRequest = SceneHeight;
                HorizontalOptions = LayoutOptions.Fill;
                VerticalOptions = LayoutOptions.Start;
            }

            public override void OnDisposing()
            {
                _gpuImage?.Dispose();
                _gpuImage = null;
                _image?.Dispose();
                _image = null;
                _drawPaint.Dispose();
                _uploadPaint.Dispose();
                base.OnDisposing();
            }

            protected override void Paint(DrawingContext ctx)
            {
                base.Paint(ctx);

                EnsureImageLoaded(ctx);

                var activeImage = _gpuImage ?? _image;
                if (activeImage == null && _bitmap == null)
                {
                    return;
                }

                var dest = ctx.Destination;
                var pixelScale = ctx.Scale;
                var repeatWidth = TorchBandWidth * pixelScale;
                var torchSize = 32f * EnvironmentScale * pixelScale;
                var pixelOffset = OffsetX * pixelScale;
                var useOffsetX = -pixelOffset % repeatWidth;
                var offsetX = useOffsetX > 0 ? useOffsetX : repeatWidth + useOffsetX;
                var startBandX = dest.Left - offsetX;
                var frameWidth = (activeImage?.Width ?? _bitmap.Width) / 4f;
                var frameHeight = activeImage?.Height ?? _bitmap.Height;
                var frameIndex = (int)((SkiaControl.GetNanoseconds() / 1_000_000_000d) * 6d) % 4;
                var sourceRect = new SKRect(
                    frameIndex * frameWidth,
                    0,
                    (frameIndex + 1) * frameWidth,
                    frameHeight);

                for (var bandX = startBandX; bandX < dest.Right + repeatWidth; bandX += repeatWidth)
                {
                    foreach (var (anchorX, anchorY) in TorchAnchors)
                    {
                        var left = bandX + (((anchorX * EnvironmentScale) - (16f * EnvironmentScale)) * pixelScale);
                        var top = dest.Top + (((anchorY * EnvironmentScale) - (16f * EnvironmentScale)) * pixelScale);
                        var torchDest = new SKRect(
                            MathF.Round(left),
                            MathF.Round(top),
                            MathF.Round(left + torchSize),
                            MathF.Round(top + torchSize));

                        if (activeImage != null)
                        {
                            ctx.Context.Canvas.DrawImage(activeImage, sourceRect, torchDest, PixelSampling, _drawPaint);
                        }
                        else
                        {
                            ctx.Context.Canvas.DrawBitmap(_bitmap, sourceRect, torchDest, _drawPaint);
                        }
                    }
                }
            }

            private void EnsureImageLoaded(DrawingContext ctx)
            {
                if (_bitmap == null)
                {
                    _bitmap = SkiaImageManager.Instance.GetFromCache(_source);
                    if (_bitmap != null)
                    {
                        _image = SKImage.FromBitmap(_bitmap);
                        _gpuImage = null;
                    }
                }

                if (_gpuImage == null && _image != null && Superview is DrawnView drawnView)
                {
                    using var surface = drawnView.CreateSurface(_image.Width, _image.Height, true);
                    if (surface?.Context != null)
                    {
                        var uploadRect = new SKRect(0, 0, _image.Width, _image.Height);
                        surface.Canvas.DrawImage(_image, uploadRect, PixelSampling, _uploadPaint);
                        surface.Canvas.Flush();
                        _gpuImage = surface.Snapshot();
                        drawnView.ReturnSurface(surface);
                    }
                }
            }
        }
    }
}
