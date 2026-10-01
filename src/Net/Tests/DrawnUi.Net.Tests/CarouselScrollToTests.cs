using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaCarousel.ScrollTo(index, animate): clamps the index, jumps at once without animation,
/// and snaps back to the current slide without raising SelectedIndexChanged.
/// </summary>
public class CarouselScrollToTests
{
    private static (HeadlessCanvasHost Host, SkiaCarousel Carousel) Create()
    {
        var host = new HeadlessCanvasHost(400, 300, scale: 1f, background: Colors.Black);
        var carousel = new SkiaCarousel
        {
            HeightRequest = 250,
            HorizontalOptions = LayoutOptions.Fill,
            Children = Enumerable.Range(0, 4).Select(i => (SkiaControl)new SkiaShape
            {
                BackgroundColor = Colors.DarkSlateBlue,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
            }).ToList(),
        };
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { carousel }
        };
        host.AdvanceFrames(8);
        return (host, carousel);
    }

    [Fact]
    public void WithoutAnimation_JumpsAtOnce()
    {
        var (host, carousel) = Create();
        using var _ = host;
        Assert.Equal(4, carousel.SnapPoints.Count);

        carousel.ScrollTo(2, false);

        Assert.Equal(2, carousel.SelectedIndex);
        Assert.Equal(carousel.SnapPoints[2], carousel.CurrentPosition);
    }

    [Fact]
    public void Animated_ArrivesLater()
    {
        var (host, carousel) = Create();
        using var _ = host;

        carousel.ScrollTo(3);

        Assert.Equal(3, carousel.SelectedIndex);
        Assert.NotEqual(carousel.SnapPoints[3], carousel.CurrentPosition);
        host.AdvanceFrames(120);
        Assert.Equal(carousel.SnapPoints[3].X, carousel.CurrentPosition.X, 1);
    }

    [Fact]
    public void ClampsIndex()
    {
        var (host, carousel) = Create();
        using var _ = host;

        carousel.ScrollTo(99, false);
        Assert.Equal(3, carousel.SelectedIndex);

        carousel.ScrollTo(-5, false);

        Assert.Equal(0, carousel.SelectedIndex);
        Assert.Equal(carousel.SnapPoints[0], carousel.CurrentPosition);
    }
}
