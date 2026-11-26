using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A child whose HeightRequest arrives after the first frames (a binding resolved when the page gets its data) takes
/// its height and pushes the next child down, also inside a picture-cached column that sits in a plain layer
/// (the DrawnUI Demo's carousel page: status bar spacer + nav bar).
/// </summary>
public class LateHeightRequestInCachedColumnTests
{
    [Theory]
    [InlineData(SkiaCacheType.Operations, false)]
    [InlineData(SkiaCacheType.None, false)]
    [InlineData(SkiaCacheType.Operations, true)]
    public void SpacerTakesLateHeight(SkiaCacheType cache, bool columnRoot)
    {
        var host = new HeadlessCanvasHost(400, 800, 1, Colors.Black);
        var spacer = new SkiaControl { HorizontalOptions = LayoutOptions.Fill };
        var nav = new SkiaLayout { HeightRequest = 50, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.Red };
        var block = new SkiaLayout
        {
            Type = LayoutType.Column, Spacing = 0, HorizontalOptions = LayoutOptions.Fill, UseCache = cache,
            Children = { spacer, nav }
        };
        var root = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
            Children = { block }
        };
        if (columnRoot)
            root.Type = LayoutType.Column;
        host.Canvas.Content = root;
        host.AdvanceFrames(4);

        spacer.HeightRequest = 62;
        host.AdvanceFrames(4);

        Assert.Equal(62, spacer.DrawingRect.Height, 1);
        Assert.Equal(62, nav.DrawingRect.Top, 1);
    }
}
