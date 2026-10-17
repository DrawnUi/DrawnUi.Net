using System.Drawing;
using AppoMobi.Specials;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaShaderCarousel, a second swipe while the first one is still animating (interrupted swipe): it moves one more
/// slide, and Stopped fires only at real rest, never between the two phases of that swipe. The FiltersCamera viewer
/// shifts its windowed ItemsSource on Stopped; a Stopped between the phases let the queued second half replay in the
/// shifted window and the carousel swept on by itself through about ten slides (2026-10-17).
/// </summary>
public class ShaderCarouselInterruptedSwipeTests
{
    private const int Width = 400;
    private const int Height = 800;

    private static SkiaShaderCarousel Create(HeadlessCanvasHost host, System.Collections.IList items, int selected, bool looped)
    {
        SkiaShaderCarousel carousel = null;
        host.Canvas.Content = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaShaderCarousel
                {
                    IsLooped = looped,
                    LinearSpeedMs = 500,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    ItemsSource = items,
                    SelectedIndex = selected,
                    ItemTemplate = new DataTemplate(() => new SkiaLayer
                    {
                        BackgroundColor = Colors.DarkGray,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                    }),
                }.Assign(out carousel)
            }
        };
        host.AdvanceFrames(30, 16);
        return carousel;
    }

    /// <summary>Two fast flicks, the second one 3 frames into the first one's transition.</summary>
    private static void TwoFastFlicks(HeadlessCanvasHost host, bool forward)
    {
        var robot = new GestureRobot(host);
        var (from, to) = forward ? (300f, 180f) : (100f, 220f);
        robot.Pan(new PointF(from, Height / 2f), new PointF(to, Height / 2f), 60, 4);
        host.AdvanceFrames(3, 16);
        robot.Pan(new PointF(from, Height / 2f), new PointF(to, Height / 2f), 60, 4);
    }

    /// <summary>Frames as an app draws them: while one is asked for, then a few more.</summary>
    private static void Settle(HeadlessCanvasHost host)
    {
        for (var i = 0; i < 400 && host.NeedsFrame; i++)
            host.RenderFrame(16);
        host.AdvanceFrames(10, 16);
    }

    [Fact]
    public void SecondSwipe_DuringSnap_MovesOneMoreSlide()
    {
        using var host = new HeadlessCanvasHost(Width, Height, scale: 1f, background: Colors.Black);
        var carousel = Create(host, Enumerable.Range(0, 10).ToList(), 2, looped: false);
        var stops = 0;
        carousel.Stopped += (s, p) => stops++;

        TwoFastFlicks(host, forward: true);
        Settle(host);

        Assert.Equal(4, carousel.SelectedIndex);
        Assert.Equal(1, stops); // once, at real rest
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Looped_SecondSwipe_AcrossTheWrap_LandsOnTheRightSlide(bool forward)
    {
        using var host = new HeadlessCanvasHost(Width, Height, scale: 1f, background: Colors.Black);
        // forward: 4 -> 0 (wrap) -> 1; backward: 0 -> 4 (wrap) -> 3
        var carousel = Create(host, Enumerable.Range(0, 5).ToList(), forward ? 4 : 0, looped: true);

        TwoFastFlicks(host, forward);
        Settle(host);

        Assert.Equal(forward ? 1 : 3, carousel.SelectedIndex);
        Assert.False(carousel.InTransition);
    }

    /// <summary>
    /// The FiltersCamera viewer's paging, copied: a 30-slide window over a 60-shot source; at rest within 6 slides of
    /// a window edge the window moves (RemoveRange + InsertRange) so the current shot is its middle again, and the
    /// carousel jumps to that shot's new index without animation.
    /// </summary>
    [Fact]
    public void WindowShiftOnStopped_DoesNotReplayTheSecondSwipe()
    {
        const int sourceCount = 60, windowSize = 30, windowMargin = 6;
        var source = Enumerable.Range(0, sourceCount).ToList();
        var windowStart = 15;
        var window = new ObservableRangeCollection<int>(Enumerable.Range(windowStart, windowSize));
        var recentering = false;

        using var host = new HeadlessCanvasHost(Width, Height, scale: 1f, background: Colors.Black);
        var carousel = Create(host, window, 23, looped: false);
        Assert.Equal(38, window[carousel.SelectedIndex]);

        carousel.Stopped += (s, p) => MainThread.BeginInvokeOnMainThread(() =>
        {
            if (recentering || carousel.InTransition || carousel.IsUserFocused)
                return;
            var index = carousel.SelectedIndex;
            if (index >= windowMargin && index <= windowSize - 1 - windowMargin)
                return;

            var shift = index - windowSize / 2;
            recentering = true;
            try
            {
                if (shift > 0)
                {
                    window.RemoveRange(0, shift);
                    window.InsertRange(window.Count,
                        Enumerable.Range(windowSize, shift).Select(k => source[(windowStart + k) % sourceCount]));
                }
                else
                {
                    window.RemoveRange(windowSize + shift, -shift);
                    window.InsertRange(0,
                        Enumerable.Range(shift, -shift).Select(k => source[((windowStart + k) % sourceCount + sourceCount) % sourceCount]));
                }

                windowStart = ((windowStart + shift) % sourceCount + sourceCount) % sourceCount;
                carousel.ScrollTo(index - shift, animate: false);
            }
            finally
            {
                recentering = false;
            }
        });

        TwoFastFlicks(host, forward: true); // 38 -> 39 -> 40
        Settle(host);

        Assert.Equal(40, window[carousel.SelectedIndex]);
    }
}
