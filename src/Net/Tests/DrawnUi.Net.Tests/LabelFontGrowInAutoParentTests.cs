using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A label whose font grows inside a parent sized by its content (no width of its own) gets the room it now needs:
/// the parent measures again, the text does not wrap to the old width (Dungeon Run's title going from a narrow
/// window to a wide one).
/// </summary>
public class LabelFontGrowInAutoParentTests
{
    [Theory]
    [InlineData(SkiaCacheType.None)]
    [InlineData(SkiaCacheType.GPU)]
    public void FontGrows_ParentWidensAndTextStaysOnOneLine(SkiaCacheType cache)
    {
        using var host = new HeadlessCanvasHost(900, 400);
        SkiaLabel label = null;
        host.Canvas.Content = new SkiaStack
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            UseCache = cache,
            Children =
            {
                new SkiaLayer
                {
                    HeightRequest = 120,
                    UseCache = cache,
                    Children =
                    {
                        new SkiaLabel("DUNGEON RUN")
                        {
                            FontSize = 32,
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center,
                            HorizontalTextAlignment = DrawTextAlignment.Center,
                        }.Assign(out label),
                    },
                },
            },
        };
        host.AdvanceFrames(4);
        var narrow = label.MeasuredSize.Units.Width;

        label.FontSize = 44;
        host.AdvanceFrames(4);

        Assert.Single(label.Lines);
        Assert.True(label.MeasuredSize.Units.Width > narrow * 1.2, $"label {label.MeasuredSize.Units.Width} wide, was {narrow}");
    }
}
