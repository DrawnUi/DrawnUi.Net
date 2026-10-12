using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// FiltersCamera (2026-10-11): a hidden SkiaDrawer made visible and opened in the same tick, before it was ever
/// measured. Its snap points came from its zero size (both zero), SetIsOpen read that as closed and flipped IsOpen
/// back to false while the drawer stayed up, and a later IsOpen = false changed nothing: it never closed.
/// </summary>
public class DrawerOpenBeforeMeasureTests
{
    [Theory]
    [InlineData(DrawerDirection.FromBottom, false)]
    [InlineData(DrawerDirection.FromRight, false)]
    [InlineData(DrawerDirection.FromBottom, true)] // shown before, then hidden: shown and opened in one tick again
    public void OpenedInTheTickItBecomesVisible_StaysOpen_ThenCloses(DrawerDirection direction, bool shownBefore)
    {
        using var host = new HeadlessCanvasHost(400, 700, scale: 1f, background: Colors.Black);
        SkiaDrawer drawer = null;
        host.Canvas.Content = new SkiaLayer
        {
            Children =
            {
                new SkiaDrawer
                {
                    IsVisible = false,
                    Direction = direction,
                    HeaderSize = 0,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Content = new SkiaLayout
                    {
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        BackgroundColor = Colors.Gray,
                    },
                }.Assign(out drawer)
            }
        };
        host.AdvanceFrames(10);

        if (shownBefore)
        {
            drawer.IsVisible = true;
            host.AdvanceFrames(10);
            drawer.IsVisible = false;
            host.AdvanceFrames(10);
        }

        drawer.IsVisible = true;
        drawer.IsOpen = true;
        host.AdvanceFrames(60);

        Assert.True(drawer.IsOpen, "IsOpen flipped back to false");
        Assert.Equal(0, drawer.TranslationX, 1);
        Assert.Equal(0, drawer.TranslationY, 1);

        drawer.IsOpen = false;
        host.AdvanceFrames(90);

        Assert.False(drawer.IsOpen);
        if (direction == DrawerDirection.FromBottom)
            Assert.Equal(700, drawer.TranslationY, 1);
        else
            Assert.Equal(400, drawer.TranslationX, 1);
    }
}
