using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// ImageComposite: LastCompositeRecord says whether the last record was full or partial and which children it redrew.
/// A child changing only its transform (Rotation) is redrawn alone, its far siblings are not.
/// On the Net heads Rotation used to be a plain property: setting it never repainted, so the record stayed full.
/// </summary>
public class CompositeRecordTests
{
    [Fact]
    public void RotatingChild_PartialRecord_RedrawsOnlyIt()
    {
        using var host = new HeadlessCanvasHost(400, 200, scale: 1f, background: Colors.Black);
        SkiaShape spinner = null;
        var tiles = Enumerable.Range(0, 4).Select(i => (SkiaControl)new SkiaShape
        {
            BackgroundColor = Colors.SteelBlue,
            WidthRequest = 40,
            HeightRequest = 40,
            Margin = new Thickness(10 + i * 60, 10, 0, 0),
        }).ToList();
        tiles.Add(new SkiaShape
        {
            BackgroundColor = Colors.Orange,
            WidthRequest = 40,
            HeightRequest = 40,
            Margin = new Thickness(300, 120, 0, 0),
        }.Assign(out spinner));

        var layer = new SkiaLayer
        {
            UseCache = SkiaCacheType.ImageComposite,
            VerticalOptions = LayoutOptions.Fill,
            Children = tiles,
        };
        host.Canvas.Content = layer;

        host.AdvanceFrames(3);
        Assert.False(layer.LastCompositeRecord.Partial);
        Assert.Equal(5, layer.LastCompositeRecord.Redrawn.Count);

        spinner.Rotation = 30;
        host.AdvanceFrames(2);

        Assert.True(layer.LastCompositeRecord.Partial);
        Assert.Equal(new[] { spinner }, layer.LastCompositeRecord.Redrawn);
    }
}
