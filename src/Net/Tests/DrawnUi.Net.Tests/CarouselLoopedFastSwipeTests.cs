using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A looped shader carousel swiped fast and non-stop across the wrap, either way: every frame shows the
/// transition a little further along, never a jump to another pair of slides, which flickered.
/// </summary>
public class CarouselLoopedFastSwipeTests
{
    class Slide : SkiaDynamicDrawnCell
    {
        protected override void SetContent(object ctx)
        {
            HorizontalOptions = LayoutOptions.Fill;
            VerticalOptions = LayoutOptions.Fill;
        }
    }

    [Fact]
    public void FastSwipesAcrossTheWrap_NeverJumpToAnotherPair()
    {
        using var host = new HeadlessCanvasHost(820, 300);
        var carousel = new SkiaShaderCarousel
        {
            HeightRequest = 280, HorizontalOptions = LayoutOptions.Fill, IsLooped = true, LinearSpeedMs = 750,
            ItemsSource = Enumerable.Range(0, 4).ToList(),
            ItemTemplate = new DataTemplate(() => new Slide()),
        };
        host.Canvas.Content = carousel;
        host.AdvanceFrames(20);

        // where the transition is, in slides around the loop: from + progress
        var positions = new List<double>();
        host.Canvas.WasDrawn += (_, _) => positions.Add(carousel.TransitionFromIndex + carousel.TransitionEffect.Progress);

        var robot = new GestureRobot(host);
        for (var s = 0; s < 10; s++)
        {
            robot.Pan(700, 150, 300, 150, 60, 4); // a fast swipe to the next slide, before the last one ended
            for (var f = 0; f < 6; f++)
                host.RenderFrame(16);
        }
        for (var s = 0; s < 10; s++)
        {
            robot.Pan(300, 150, 700, 150, 60, 4); // and back the other way, across the wrap from the first to the last
            for (var f = 0; f < 6; f++)
                host.RenderFrame(16);
        }
        for (var f = 0; f < 80; f++)
            host.RenderFrame(16);

        for (var i = 1; i < positions.Count; i++)
        {
            var step = Math.Abs(positions[i] - positions[i - 1]) % 4;
            step = Math.Min(step, 4 - step);
            Assert.True(step < 0.5, $"frame {i}: {positions[i - 1]:0.00} -> {positions[i]:0.00}");
        }
    }

    [Fact]
    public void RelayoutAtTheSameSize_DuringATransition_DoesNotMoveIt()
    {
        using var host = new HeadlessCanvasHost(820, 330);
        var title = new SkiaLabel("start");
        var carousel = new SkiaShaderCarousel
        {
            HeightRequest = 280, HorizontalOptions = LayoutOptions.Fill, IsLooped = true, LinearSpeedMs = 750,
            ItemsSource = Enumerable.Range(0, 4).ToList(),
            ItemTemplate = new DataTemplate(() => new Slide()),
        };
        // as in the demo: a title above shows the pair, its text changes while the transition runs
        carousel.FromToChanged += (_, _) => title.Text = $"{carousel.TransitionFromIndex} -> {carousel.TransitionToIndex}";
        // the demo's page: a centered column in a scroll, a card around the title and the carousel
        var page = new SkiaStack
        {
            HorizontalOptions = LayoutOptions.Center, MaximumWidthRequest = 720,
            Children =
            {
                new SkiaShape
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    Children = { new SkiaStack { HorizontalOptions = LayoutOptions.Fill, Children = { title, carousel } } },
                },
            },
        };
        host.Canvas.Content = new SkiaScroll { VerticalOptions = LayoutOptions.Fill, Content = page };
        host.AdvanceFrames(20);

        var positions = new List<double>();
        host.Canvas.WasDrawn += (_, _) => positions.Add(carousel.TransitionFromIndex + carousel.TransitionEffect.Progress);

        carousel.SelectedIndex = 3; // 0 -> 3 backward across the wrap: one slide, through the virtual slot
        for (var f = 0; f < 60; f++)
            host.RenderFrame(16);

        for (var i = 1; i < positions.Count; i++)
        {
            var step = Math.Abs(positions[i] - positions[i - 1]) % 4;
            step = Math.Min(step, 4 - step);
            Assert.True(step < 0.5, $"frame {i}: {positions[i - 1]:0.00} -> {positions[i]:0.00}");
        }
        Assert.Equal(3, carousel.SelectedIndex);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SlowDragsPastHalf_MoveOneSlide_BothWaysAcrossTheWrap(bool shader)
    {
        using var host = new HeadlessCanvasHost(820, 300);
        SkiaCarousel carousel = shader
            ? new SkiaShaderCarousel { HeightRequest = 280, HorizontalOptions = LayoutOptions.Fill, IsLooped = true, LinearSpeedMs = 750, ItemsSource = Enumerable.Range(0, 4).ToList(), ItemTemplate = new DataTemplate(() => new Slide()) }
            : new SkiaCarousel { HeightRequest = 280, HorizontalOptions = LayoutOptions.Fill, IsLooped = true, ItemsSource = Enumerable.Range(0, 4).ToList(), ItemTemplate = new DataTemplate(() => new Slide()) };
        host.Canvas.Content = carousel;
        host.AdvanceFrames(20);
        var robot = new GestureRobot(host);

        void Drag(float dx, double ms, double holdMs)
        {
            var from = dx < 0 ? 700f : 120f;
            robot.Pan(new System.Drawing.PointF(from, 150), new System.Drawing.PointF(from + dx, 150), ms, 30, holdMs);
            for (var f = 0; f < 120; f++)
                host.RenderFrame(16);
        }

        // slow drags of 60% of a slide, released without a flick: one slide each, around the loop
        var forward = new List<int>();
        for (var s = 0; s < 5; s++) { Drag(-820 * 0.6f, 600, 300); forward.Add(carousel.SelectedIndex); }
        Assert.Equal(new[] { 1, 2, 3, 0, 1 }, forward);
        var back = new List<int>();
        for (var s = 0; s < 5; s++) { Drag(820 * 0.6f, 600, 300); back.Add(carousel.SelectedIndex); }
        Assert.Equal(new[] { 0, 3, 2, 1, 0 }, back);

        // a short slow drag stays, a flick moves one slide
        Drag(-820 * 0.2f, 600, 300);
        Assert.Equal(0, carousel.SelectedIndex);
        Drag(-820 * 0.3f, 60, 0);
        Assert.Equal(1, carousel.SelectedIndex);
    }
}
