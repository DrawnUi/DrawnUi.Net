using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// An Auto row is as tall as its tallest child at the child's real width. A label wrapping in a star column used to
/// be measured against the row height its first pass gave (one line, at the grid's whole width) and was cut to
/// one line with a trail, while MaxLines allowed more.
/// </summary>
public class GridAutoRowWrapTests
{
    const string Text = "A long caption that needs two lines in its column";

    static (HeadlessCanvasHost host, SkiaLabel label, SkiaLayout grid) Scene(LayoutOptions labelVertical)
    {
        var host = new HeadlessCanvasHost(400, 300, scale: 1f, background: Colors.Black);
        SkiaLabel label = null;
        SkiaLayout grid = new SkiaGrid
        {
            HorizontalOptions = LayoutOptions.Fill,
            ColumnSpacing = 0,
            Children =
            {
                new SkiaLabel
                {
                    Text = Text,
                    FontSize = 16,
                    MaxLines = 3,
                    TextColor = Colors.White,
                    VerticalOptions = labelVertical,
                }.Assign(out label).SetGrid(0, 0),
                new SkiaShape { WidthRequest = 220, HeightRequest = 30, BackgroundColor = Colors.Gray }.SetGrid(1, 0),
            },
        }.WithColumnDefinitions("*,Auto");
        host.Canvas.Content = grid;
        host.AdvanceFrames(4);
        return (host, label, grid);
    }

    [Theory]
    [InlineData("Center")]
    [InlineData("Fill")]
    public void StarColumnLabel_WrapsAndTheAutoRowGrows(string vertical)
    {
        var (host, label, grid) = Scene(vertical == "Fill" ? LayoutOptions.Fill : LayoutOptions.Center);
        using (host)
        {
            Assert.True(label.LinesCount >= 2, $"{label.LinesCount} line(s): {string.Join(" | ", label.Lines.Select(l => l.Value))}");
            Assert.All(label.Lines, line => Assert.False(line.Value.EndsWith(SkiaLabel.Trail), $"'{line.Value}' was cut"));
            Assert.True(label.MeasuredSize.Pixels.Width <= 180 + 1, $"label {label.MeasuredSize.Pixels.Width} px in a 180 px column");
            Assert.True(grid.MeasuredSize.Pixels.Height >= label.MeasuredSize.Pixels.Height,
                $"grid {grid.MeasuredSize.Pixels.Height} px, label {label.MeasuredSize.Pixels.Height} px");
        }
    }
}
