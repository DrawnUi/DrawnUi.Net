using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Overlapping children on a real canvas (moved from the old UnitTests project, whose canvas-less render path does not
/// draw the way DrawnUI does).
/// </summary>
public class LayoutOverlapTests
{
    private static SKColor PixelAt(HeadlessCanvasHost host, int x, int y)
    {
        using var bmp = SKBitmap.FromImage(host.Snapshot());
        return bmp.GetPixel(x, y);
    }

    /// <summary>An absolute layout draws its children by ZIndex: the red Fill shape (1) covers the yellow one (0).</summary>
    [Fact]
    public void Absolute_DrawsByZIndex()
    {
        using var host = new HeadlessCanvasHost(200, 200, scale: 1f, background: Colors.White);
        host.Canvas.Content = new SkiaLayout
        {
            BackgroundColor = Colors.Black,
            UseCache = SkiaCacheType.Image,
            Children = new List<SkiaControl>
            {
                new SkiaLabel { Text = "Tests", HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, ZIndex = 2 },
                new SkiaShape { ZIndex = 1, BackgroundColor = Colors.Red, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill },
                new SkiaShape { BackgroundColor = Colors.Yellow, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill },
            }
        };
        host.AdvanceFrames(3);

        Assert.Equal(SKColors.Red, PixelAt(host, 2, 2));
    }

    /// <summary>
    /// A column child pulled over the previous one by a negative margin (AddMarginTop = -its height) leaves an empty slot
    /// but still draws its full size, over the previous child. The column used to judge visibility by the slot, so it
    /// was never drawn.
    /// </summary>
    [Theory]
    [InlineData(SkiaCacheType.Image)]
    [InlineData(SkiaCacheType.None)]
    public void Column_NegativeMarginChild_DrawsOverThePreviousOne(SkiaCacheType cache)
    {
        using var host = new HeadlessCanvasHost(200, 300, scale: 1f, background: Colors.White);
        host.Canvas.Content = new SkiaLayout
        {
            Type = LayoutType.Column,
            BackgroundColor = Colors.Black,
            Spacing = 0,
            UseCache = cache,
            Children = new List<SkiaControl>
            {
                new SkiaShape { BackgroundColor = Colors.Green, HeightRequest = 100, LockRatio = 1 },
                new SkiaShape { AddMarginTop = -100, BackgroundColor = Colors.Red, HeightRequest = 100, LockRatio = 1 },
                new SkiaShape { BackgroundColor = Colors.Blue, HeightRequest = 100, LockRatio = -1 },
            }
        };
        host.AdvanceFrames(3);

        Assert.Equal(SKColors.Red, PixelAt(host, 50, 50));
        Assert.Equal(SKColors.Blue, PixelAt(host, 50, 150)); // the empty slot moved the next child up
    }
}
