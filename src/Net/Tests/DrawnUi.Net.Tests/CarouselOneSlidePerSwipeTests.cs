using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// One finger gesture moves a SkiaCarousel at most one slide, as iOS paging and the Android ViewPager do: from slide N a
/// release lands on N-1, N or N+1, the velocity only decides between staying and the neighbour. A drag past the middle
/// followed by a flick in the same direction used to skip a slide (an onboarding carousel went from 0 to 2).
/// </summary>
public class CarouselOneSlidePerSwipeTests
{
    private readonly ITestOutputHelper _out;
    public CarouselOneSlidePerSwipeTests(ITestOutputHelper o) { _out = o; }

    private const float Width = 400, Y = 125;

    private static (HeadlessCanvasHost host, SkiaCarousel carousel) Create(bool looped = false)
    {
        var host = new HeadlessCanvasHost((int)Width, 300, scale: 1f, background: Colors.Black);
        SkiaCarousel carousel = null;
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaCarousel
                {
                    HeightRequest = 250,
                    HorizontalOptions = LayoutOptions.Fill,
                    Bounces = false,
                    IsLooped = looped,
                    LinearSpeedMs = 420,
                    Children = Enumerable.Range(0, 3).Select(i => (SkiaControl)new SkiaShape
                    {
                        BackgroundColor = Colors.DarkSlateBlue,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                    }).ToList(),
                }.Assign(out carousel)
            }
        };
        host.AdvanceFrames(8);
        return (host, carousel);
    }

    /// <summary>
    /// One gesture: a slow drag of <paramref name="dragRatio"/> of a slide, then without lifting a flick of 4 fast moves the
    /// same way. <paramref name="dir"/> -1 = finger to the left (next slide), +1 = to the right (previous slide).
    /// </summary>
    private static void DragThenFlick(HeadlessCanvasHost host, float dragRatio, int dir, bool flick = true)
    {
        var robot = new GestureRobot(host);
        var x = dir < 0 ? Width - 20 : 20f;
        var drag = dragRatio * Width;
        robot.PointerDown(x, Y);
        for (var i = 1; i <= 20; i++)
            robot.PointerMoveTo(x + dir * drag * i / 20f, Y, 20);
        x += dir * drag;
        if (flick)
            for (var i = 1; i <= 4; i++)
                robot.PointerMoveTo(x + dir * 30 * i, Y, 8);
        robot.PointerUp(8);
        host.AdvanceFrames(90);
    }

    [Theory]
    [InlineData(0.45f)]
    [InlineData(0.5f)]
    [InlineData(0.55f)]
    [InlineData(0.7f)]
    public void HalfDragThenFlick_FromTheFirstSlide_LandsOnTheSecond(float dragRatio)
    {
        var (host, carousel) = Create();
        using var _ = host;
        Assert.Equal(0, carousel.SelectedIndex);

        DragThenFlick(host, dragRatio, -1);

        _out.WriteLine($"drag {dragRatio}: index {carousel.SelectedIndex} at {carousel.CurrentPosition}");
        Assert.Equal(1, carousel.SelectedIndex);
        Assert.Equal(carousel.SnapPoints[1], carousel.CurrentPosition);
    }

    [Fact]
    public void HalfDragThenFlickBack_FromTheLastSlide_LandsOnTheMiddle()
    {
        var (host, carousel) = Create();
        using var _ = host;
        carousel.ScrollTo(2, false);
        host.AdvanceFrames(4);

        DragThenFlick(host, 0.6f, +1);

        Assert.Equal(1, carousel.SelectedIndex);
        Assert.Equal(carousel.SnapPoints[1], carousel.CurrentPosition);
    }

    [Fact]
    public void Looped_HalfDragThenFlick_MovesOneSlide()
    {
        var (host, carousel) = Create(looped: true);
        using var _ = host;

        DragThenFlick(host, 0.6f, -1);

        Assert.Equal(1, carousel.SelectedIndex);
    }

    [Fact]
    public void SlowDragPastTheMiddle_WithoutFlick_GoesToTheNeighbour()
    {
        var (host, carousel) = Create();
        using var _ = host;

        DragThenFlick(host, 0.7f, -1, flick: false);

        Assert.Equal(1, carousel.SelectedIndex);
    }

    [Fact]
    public void CodeMoves_AreNotLimited()
    {
        var (host, carousel) = Create();
        using var _ = host;

        carousel.ScrollTo(2);
        host.AdvanceFrames(90);

        Assert.Equal(2, carousel.SelectedIndex);
        Assert.Equal(carousel.SnapPoints[2], carousel.CurrentPosition);
    }
}
