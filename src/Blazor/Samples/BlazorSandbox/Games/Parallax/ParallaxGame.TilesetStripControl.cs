using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;

namespace ParallaxGameLoop.Game
{
    public sealed partial class ParallaxGame
    {
        /// <summary>
        /// Custom floor-strip renderer that tiles the corridor floor texture along the bottom band of the scene.
        /// </summary>
        private sealed class TilesetStripControl : SkiaControl
        {
            private static readonly SKSamplingOptions PixelSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);
            private readonly SKPaint _drawPaint = new() { IsAntialias = false };
            private readonly SKPaint _uploadPaint = new() { IsAntialias = false };
            private readonly string _source;
            private readonly float _tileWidth;
            private float _offsetX;
            private SKBitmap _bitmap;
            private SKImage _image;
            private SKImage _gpuImage;

            /// <summary>
            /// Horizontal scroll offset applied to the floor strip.
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
            /// Creates the floor-strip renderer with a fixed tile width in scene units.
            /// </summary>
            public TilesetStripControl(string source, float tileWidth)
            {
                _source = source.StartsWith('/') ? source : '/' + source;
                _tileWidth = tileWidth;
                WidthRequest = -1;
                HeightRequest = SceneHeight;
                HorizontalOptions = LayoutOptions.Fill;
                VerticalOptions = LayoutOptions.Start;
            }

            /// <summary>
            /// Injects the preloaded floor bitmap and refreshes cached image resources.
            /// </summary>
            public void SetBitmap(SKBitmap bitmap)
            {
                if (bitmap == null)
                {
                    return;
                }

                _bitmap?.Dispose();
                _image?.Dispose();
                _gpuImage?.Dispose();

                _bitmap = bitmap;
                _image = SKImage.FromBitmap(_bitmap);
                _gpuImage = null;
                Update();
            }

            /// <summary>
            /// Releases cached CPU and GPU image resources owned by the floor strip.
            /// </summary>
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

            /// <summary>
            /// Draws the floor strip into its authored band using the current scroll offset.
            /// </summary>
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
                var sceneHeight = SceneHeight * pixelScale;
                var stripTopOffset = TilesetTop * pixelScale;
                var stripHeight = TilesetHeight * pixelScale;
                var tileWidth = _tileWidth * pixelScale;
                var pixelOffset = OffsetX * pixelScale;
                var sceneTop = dest.Bottom - sceneHeight;
                var stripTop = sceneTop + stripTopOffset;
                var stripBottom = stripTop + stripHeight;
                var useOffsetX = -pixelOffset % tileWidth;
                var offsetX = useOffsetX > 0 ? useOffsetX : tileWidth + useOffsetX;

                for (var x = dest.Left - offsetX; x < dest.Right + tileWidth; x += tileWidth)
                {
                    var tileDest = new SKRect(x, stripTop, x + tileWidth, stripBottom);
                    if (activeImage != null)
                    {
                        ctx.Context.Canvas.DrawImage(activeImage, tileDest, PixelSampling, _drawPaint);
                    }
                    else
                    {
                        ctx.Context.Canvas.DrawBitmap(_bitmap, tileDest, _drawPaint);
                    }
                }
            }

            /// <summary>
            /// Lazily resolves the floor texture from cache and promotes it to GPU memory when possible.
            /// </summary>
            private void EnsureImageLoaded(DrawingContext ctx)
            {
                if (_bitmap == null)
                {
                    _bitmap = SkiaImageManager.Instance.GetFromCache(_source);
                    if (_bitmap != null)
                    {
                        _image = SKImage.FromBitmap(_bitmap);
                    }
                }

                if (_gpuImage == null && _image != null && Superview is DrawnView drawnView)
                {
                    using var surface = drawnView.CreateSurface(_bitmap.Width, _bitmap.Height, true);
                    if (surface?.Context != null)
                    {
                        surface.Canvas.DrawImage(_image, 0, 0, PixelSampling, _uploadPaint);
                        surface.Canvas.Flush();
                        _gpuImage = surface.Snapshot();
                        drawnView.ReturnSurface(surface);
                    }
                }
            }
        }
    }
}
