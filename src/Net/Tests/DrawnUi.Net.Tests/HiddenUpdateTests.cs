using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// An Update() from a control that is not drawn (IsVisible false, or under a hidden ancestor) marks it dirty and
/// requests no canvas frame. A live control behind a pushed screen (a camera preview updating ~30 times a second)
/// used to redraw the whole canvas on every update. Showing it again draws its latest state; hiding still works.
/// </summary>
public class HiddenUpdateTests
{
    /// <summary>A live source updating as a camera preview does: NeedUpdate = false, then Update() (SkiaCamera.UpdatePreview).</summary>
    sealed class LiveShape : SkiaShape
    {
        public void NewFrame()
        {
            NeedUpdate = false;
            Update();
        }
    }

    static (HeadlessCanvasHost host, SkiaLayout screen, LiveShape live) Scene(SkiaCacheType screenCache)
    {
        var host = new HeadlessCanvasHost(200, 200, scale: 1f, background: Colors.Black);
        var live = new LiveShape
        {
            BackgroundColor = Colors.Blue,
            WidthRequest = 100,
            HeightRequest = 100,
            UseCache = SkiaCacheType.Operations,
        };
        var screen = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            UseCache = screenCache,
            Children = { new SkiaLayout { Children = { live } } }, // an uncached layer in between
        };
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            UseCache = SkiaCacheType.Image,
            Children = { screen },
        };
        Settle(host);
        return (host, screen, live);
    }

    static void Settle(HeadlessCanvasHost host)
    {
        host.AdvanceFrames(3);
        for (int i = 0; i < 30 && host.NeedsFrame; i++)
            host.RenderFrame(16);
    }

    static SKColor PixelAt(HeadlessCanvasHost host, int x, int y)
    {
        using var image = host.Snapshot();
        using var pixmap = image.PeekPixels();
        return pixmap.GetPixelColor(x, y);
    }

    [Theory]
    [InlineData(SkiaCacheType.None)]
    [InlineData(SkiaCacheType.Image)]
    public void HiddenAncestor_UpdatesRequestNoFrame(SkiaCacheType screenCache)
    {
        var (host, screen, live) = Scene(screenCache);
        using (host)
        {
            screen.IsVisible = false;
            Settle(host);
            Assert.False(host.NeedsFrame);

            for (int i = 0; i < 10; i++)
            {
                live.NewFrame();
                Assert.False(host.NeedsFrame, $"update {i} asked for a frame");
            }

            live.IsVisible = false; // hidden itself, not only under a hidden ancestor
            screen.IsVisible = true;
            Settle(host);
            live.NewFrame();
            Assert.False(host.NeedsFrame);
        }
    }

    [Fact]
    public void VisibleControl_UpdateRequestsFrame()
    {
        var (host, _, live) = Scene(SkiaCacheType.Image);
        using (host)
        {
            Assert.False(host.NeedsFrame);
            live.NewFrame();
            Assert.True(host.NeedsFrame);
        }
    }

    [Theory]
    [InlineData(SkiaCacheType.None)]
    [InlineData(SkiaCacheType.Image)]
    public void Reveal_DrawsTheStateChangedWhileHidden(SkiaCacheType screenCache)
    {
        var (host, screen, live) = Scene(screenCache);
        using (host)
        {
            Assert.Equal(SKColors.Blue, PixelAt(host, 50, 50));

            screen.IsVisible = false;
            Settle(host);
            Assert.Equal(SKColors.Black, PixelAt(host, 50, 50));

            live.BackgroundColor = Colors.Red; // changes (and updates) while hidden
            Settle(host);

            screen.IsVisible = true;
            Settle(host);
            Assert.Equal(SKColors.Red, PixelAt(host, 50, 50));
        }
    }

    [Fact]
    public void HidingAChild_UnderCachedParents_RemovesIt()
    {
        var (host, _, live) = Scene(SkiaCacheType.Image);
        using (host)
        {
            Assert.Equal(SKColors.Blue, PixelAt(host, 50, 50));
            live.IsVisible = false;
            Settle(host);
            Assert.Equal(SKColors.Black, PixelAt(host, 50, 50));
            live.IsVisible = true;
            Settle(host);
            Assert.Equal(SKColors.Blue, PixelAt(host, 50, 50));
        }
    }
}
