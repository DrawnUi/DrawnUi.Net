using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HelloWpf;

/// <summary>
/// Development aid for verifying a ported page without a human at the screen: when the
/// <c>HELLOWPF_SNAPSHOT</c> environment variable holds a file path, the drawn surface is rendered to
/// that PNG a couple of seconds after load, and the app exits when <c>HELLOWPF_EXIT</c> is set too.
/// <para>
/// Inert in a normal run — no variable, no timer, no cost. It uses WPF's own
/// <see cref="RenderTargetBitmap"/> rather than a screen grab, so it captures exactly what the
/// element painted and works on machines with no interactive desktop session.
/// </para>
/// </summary>
public static class DevSnapshot
{
    /// <summary>
    /// Navigates straight to a route when <c>HELLOWPF_NAV</c> names one, so a page can be verified
    /// without driving the menu. Inert when the variable is unset.
    /// </summary>
    public static void NavigateIfRequested(DrawnUi.Draw.SkiaShell shell)
    {
        var route = Environment.GetEnvironmentVariable("HELLOWPF_NAV");
        if (string.IsNullOrWhiteSpace(route))
            return;

        // After the first layout: the shell measures its slide distance from its own arranged width.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _ = shell.GoToAsync(route);
        };
        timer.Start();
    }

    /// <summary>Arms the snapshot for an element, if the environment asks for one.</summary>
    public static void ArmIfRequested(FrameworkElement element)
    {
        var path = Environment.GetEnvironmentVariable("HELLOWPF_SNAPSHOT");
        if (string.IsNullOrWhiteSpace(path))
            return;

        var delay = double.TryParse(Environment.GetEnvironmentVariable("HELLOWPF_SNAPSHOT_DELAY"), out var seconds)
            ? seconds
            : 2.5;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(delay) };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();

            // HELLOWPF_WHEEL=<notches> scrolls the canvas down before capturing, which is how a page's
            // scroll end gets verified without a human at the wheel.
            if (element is DrawnUi.Wpf.DrawnUiElement drawn
                && int.TryParse(Environment.GetEnvironmentVariable("HELLOWPF_WHEEL"), out var notches)
                && notches != 0)
            {
                var w = (float)(drawn.ActualWidth * 2);
                var h = (float)(drawn.ActualHeight * 2);
                var step = Math.Sign(notches);

                for (var i = 0; i < Math.Abs(notches); i++)
                {
                    drawn.Canvas.HandleDesktopWheel(w / 2, h / 2, -120 * 5 * step, w, h);
                    await Task.Delay(20);
                }

                await Task.Delay(1200);
            }

            // HELLOWPF_TAP="x,y" taps the canvas at those PIXEL coordinates before capturing, so a
            // page can be driven through one of its own buttons.
            var tap = Environment.GetEnvironmentVariable("HELLOWPF_TAP");
            if (element is DrawnUi.Wpf.DrawnUiElement tapTarget && !string.IsNullOrWhiteSpace(tap))
            {
                var parts = tap.Split(',');
                if (parts.Length == 2
                    && float.TryParse(parts[0], out var x)
                    && float.TryParse(parts[1], out var y))
                {
                    var w = (float)(tapTarget.ActualWidth * 2);
                    var h = (float)(tapTarget.ActualHeight * 2);
                    tapTarget.Canvas.HandleDesktopPointerDown(x, y, w, h);
                    tapTarget.Canvas.HandleDesktopPointerUp(x, y, w, h);
                    await Task.Delay(2500);
                }
            }

            Capture(element, path);

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HELLOWPF_EXIT")))
                Application.Current.Shutdown();
        };
        timer.Start();
    }

    private static void Capture(FrameworkElement element, string path)
    {
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
            return;

        var dpi = VisualTreeHelper.GetDpi(element);
        var target = new RenderTargetBitmap(
            (int)(element.ActualWidth * dpi.DpiScaleX),
            (int)(element.ActualHeight * dpi.DpiScaleY),
            96 * dpi.DpiScaleX,
            96 * dpi.DpiScaleY,
            PixelFormats.Pbgra32);

        target.Render(element);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        using var stream = File.Create(path);
        encoder.Save(stream);

        Console.WriteLine($"[snapshot] {path}");
    }
}
