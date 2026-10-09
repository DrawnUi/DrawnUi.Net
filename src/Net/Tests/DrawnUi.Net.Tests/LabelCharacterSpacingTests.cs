using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// CharacterSpacing widens the gap after every glyph. The label must MEASURE the spaced width it DRAWS, and decide
/// wrapping and truncation with it, or a spaced title overflows its column into the neighbor (FiltersCamera: a
/// SkiaRichLabel title "ESTILIZAR UMA FOTO", spacing 3, in a 151 pt star column ran under the toggle beside it).
/// </summary>
public class LabelCharacterSpacingTests
{
    const string Title = "ESTILIZAR UMA FOTO";

    /// <summary>Rightmost column with ink, -1 when nothing is drawn.</summary>
    static int InkRight(HeadlessCanvasHost host)
    {
        using var image = host.Snapshot();
        using var pixmap = image.PeekPixels();
        for (int x = pixmap.Width - 1; x >= 0; x--)
            for (int y = 0; y < pixmap.Height; y++)
                if (pixmap.GetPixelColor(x, y).Red > 80)
                    return x;
        return -1;
    }

    public static IEnumerable<object[]> Labels()
    {
        yield return new object[] { "SkiaLabel", (Func<SkiaLabel>)(() => new SkiaLabel { Text = Title }) };
        yield return new object[] { "SkiaLabel spans", (Func<SkiaLabel>)(() => new SkiaLabel
        {
            Spans = { new TextSpan { Text = "ESTILIZAR " }, new TextSpan { Text = "UMA FOTO", IsBold = true } }
        }) };
        yield return new object[] { "SkiaRichLabel", (Func<SkiaLabel>)(() => new SkiaRichLabel { Text = Title }) };
    }

    [Theory]
    [MemberData(nameof(Labels))]
    public void SpacedWidth_MeasuredEqualsDrawn(string name, Func<SkiaLabel> make)
    {
        using var host = new HeadlessCanvasHost(700, 200, scale: 1f, background: Colors.Black);
        var label = make();
        label.FontSize = 32;
        label.CharacterSpacing = 3;
        label.TextColor = Colors.White;
        label.UseCache = SkiaCacheType.None;
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { label },
        };
        host.AdvanceFrames(4);

        var measured = label.MeasuredSize.Pixels.Width;
        var ink = InkRight(host) + 1;
        Assert.True(ink > 0, $"{name}: nothing drawn");
        // the last glyph's side bearing is not ink: the ink may end a few pixels before the measured edge
        Assert.True(ink <= measured + 1 && measured - ink < 12, $"{name}: measured {measured} px, ink ends at {ink} px");
    }

    /// <summary>The FiltersCamera title row: a grid "*,Auto", the spaced title in the star column with
    /// MaxLines 2 and right padding, a fixed-size neighbor in the Auto column. The spaced title is wider than its
    /// column (unspaced it would fit): it must wrap to two lines inside the column, the row growing for them.</summary>
    [Theory]
    [MemberData(nameof(Labels))]
    public void SpacedTitle_WrapsInsideItsStarColumn(string name, Func<SkiaLabel> make)
    {
        const float scale = 1.25f;
        const double column = 151; // pt, the star column
        using var host = new HeadlessCanvasHost((int)(402 * scale), (int)(200 * scale), scale, Colors.Black);
        var label = make();
        label.UseCache = SkiaCacheType.Image;
        label.FontSize = 12;
        label.CharacterSpacing = 3;
        label.MaxLines = 2;
        label.TextColor = Colors.White;
        label.Padding = new Thickness(0, 8, 12, 8);
        label.HorizontalOptions = LayoutOptions.Start;
        label.VerticalOptions = LayoutOptions.Center;
        host.Canvas.Content = new SkiaGrid
        {
            HorizontalOptions = LayoutOptions.Fill,
            Margin = new Thickness(20, 0, 20, 12),
            ColumnSpacing = 0,
            Children =
            {
                label.SetGrid(0, 0),
                new SkiaShape
                {
                    WidthRequest = 402 - 40 - column, HeightRequest = 38,
                    BackgroundColor = Colors.Transparent, HorizontalOptions = LayoutOptions.End,
                }.SetGrid(1, 0),
            },
        }.WithColumnDefinitions("*,Auto");
        host.AdvanceFrames(4);

        var contentRight = (int)Math.Round((20 + column - 12) * scale); // column right edge minus the padding
        var ink = InkRight(host) + 1;
        Assert.Equal(2, label.LinesCount);
        Assert.True(ink > 0, $"{name}: nothing drawn");
        Assert.True(ink <= contentRight + 1, $"{name}: ink ends at {ink} px, the text area ends at {contentRight} px");
        Assert.All(label.Lines, line => Assert.False(line.Value.EndsWith(SkiaLabel.Trail), $"{name}: '{line.Value}' was cut"));
    }

    /// <summary>One line only: the cut with the trail must keep the spaced text inside the width.</summary>
    [Theory]
    [MemberData(nameof(Labels))]
    public void SpacedText_TruncatesAtTheSpacedWidth(string name, Func<SkiaLabel> make)
    {
        using var host = new HeadlessCanvasHost(400, 100, scale: 1f, background: Colors.Black);
        var label = make();
        label.FontSize = 20;
        label.CharacterSpacing = 3;
        label.MaxLines = 1;
        label.WidthRequest = 150;
        label.TextColor = Colors.White;
        label.UseCache = SkiaCacheType.None;
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { label },
        };
        host.AdvanceFrames(4);

        var ink = InkRight(host) + 1;
        Assert.True(ink > 0, $"{name}: nothing drawn");
        Assert.True(ink <= 151, $"{name}: ink ends at {ink} px in a 150 px label");
    }
}
