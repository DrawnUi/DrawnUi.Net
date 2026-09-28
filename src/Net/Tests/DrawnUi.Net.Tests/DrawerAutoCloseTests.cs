using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// An open SkiaDrawer with AutoClose closes on a press outside its content, not when the mouse only moves there.
/// </summary>
public class DrawerAutoCloseTests
{
    [Fact]
    public void AutoClose_IgnoresHoverOutside_ClosesOnTapOutside()
    {
        using var host = new HeadlessCanvasHost(300, 400);
        SkiaDrawer drawer = null;
        host.Canvas.Content = new SkiaDrawer
        {
            Direction = DrawerDirection.FromBottom,
            HeaderSize = 40,
            AutoClose = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaShape
            {
                BackgroundColor = Colors.Gray,
                HeightRequest = 150,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.End,
            },
        }.Assign(out drawer);
        host.AdvanceFrames(4);

        drawer.IsOpen = true;
        host.AdvanceFrames(60);
        Assert.True(drawer.IsOpen);

        host.Canvas.HandleDesktopPointerMove(150, 50, false, 300, 400); // above the panel, inside the drawer
        host.AdvanceFrames(4);
        host.Canvas.HandleDesktopPointerMove(150, 60, false, 300, 400);
        host.AdvanceFrames(4);
        Assert.True(drawer.IsOpen, "the drawer closed on a hover outside its panel");

        new GestureRobot(host).Tap(150, 50);
        host.AdvanceFrames(4);
        Assert.False(drawer.IsOpen);
    }
}
