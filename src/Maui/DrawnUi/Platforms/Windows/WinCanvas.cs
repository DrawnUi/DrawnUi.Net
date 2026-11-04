using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp.Views.Windows;
using SKPaintSurfaceEventArgs = SkiaSharp.Views.Maui.SKPaintSurfaceEventArgs;
using Stretch = Microsoft.UI.Xaml.Media.Stretch;

namespace DrawnUi.Platforms.Windows
{
    public partial class WinCanvas : Microsoft.UI.Xaml.Controls.Canvas
    {

        private const float DpiBase = 96.0f;

        private static readonly DependencyProperty ProxyVisibilityProperty =
            DependencyProperty.Register(
                "ProxyVisibility",
                typeof(Microsoft.UI.Xaml.Visibility),
                typeof(WinCanvas),
                new PropertyMetadata(Microsoft.UI.Xaml.Visibility.Visible, OnVisibilityChanged));

        private static bool designMode = DesignMode.IsDesignModeEnabled;

        private IntPtr pixels;
        private WriteableBitmap bitmap;
        private ImageBrush brush;
        private bool ignorePixelScaling;
        private bool isVisible = true;

        // workaround for https://github.com/mono/SkiaSharp/issues/1118
        private int loadUnloadCounter = 0;

        public WinCanvas()
        {
            if (designMode)
                return;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;

            var binding = new Microsoft.UI.Xaml.Data.Binding
            {
                Path = new PropertyPath(nameof(Visibility)),
                Source = this
            };
            SetBinding(ProxyVisibilityProperty, binding);
        }

        public SKSize CanvasSize { get; private set; }

        public bool IgnorePixelScaling
        {
            get => ignorePixelScaling;
            set
            {
                ignorePixelScaling = value;
                Invalidate();
            }
        }

        public double Dpi { get; private set; } = 1;

        public event EventHandler<SKPaintSurfaceEventArgs> PaintSurface;

        protected virtual void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            PaintSurface?.Invoke(this, e);
        }

        private static void OnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WinCanvas canvas && e.NewValue is Microsoft.UI.Xaml.Visibility visibility)
            {
                canvas.isVisible = visibility == Microsoft.UI.Xaml.Visibility.Visible;
                canvas.Invalidate();
            }
        }

 
        private void OnXamlRootChanged(XamlRoot xamlRoot = null, XamlRootChangedEventArgs e = null)
        {
            var root = xamlRoot ?? XamlRoot;
            var newDpi = root?.RasterizationScale ?? 1.0;
            if (newDpi != Dpi)
            {
                Dpi = newDpi;
                UpdateBrushScale();
                Invalidate();
            }
        }
 

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Invalidate();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            loadUnloadCounter++;
            if (loadUnloadCounter != 1)
                return;

 
            _xamlRoot = XamlRoot;
            _xamlRoot.Changed += OnXamlRootChanged;
            OnXamlRootChanged();
 
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            loadUnloadCounter--;
            if (loadUnloadCounter != 0)
                return;

 
            DetachXamlRoot();
 

            FreeBitmap();
        }

        // the root this canvas listens to: XamlRoot is already null when a detached tree unloads, and the window's root
        // would keep the canvas alive
        private XamlRoot _xamlRoot;

        private void DetachXamlRoot()
        {
            if (_xamlRoot != null)
            {
                _xamlRoot.Changed -= OnXamlRootChanged;
                _xamlRoot = null;
            }
        }

        public void Invalidate()
        {
 
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, DoInvalidate);
 
        }

        private void DoInvalidate()
        {
            if (designMode)
                return;

            if (!isVisible)
                return;

            var (info, viewSize, dpi) = CreateBitmap();

            if (info.Width <= 0 || info.Height <= 0)
            {
                CanvasSize = SKSize.Empty;
                return;
            }

            // This is here because the property name is confusing and backwards.
            // True actually means to ignore the pixel scaling of the raw pixel
            // size and instead use the view size such that sizes match the XAML
            // elements.
            var matchUI = IgnorePixelScaling;

            var userVisibleSize = matchUI ? viewSize : info.Size;
            CanvasSize = userVisibleSize;

            using (var surface = SKSurface.Create(info, pixels, info.RowBytes))
            {
                if (matchUI)
                {
                    var canvas = surface.Canvas;
                    canvas.Scale(dpi);
                    canvas.Save();
                }

                OnPaintSurface(new SKPaintSurfaceEventArgs(surface, info.WithSize(userVisibleSize), info));
            }
            bitmap.Invalidate();
        }

        private (SKSizeI ViewSize, SKSizeI PixelSize, float Dpi) CreateSize()
        {
            var w = ActualWidth;
            var h = ActualHeight;

            if (!IsPositive(w) || !IsPositive(h))
                return (SKSizeI.Empty, SKSizeI.Empty, 1);

            var dpi = (float)Dpi;
            var viewSize = new SKSizeI((int)w, (int)h);
            var pixelSize = new SKSizeI((int)(w * dpi), (int)(h * dpi));

            return (viewSize, pixelSize, dpi);

            static bool IsPositive(double value)
            {
                return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
            }
        }

        private (SKImageInfo Info, SKSizeI PixelSize, float Dpi) CreateBitmap()
        {
            var (viewSize, pixelSize, dpi) = CreateSize();
            var info = new SKImageInfo(pixelSize.Width, pixelSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

            if (bitmap?.PixelWidth != info.Width || bitmap?.PixelHeight != info.Height)
                FreeBitmap();

            if (bitmap == null && info.Width > 0 && info.Height > 0)
            {
                bitmap = new WriteableBitmap(info.Width, info.Height);
                //pixels = bitmap.GetPixels(); TODO !!!

                brush = new ImageBrush
                {
                    ImageSource = bitmap,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top,
                    Stretch = Stretch.None
                };
                UpdateBrushScale();

                Background = brush;
            }

            return (info, viewSize, dpi);
        }

        private void UpdateBrushScale()
        {
            if (brush == null)
                return;

            var scale = 1.0 / Dpi;

            brush.Transform = new ScaleTransform
            {
                ScaleX = scale,
                ScaleY = scale
            };
        }

        private void FreeBitmap()
        {
            Background = null;
            brush = null;
            bitmap = null;
            pixels = IntPtr.Zero;
        }
    }
}
