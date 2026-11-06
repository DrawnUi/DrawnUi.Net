using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Views;
using DrawnUi.Testing;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A child's margins on a stack's cross axis are taken off its arranged rect once: in a Row the vertical ones, in a
/// Column the horizontal ones (Bricks Breaker life pads drew as thin lines).
/// </summary>
public class StackCrossMarginTests
{
    static SkiaShape Pad() => new()
    {
        WidthRequest = 26,
        HeightRequest = 7.5,
        Margin = new Thickness(1, 1, 3, 3),
        StrokeWidth = -1,
        CornerRadius = 4,
        Children = { new SkiaShape { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, Margin = new Thickness(5, 1.5) } },
    };

    [Theory]
    [InlineData(LayoutType.Row)]
    [InlineData(LayoutType.Column)]
    public void Cross_axis_margins_are_taken_off_once(LayoutType type)
    {
        const float scale = 1.33f;
        using var host = new HeadlessCanvasHost(400, 400, scale);
        SkiaShape first = null;
        host.Canvas.Content = new SkiaLayout
        {
            Type = type,
            Spacing = 3,
            Children = { Pad().Assign(out first), Pad() },
        };
        host.AdvanceFrames(3);

        var rect = first.DrawingRect;
        Assert.Equal(26 * scale, rect.Width, 1.0);
        Assert.Equal(7.5 * scale, rect.Height, 1.0);
    }
}
