using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaSvg.IconFilePath was a bindable property whose change handler only rebuilt the icon from SvgString,
/// so the path itself was never read. It now loads through the same loader as Source.
/// Assets/red-square.svg is copied next to the test assembly, where the desktop heads look for app files.
/// </summary>
public class SvgIconFilePathTests
{
    [Fact]
    public void IconFilePath_LoadsAndDrawsTheFile()
    {
        using var host = new HeadlessCanvasHost(200, 200, scale: 1f, background: Colors.Black);
        var svg = new SkiaSvg
        {
            IconFilePath = "Assets/red-square.svg",
            WidthRequest = 100,
            HeightRequest = 100,
        };
        host.Canvas.Content = new SkiaLayout
        {
            Type = LayoutType.Absolute,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { svg }
        };

        // the file is read asynchronously
        for (int i = 0; i < 200 && svg.Svg == null; i++)
        {
            host.RenderFrame(16);
            Thread.Sleep(10);
        }
        host.AdvanceFrames(3, 16);

        Assert.True(svg.HasContent, "svg text not loaded");
        Assert.NotNull(svg.Svg);

        using var image = host.Snapshot();
        using var pixmap = image.PeekPixels();
        var inside = pixmap.GetPixelColor(50, 50);
        Assert.True(inside.Red > 200 && inside.Green < 50 && inside.Blue < 50, $"inside {inside}");
        var outside = pixmap.GetPixelColor(150, 150);
        Assert.True(outside.Red < 50, $"outside {outside}");
    }
}
