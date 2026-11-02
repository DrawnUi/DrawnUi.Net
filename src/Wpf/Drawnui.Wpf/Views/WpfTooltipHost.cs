using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawnUi.Draw;
using SkiaSharp;

namespace DrawnUi.Wpf;

/// <summary>
/// Shows canvas tooltips in a WPF popup window: it can go past the edge of the app window, never takes focus or
/// input, and Windows keeps it on screen. The tooltip is a still image, nothing is drawn per frame.
/// </summary>
internal sealed class WpfTooltipHost : ITooltipHost
{
    private readonly FrameworkElement _target;
    private Popup _popup;
    private Image _image;
    private int _version;

    public WpfTooltipHost(FrameworkElement target)
    {
        _target = target;
    }

    public void ShowTooltip(SKImage image, SKPoint position, float scale)
    {
        int width = image.Width, height = image.Height;
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var pixels = new byte[info.BytesSize];
        using (image)
        {
            unsafe
            {
                fixed (byte* p = pixels)
                    image.ReadPixels(info, (IntPtr)p, info.RowBytes, 0, 0);
            }
        }

        var version = ++_version;
        _target.Dispatcher.BeginInvoke(() =>
        {
            if (version != _version)
                return;

            var dpi = 96.0 * scale;
            var source = BitmapSource.Create(width, height, dpi, dpi, PixelFormats.Pbgra32, null, pixels, info.RowBytes);
            source.Freeze();

            _image ??= new Image { Stretch = Stretch.None, IsHitTestVisible = false };
            _popup ??= new Popup
            {
                AllowsTransparency = true,
                Placement = PlacementMode.Relative,
                PlacementTarget = _target,
                IsHitTestVisible = false,
                Focusable = false,
                StaysOpen = true,
                PopupAnimation = PopupAnimation.None,
                Child = _image,
            };

            _image.Source = source;
            _image.Width = width / scale;
            _image.Height = height / scale;
            _popup.HorizontalOffset = position.X / scale;
            _popup.VerticalOffset = position.Y / scale;
            _popup.IsOpen = true;
        });
    }

    public void HideTooltip()
    {
        var version = ++_version;
        _target.Dispatcher.BeginInvoke(() =>
        {
            if (version == _version && _popup != null)
                _popup.IsOpen = false;
        });
    }
}
