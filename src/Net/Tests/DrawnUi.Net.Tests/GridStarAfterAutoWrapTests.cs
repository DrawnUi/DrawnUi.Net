using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A grid of a set height with rows "Auto,*,Auto" whose last Auto row holds a wrap that only wraps at the grid's width:
/// the rows fit inside the grid (the * row gives up the room the wrapped row needs), nothing is drawn past its bottom.
/// </summary>
public class GridStarAfterAutoWrapTests
{
    [Fact]
    public void StarRowShrinks_WhenAnAutoRowWrapsAtTheFinalWidth()
    {
        using var host = new HeadlessCanvasHost(300, 600, scale: 1f, background: Colors.Black);
        SkiaControl top = null, middle = null, bottom = null;
        host.Canvas.Content = new SkiaGrid
        {
            HeightRequest = 500,
            RowSpacing = 0,
            Children =
            {
                new SkiaShape { HeightRequest = 50, HorizontalOptions = LayoutOptions.Fill }.Assign(out top).SetGrid(0, 0),
                new SkiaShape { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill }.Assign(out middle).SetGrid(0, 1),
                new SkiaStack
                {
                    Children =
                    {
                        new SkiaWrap
                        {
                            Spacing = 8,
                            Children = Enumerable.Range(0, 7).Select(i => (SkiaControl)new SkiaShape { WidthRequest = 100, HeightRequest = 40 }).ToList(),
                        }
                    }
                }.Assign(out bottom).SetGrid(0, 2),
            }
        }.WithRowDefinitions("Auto,*,Auto");
        host.AdvanceFrames(3);

        var grid = top.Parent as SkiaControl;
        var g = grid.GetAccessibilityPixelRect();
        var m = middle.GetAccessibilityPixelRect();
        var b = bottom.GetAccessibilityPixelRect();
        Assert.True(b.Height > 100, $"the wrap took several lines: {b}");
        Assert.True(b.Bottom <= g.Bottom + 0.5f, $"the last row ends inside the grid: row {b}, grid {g}");
        Assert.True(m.Bottom <= b.Top + 0.5f, $"the * row ends where the last row starts: * {m}, last {b}");
    }

    /// <summary>The Boss page's shape: header, a * row, a line, then centered wraps of buttons in a stack.</summary>
    [Fact]
    public void BossLikeGrid_FitsInsideItsHeight()
    {
        using var host = new HeadlessCanvasHost(390, 700, scale: 1f, background: Colors.Black);
        SkiaControl last = null, star = null;
        SkiaWrap Buttons(int n, float w) => new()
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center,
            Children = Enumerable.Range(0, n).Select(i => (SkiaControl)new SkiaShape { WidthRequest = w, HeightRequest = 44 }).ToList(),
        };
        host.Canvas.Content = new SkiaGrid
        {
            HeightRequest = 650,
            Padding = new Thickness(20),
            RowSpacing = 10,
            Children =
            {
                new SkiaShape { HeightRequest = 90, HorizontalOptions = LayoutOptions.Fill }.SetGrid(0, 0),
                new SkiaShape { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill }.Assign(out star).SetGrid(0, 1),
                new SkiaShape { HeightRequest = 20, WidthRequest = 150 }.SetGrid(0, 2),
                new SkiaStack
                {
                    Spacing = 10,
                    Children = { Buttons(7, 100), Buttons(4, 100), Buttons(2, 124) },
                }.Assign(out last).SetGrid(0, 3),
            }
        }.WithRowDefinitions("Auto,*,Auto,Auto");
        host.AdvanceFrames(3);

        var g = (last.Parent as SkiaControl).GetAccessibilityPixelRect();
        var b = last.GetAccessibilityPixelRect();
        var m = star.GetAccessibilityPixelRect();
        Assert.True(b.Bottom <= g.Bottom - 20 + 0.5f, $"the buttons end inside the grid's padding: buttons {b}, grid {g}, star {m}");
        Assert.True(m.Height > 0, $"the * row keeps room: {m}");
    }
}
