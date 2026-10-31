using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A stack sized by its content on its cross axis (a Column without width, a Row without height) is as wide (tall) as
/// its widest child, Fill children included at their fit-content size: their content size capped by the room there is.
/// A Fill child without content (a divider) adds only its own requests. Fill children are then measured at the final
/// size, so text wrapping there keeps the spacing below it. The same rule as DrawnUi.Rust and DrawnUi.React.
/// </summary>
public class StackFillChildFinalWidthTests
{
    const string LongText = "Every control of this window is visible to the designer, with its properties.";

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

    static HeadlessCanvasHost Host(SkiaControl stack, float scale)
    {
        var host = new HeadlessCanvasHost((int)(1000 * scale), (int)(700 * scale), scale, Colors.Black);
        host.Canvas.Content = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { stack }
        };
        host.AdvanceFrames(6);
        return host;
    }

    static SkiaRow Buttons() => new()
    {
        Spacing = 12,
        Children =
        {
            new SkiaShape { WidthRequest = 100, HeightRequest = 40 },
            new SkiaShape { WidthRequest = 100, HeightRequest = 40 },
            new SkiaShape { WidthRequest = 100, HeightRequest = 40 },
        }
    };

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    public void FillCards_WidenTheStackToTheirText(float scale)
    {
        SkiaShape card1, card2;
        SkiaRow row;
        SkiaStack stack;
        using var host = Host(new SkiaStack
        {
            Spacing = 16,
            Padding = new Thickness(24),
            MaximumWidthRequest = 640,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                Card(LongText).Assign(out card1),
                Card("Change a property in the designer and this window shows it at once.").Assign(out card2),
                Buttons().Assign(out row),
            }
        }.Assign(out stack), scale);

        // wider than the 324 pt button row, one line of text each, at most 640 pt with the padding
        Assert.True(card1.DrawingRect.Width > 400 * scale, $"card {card1.DrawingRect.Width}");
        Assert.True(stack.DrawingRect.Width <= 640 * scale + 1, $"stack {stack.DrawingRect.Width}");
        Assert.Equal(card1.DrawingRect.Width, card2.DrawingRect.Width, 1);
        Assert.Equal(card1.DrawingRect.Bottom + 16 * scale, card2.DrawingRect.Top, 1.5);
        Assert.Equal(card2.DrawingRect.Bottom + 16 * scale, row.DrawingRect.Top, 1.5);
        Assert.Equal(row.DrawingRect.Bottom + 24 * scale, stack.DrawingRect.Bottom, 1.5);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    public void TextWiderThanTheMax_WrapsInside_SpacingKept(float scale)
    {
        SkiaShape card1, card2;
        SkiaStack stack;
        using var host = Host(new SkiaStack
        {
            Spacing = 16,
            MaximumWidthRequest = 300,
            HorizontalOptions = LayoutOptions.Center,
            Children = { Card(LongText).Assign(out card1), Card(LongText).Assign(out card2) }
        }.Assign(out stack), scale);

        Assert.Equal(300 * scale, stack.DrawingRect.Width, 1.5);
        Assert.Equal(card1.DrawingRect.Bottom + 16 * scale, card2.DrawingRect.Top, 1.5);
        Assert.Equal(card2.DrawingRect.Bottom, stack.DrawingRect.Bottom, 1.5);
    }

    [Fact]
    public void Divider_DoesNotWidenTheStack()
    {
        SkiaLabel title;
        SkiaShape divider;
        SkiaStack stack;
        using var host = Host(new SkiaStack
        {
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new SkiaLabel("Title") { FontSize = 20 }.Assign(out title),
                new SkiaShape { HeightRequest = 1, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.White }
                    .Assign(out divider),
            }
        }.Assign(out stack), 1f);

        Assert.Equal(title.DrawingRect.Width, stack.DrawingRect.Width, 1.5);
        Assert.Equal(title.DrawingRect.Width, divider.DrawingRect.Width, 1.5);
    }

    [Fact]
    public void Row_FillChildWithContent_SetsTheHeight()
    {
        SkiaStack column;
        SkiaLayout row;
        using var host = Host(new SkiaRow
        {
            VerticalOptions = LayoutOptions.Start,
            Children =
            {
                new SkiaShape { WidthRequest = 40, HeightRequest = 20 },
                new SkiaStack
                {
                    WidthRequest = 120,
                    VerticalOptions = LayoutOptions.Fill,
                    Children = { new SkiaLabel("One"), new SkiaLabel("Two"), new SkiaLabel("Three") }
                }.Assign(out column),
            }
        }.Assign(out row), 1f);

        Assert.True(column.DrawingRect.Height > 40, $"column {column.DrawingRect.Height}");
        Assert.Equal(column.DrawingRect.Height, row.DrawingRect.Height, 1);
    }

    [Fact]
    public void FillControlOnUnboundedAxis_KeepsItsPadding()
    {
        var label = new SkiaLabel("Some text") { FontSize = 16, HorizontalOptions = LayoutOptions.Fill };
        var card = new SkiaShape { Padding = new Thickness(16), HorizontalOptions = LayoutOptions.Fill, Children = { label } };
        using var host = Host(new SkiaLayer(), 1f);

        var cardWidth = card.Measure(float.PositiveInfinity, 500, 1f).Pixels.Width;
        var labelWidth = label.Measure(float.PositiveInfinity, 500, 1f).Pixels.Width;

        Assert.Equal(labelWidth + 32, cardWidth, 1);
    }
}
