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
}
