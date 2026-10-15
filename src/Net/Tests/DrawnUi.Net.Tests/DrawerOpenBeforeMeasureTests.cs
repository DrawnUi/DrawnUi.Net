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

    /// <summary>The FiltersCamera Adjust sheet: a fixed-height bottom drawer with margins over an Absolute layout.</summary>
    [Fact]
    public void FixedHeightBottomSheet_OpenedInTheTickItBecomesVisible_StaysOpen_ThenCloses()
    {
        using var host = new HeadlessCanvasHost(485, 693, scale: 1.25f, background: Colors.Black);
        SkiaDrawer drawer = null;
        var closed = false;
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaDrawer
                {
                    ZIndex = 55,
                    IsVisible = false,
                    Direction = DrawerDirection.FromBottom,
                    IsClippedToBounds = true,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.End,
                    HeightRequest = 258,
                    Margin = new Thickness(12, 0, 12, 108),
                    Content = new SkiaShape
                    {
                        UseCache = SkiaCacheType.Image,
                        BackgroundColor = Colors.Gray,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.End,
                        HeightRequest = 258,
                    },
                }.Assign(out drawer)
            }
        };
        drawer.StateTransitionComplete += (_, isOpen) => closed |= !isOpen;
        host.AdvanceFrames(10);

        drawer.IsVisible = true;
        drawer.IsOpen = true;
        host.AdvanceFrames(60);

        Assert.True(drawer.IsOpen, "IsOpen flipped back to false");
        Assert.Equal(0, drawer.TranslationY, 1);

        drawer.IsOpen = false;
        host.AdvanceFrames(90);

        Assert.False(drawer.IsOpen);
        Assert.True(drawer.TranslationY >= 258, $"not slid out: {drawer.TranslationY}"); // its measured size includes the margin
        Assert.True(closed, "StateTransitionComplete(false) never came");
    }
}
