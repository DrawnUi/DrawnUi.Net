using DrawnUi.Controls;
using DrawnUi.Views;
using DrawnUi.Draw;
using DrawnUi.Testing;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A full-height grid "Auto,*,Auto,Auto" on a narrow screen (a phone): its Auto rows hold a wrapping label and two
/// button wraps. The Auto rows take the height of their content at the screen's width, the star row what is left.
/// </summary>
public class GridAutoRowsNarrowTests
{
    private readonly ITestOutputHelper _out;
    public GridAutoRowsNarrowTests(ITestOutputHelper o) { _out = o; }

    const string Long = "A glTF model drawn in 3D inside DrawnUI, driven by the app. Drag it to turn it, tap it to hit it.";

    static SkiaWrap Buttons(int count) => new()
    {
        Spacing = 8,
        HorizontalOptions = LayoutOptions.Center,
        Children = Enumerable.Range(0, count).Select(_ => (SkiaControl)new SkiaShape { WidthRequest = 100, HeightRequest = 40, BackgroundColor = Colors.Gray }).ToList(),
    };

    [Theory]
    // the implicit Auto column: it gets the grid's width only after the rows were measured
    [InlineData(null, 390, 844, 2, 136, 88)]
    [InlineData("*", 390, 844, 2, 136, 88)]
    [InlineData(null, 1280, 796, 1, 40, 40)]
    [InlineData("*", 1280, 796, 1, 40, 40)]
    public void AutoRows_TakeTheirContentAtTheScreenWidth_StarRowGetsTheRest(string columns, int width, int height,
        int subtitleLines, float movesHeight, float skinsHeight)
    {
        using var host = new HeadlessCanvasHost(width, height);
        SkiaLabel subtitle, news;
        SkiaControl star, bottom;
        SkiaWrap moves, skins;
        var grid = new SkiaGrid
        {
            Padding = new Thickness(20),
            RowSpacing = 10,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaStack
                {
                    Spacing = 8,
                    Children =
                    {
                        new SkiaLabel("BOSS FIGHT") { FontSize = 30, HorizontalOptions = LayoutOptions.Center },
                        new SkiaLabel(Long) { FontSize = 15, HorizontalOptions = LayoutOptions.Center, HorizontalTextAlignment = DrawTextAlignment.Center }.Assign(out subtitle),
                    }
                }.SetGrid(0, 0),
                new SkiaLayout { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill }.Assign(out star).SetGrid(0, 1),
                new SkiaLabel("Tap the dragon to hit it.") { FontSize = 15, HorizontalOptions = LayoutOptions.Center }.Assign(out news).SetGrid(0, 2),
                new SkiaStack
                {
                    Spacing = 10,
                    Children =
                    {
                        new SkiaLabel("MOVES") { FontSize = 13, HorizontalOptions = LayoutOptions.Center },
                        Buttons(7).Assign(out moves),
                        new SkiaLabel("SKIN") { FontSize = 13, HorizontalOptions = LayoutOptions.Center },
                        Buttons(6).Assign(out skins),
                    }
                }.Assign(out bottom).SetGrid(0, 3),
            }
        }.WithRowDefinitions("Auto,*,Auto,Auto");
        if (columns != null)
            grid.WithColumnDefinitions(columns);
        host.Canvas.Content = grid;
        host.AdvanceFrames(6);

        string R(SkiaControl c) => $"{c.DrawingRect.Left:0} {c.DrawingRect.Top:0} {c.DrawingRect.Width:0}x{c.DrawingRect.Height:0} measured {c.MeasuredSize.Pixels.Width:0}x{c.MeasuredSize.Pixels.Height:0}";
        _out.WriteLine($"[{columns}] subtitle lines {subtitle.LinesCount} {R(subtitle)}");
        _out.WriteLine($"star {R(star)}");
        _out.WriteLine($"bottom {R(bottom)}");
        _out.WriteLine($"moves {R(moves)}");
        _out.WriteLine($"skins {R(skins)}");

        Assert.Equal(subtitleLines, subtitle.LinesCount);
        Assert.Equal(movesHeight, moves.DrawingRect.Height, 1f);
        Assert.Equal(skinsHeight, skins.DrawingRect.Height, 1f);
        // the Auto rows end at the grid's bottom padding: the star row took the rest, not more
        Assert.Equal(height - 20, skins.DrawingRect.Bottom, 1f);
        Assert.True(star.DrawingRect.Height > 100, $"star row {star.DrawingRect.Height}");
        Assert.Equal(star.DrawingRect.Bottom + 10, news.DrawingRect.Top, 1f);
    }
}

