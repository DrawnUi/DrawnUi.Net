using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A margin that is not a whole pixel at the rendering scale (18 pt at 1.25 = 22.5 px) was added to the measured size
/// rounded (22) but taken from the measuring constraint raw (22.5). A control measured again at its own measured
/// size, as a grid measures its Auto rows, then got half a pixel less for its content and lost a line of text:
/// FiltersCamera onboarding, a headline + caption stack on a grid Auto row, caption cut to one line with a trail.
/// </summary>
public class GridAutoRowHeadlineTests
{
    const string Caption = "Presets, looks, distortions and vignettes. Tap MENU at the start of the strip.";

    static SkiaStack Headline(double bottomMargin, out SkiaRichLabel caption) => new SkiaStack
    {
        Spacing = 10,
        Margin = new Thickness(0, 0, 0, bottomMargin),
        UseCache = SkiaCacheType.Image,
        Children =
        {
            new SkiaRichLabel { Text = "EVERYTHING IN\n**ONE MENU**", FontSize = 42, TextColor = Colors.White },
            new SkiaRichLabel { Text = Caption, FontSize = 15, TextColor = Colors.White }.Assign(out caption),
        },
    };

    static void AssertNotCut(SkiaLabel label)
    {
        var lines = string.Join(" | ", label.Lines?.Select(l => l.Value) ?? Array.Empty<string>());
        Assert.True(label.LinesCount >= 2, $"{label.LinesCount} line(s): {lines}");
        Assert.All(label.Lines, line => Assert.False(line.Value.EndsWith(SkiaLabel.Trail), $"'{line.Value}' was cut"));
    }

    /// <summary>The onboarding slide as FiltersCamera had it (MAUI Windows, 607 x 866 px at 1.25).</summary>
    [Fact]
    public void OnboardingSlide_CaptionOnAutoRow_Wraps()
    {
        using var host = new HeadlessCanvasHost(607, 866, scale: 1.25f, background: Colors.Black);
        host.Canvas.Content = new SkiaGrid
        {
            VerticalOptions = LayoutOptions.Fill,
            Padding = new Thickness(20, 0),
            RowSpacing = 0,
            Children =
            {
                Headline(18, out var caption).SetGrid(0, 0),
                new SkiaShape
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    CornerRadius = 18,
                    Margin = new Thickness(0, 0, 0, 12),
                    BackgroundColor = Colors.Gray,
                }.SetGrid(0, 1),
                new SkiaStack
                {
                    Spacing = 4,
                    Margin = new Thickness(2, 12, 2, 4),
                    UseCache = SkiaCacheType.Image,
                    Children =
                    {
                        new SkiaRichLabel { Text = "DISTORT", FontSize = 20, CharacterSpacing = 3, TextColor = Colors.White },
                        new SkiaRichLabel { Text = "Pick what to change, then swipe through its options.", FontSize = 13, TextColor = Colors.White },
                    },
                }.SetGrid(0, 2),
            },
        }.WithColumnDefinitions("*").WithRowDefinitions("Auto,*,Auto");
        host.AdvanceFrames(6);

        AssertNotCut(caption);
    }

    /// <summary>Measured again at its own measured size, a control keeps that size and its text.</summary>
    [Theory]
    [InlineData(18, 1.25f)]
    [InlineData(2, 1.25f)]
    [InlineData(1.3, 1.25f)]
    [InlineData(7, 1.5f)]
    [InlineData(5, 2.75f)]
    public void MeasuredAgainAtOwnSize_KeepsContent(double bottomMargin, float scale)
    {
        using var host = new HeadlessCanvasHost(607, 866, scale: scale, background: Colors.Black);
        var stack = Headline(bottomMargin, out var caption);
        host.Canvas.Content = new SkiaLayer { Children = { stack } };
        host.AdvanceFrames(2);

        var width = (float)Math.Round(445 * scale);
        var free = stack.Measure(width, float.PositiveInfinity, scale);
        var again = stack.Measure(width, free.Pixels.Height, scale);

        Assert.Equal(free.Pixels.Height, again.Pixels.Height);
        AssertNotCut(caption);
    }
}
