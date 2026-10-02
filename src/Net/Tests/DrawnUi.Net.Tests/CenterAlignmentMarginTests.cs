using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A horizontally centered control with an asymmetric margin, in a cell barely wider than it
/// (ArtOfFoto timer settings: trash icon, Margin right 12, in a 32pt grid column). The centered box
/// overflowed the cell before the margin shift was applied and got truncated, clipping the glyph.
/// The arranged size must not drop below the measured one, like the vertical Center path. It may gain one
/// pixel: when the free space around a centered child is odd, the child takes that pixel (ca4b076e, 1.10.6.16).
/// </summary>
public class CenterAlignmentMarginTests
{
    [Fact]
    public void CenteredWithRightMargin_InNarrowCell_KeepsMeasuredWidth()
    {
        using var host = new HeadlessCanvasHost(400, 100, scale: 1f, background: Colors.Black);
        var icon = new SkiaControl
        {
            WidthRequest = 13,
            HeightRequest = 10,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 12, 0),
        };
        var grid = new SkiaLayout
        {
            Type = LayoutType.Grid,
            HorizontalOptions = LayoutOptions.Fill,
            HeightRequest = 38,
            ColumnSpacing = 8,
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(32)),
            },
        };
        icon.WithColumn(1);
        grid.Children.Add(icon);
        host.Canvas.Content = grid;

        for (int i = 0; i < 3; i++) host.RenderFrame(16);

        // 32 - 13 = 19 free pixels, odd: the center rule gives the child the odd pixel
        Assert.InRange(icon.DrawingRect.Width, 13, 14);
    }
}
