using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// An auto-width stack (Center, no WidthRequest) is as wide as its non-Fill children; its Fill children are measured
/// for that final width, so text that wraps there gets its full height, the spacing is kept and nothing overlaps.
/// </summary>
public class StackFillChildFinalWidthTests
{
    static SkiaShape Card(string text) => new()
    {
        Padding = new Thickness(16),
        HorizontalOptions = LayoutOptions.Fill,
        UseCache = SkiaCacheType.Image,
        Children =
        {
            new SkiaStack
            {
                Spacing = 6,
                Children =
                {
                    new SkiaLabel("Heading") { FontSize = 18 },
                    new SkiaLabel(text) { FontSize = 13, HorizontalOptions = LayoutOptions.Fill },
                }
            }
        }
    };

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    public void CardsBelowEachOther_SpacingKept(float scale)
    {
        using var host = new HeadlessCanvasHost((int)(1000 * scale), (int)(700 * scale), scale, Colors.Black);
        SkiaShape card1, card2;
        SkiaRow row;
        SkiaStack stack;
        host.Canvas.Content = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaStack
                {
                    Spacing = 16,
                    Padding = new Thickness(24),
                    MaximumWidthRequest = 640,
                    HorizontalOptions = LayoutOptions.Center,
                    Children =
                    {
                        Card("Every control of this window is visible to the designer, with its properties.").Assign(out card1),
                        Card("Change a property in the designer and this window shows it at once.").Assign(out card2),
                        new SkiaRow
                        {
                            Spacing = 12,
                            Children =
                            {
                                new SkiaShape { WidthRequest = 100, HeightRequest = 40 },
                                new SkiaShape { WidthRequest = 100, HeightRequest = 40 },
                                new SkiaShape { WidthRequest = 100, HeightRequest = 40 },
                            }
                        }.Assign(out row),
                    }
                }.Assign(out stack)
            }
        };
        host.AdvanceFrames(6);

        var spacing = 16 * scale;
        Assert.True(card1.DrawingRect.Width < 330 * scale, $"stack keeps the width of its row: card {card1.DrawingRect.Width}");
        Assert.Equal(card1.DrawingRect.Bottom + spacing, card2.DrawingRect.Top, 1.5);
        Assert.Equal(card2.DrawingRect.Bottom + spacing, row.DrawingRect.Top, 1.5);
        Assert.Equal(row.DrawingRect.Bottom + 24 * scale, stack.DrawingRect.Bottom, 1.5);
    }
}
