using AppoMobi.Gestures;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The desktop canvas (WPF, OpenTK): a release after a fired long press sends no Tapped, as AppoMobi.Gestures does on
/// MAUI and Blazor; a short press still taps.
/// </summary>
public class DesktopLongPressTests
{
    private static (HeadlessCanvasHost host, Func<int> taps, Func<int> longPresses) Scene()
    {
        var host = new HeadlessCanvasHost(300, 300);
        int taps = 0, longPresses = 0;
        host.Canvas.Content = new SkiaLayout { WidthRequest = 200, HeightRequest = 200, BackgroundColor = Colors.Red }
            .OnTapped(me => taps++)
            .OnLongPressing(me => longPresses++);
        host.AdvanceFrames(3);
        return (host, () => taps, () => longPresses);
    }

    [Fact]
    public void A_release_after_a_long_press_does_not_tap()
    {
        var (host, taps, longPresses) = Scene();
        using (host)
        {
            // the entry points WPF and OpenTK call
            host.Canvas.HandleDesktopPointerDown(100, 100, 300, 300);
            host.AdvanceFrames(2);
            // the long press fires on a timer thread: wait for it by time, a busy full run is slower
            var waited = System.Diagnostics.Stopwatch.StartNew();
            while (longPresses() == 0 && waited.ElapsedMilliseconds < TouchEffect.LongPressTimeMsDefault + 5000)
            {
                Thread.Sleep(20);
                host.AdvanceFrames(1);
            }
            host.Canvas.HandleDesktopPointerUp(100, 100, 300, 300);
            host.AdvanceFrames(3);

            Assert.Equal(1, longPresses());
            Assert.Equal(0, taps());
        }
    }

    [Fact]
    public void A_short_press_still_taps()
    {
        var (host, taps, longPresses) = Scene();
        using (host)
        {
            host.Canvas.HandleDesktopPointerDown(100, 100, 300, 300);
            host.AdvanceFrames(2);
            host.Canvas.HandleDesktopPointerUp(100, 100, 300, 300);
            host.AdvanceFrames(3);

            Assert.Equal(1, taps());
            Assert.Equal(0, longPresses());
        }
    }
}
