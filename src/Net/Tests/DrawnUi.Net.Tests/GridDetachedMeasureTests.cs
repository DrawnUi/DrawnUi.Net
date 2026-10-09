using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A Grid measured by hand with an explicit scale must use that scale for its cells, like Column and
/// Row do, and never the children's own RenderingScale, which falls back to the global screen density.
/// With density 0 (a headless process that never initialized the engine) the grid used to measure
/// every cell with scale 0 and come out 0x0 with NaN cell widths.
/// </summary>
public class GridDetachedMeasureTests
{
    static SkiaControl Box(float width, float height) => new SkiaControl
    {
        WidthRequest = width,
        HeightRequest = height,
    };

    [Fact]
    public void Grid_MeasuredWithExplicitScale_UsesItForCells_WhenScreenDensityIsZero()
    {
        var density = Super.Screen.Density;
        try
        {
            Super.Screen.Density = 0;

            var grid = new SkiaLayout
            {
                Type = LayoutType.Grid,
                HorizontalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl>
                {
                    Box(100, 40).SetGrid(0, 0),
                    Box(100, 60).SetGrid(1, 0),
                }
            }.WithColumnDefinitions("*,*");

            var measured = grid.Measure(1000, float.PositiveInfinity, 2f);

            Assert.False(float.IsNaN(measured.Pixels.Width));
            Assert.Equal(1000, measured.Pixels.Width);
            Assert.Equal(120, measured.Pixels.Height); // tallest cell 60 units at scale 2

            var structure = grid.GridStructureMeasured;
            Assert.NotNull(structure);
            foreach (var column in structure.Columns)
                Assert.False(double.IsNaN(column.Size));
        }
        finally
        {
            Super.Screen.Density = density;
        }
    }

    [Fact]
    public void Grid_StarColumns_SplitTheWidth_WhenScreenDensityIsZero()
    {
        var density = Super.Screen.Density;
        try
        {
            Super.Screen.Density = 0;

            var grid = new SkiaLayout
            {
                Type = LayoutType.Grid,
                ColumnSpacing = 0,
                HorizontalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl>
                {
                    new SkiaControl { HorizontalOptions = LayoutOptions.Fill, HeightRequest = 10 }.SetGrid(0, 0),
                    new SkiaControl { HorizontalOptions = LayoutOptions.Fill, HeightRequest = 10 }.SetGrid(1, 0),
                }
            }.WithColumnDefinitions("*,3*");

            grid.Measure(800, float.PositiveInfinity, 1f);
            grid.Arrange(new SKRect(0, 0, 800, grid.MeasuredSize.Pixels.Height), 800, grid.MeasuredSize.Pixels.Height, 1f);

            var columns = grid.GridStructureMeasured.Columns;
            Assert.Equal(2, columns.Length);
            Assert.Equal(200, columns[0].Size, 0.5);
            Assert.Equal(600, columns[1].Size, 0.5);
        }
        finally
        {
            Super.Screen.Density = density;
        }
    }
}
