using System.Drawing;
using AppoMobi.Gestures;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A touch long press opens the context menu once (drawnui-cross 6cb): raised by the canvas after the long press when
/// the browser did not (iOS Safari), never a second time when it did (Android Chrome fires contextmenu itself).
/// </summary>
public class TouchLongPressContextMenuTests
{
    private static (HeadlessCanvasHost Host, SkiaLayer Box, List<ContextMenuSource> Menus) Create()
    {
        var host = new HeadlessCanvasHost(300, 300, scale: 1f, background: Colors.Black);
        var menus = new List<ContextMenuSource>();
        var box = new SkiaLayer { WidthRequest = 200, HeightRequest = 200, BackgroundColor = Colors.Gray };
        box.OnContextMenu((me, e) =>
        {
            menus.Add(e.Source);
            return true;
        });
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { box } };
        host.AdvanceFrames(3);
        return (host, box, menus);
    }

    private static TouchActionEventArgs Touch(TouchActionType type, float x, float y) => new(1, type, new PointF(x, y), null, 1f)
    {
        StartingLocation = new PointF(x, y),
        NumberOfTouches = 1,
        IsInsideView = true,
        Pointer = new PointerData { DeviceType = PointerDeviceType.Touch },
    };

    private static void Send(HeadlessCanvasHost host, TouchActionResult action, TouchActionEventArgs args)
    {
        host.Canvas.OnGestureEvent(args.Type, args, action);
        host.RenderFrame();
    }

    [Fact]
    public void LongPress_WithoutBrowserMenu_RaisesOneTouchMenu()
    {
        var (host, _, menus) = Create();
        using var __ = host;
        Send(host, TouchActionResult.Down, Touch(TouchActionType.Pressed, 100, 100));
        Send(host, TouchActionResult.LongPressing, Touch(TouchActionType.Pressed, 100, 100));
        Send(host, TouchActionResult.Up, Touch(TouchActionType.Released, 100, 100));
        Assert.Equal(new[] { ContextMenuSource.Touch }, menus);
    }

    [Fact]
    public void BrowserMenuFirst_ThenLongPress_StaysOneMenu()
    {
        var (host, _, menus) = Create();
        using var __ = host;
        Send(host, TouchActionResult.Down, Touch(TouchActionType.Pressed, 100, 100));
        var browser = Touch(TouchActionType.ContextMenu, 100, 100);
        Send(host, TouchActionResult.ContextMenu, browser);
        Assert.True(browser.Handled);
        Send(host, TouchActionResult.LongPressing, Touch(TouchActionType.Pressed, 100, 100));
        Assert.Single(menus);
    }

    [Fact]
    public void LongPressFirst_ThenBrowserMenu_StaysOneMenu_AndKeepsItsAnswer()
    {
        var (host, _, menus) = Create();
        using var __ = host;
        Send(host, TouchActionResult.Down, Touch(TouchActionType.Pressed, 100, 100));
        Send(host, TouchActionResult.LongPressing, Touch(TouchActionType.Pressed, 100, 100));
        var browser = Touch(TouchActionType.ContextMenu, 100, 100);
        Send(host, TouchActionResult.ContextMenu, browser);
        Assert.Single(menus);
        Assert.True(browser.Handled); // the browser's own menu stays suppressed: the app took the first one
    }

    [Fact]
    public void NextPress_OpensItsOwnMenu()
    {
        var (host, _, menus) = Create();
        using var __ = host;
        for (var i = 0; i < 2; i++)
        {
            Send(host, TouchActionResult.Down, Touch(TouchActionType.Pressed, 100, 100));
            Send(host, TouchActionResult.LongPressing, Touch(TouchActionType.Pressed, 100, 100));
            Send(host, TouchActionResult.Up, Touch(TouchActionType.Released, 100, 100));
        }
        Assert.Equal(2, menus.Count);
    }

    [Fact]
    public void BrowserMenuFirst_OnText_SelectsTheWordBeforeTheMenu_AndSkipsTheLaterLongPress()
    {
        using var host = new HeadlessCanvasHost(420, 200, scale: 1f, background: Colors.Black);
        string seen = null;
        var longPresses = 0;
        var label = new SkiaLabel { Text = "Hello wonderful world", FontSize = 20, WidthRequest = 360, AccessibilityTextSelectable = true };
        var root = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { label } };
        root.OnContextMenu((me, e) =>
        {
            seen = label.SelectedText;
            return true;
        });
        root.OnLongPressing(me => longPresses++);
        host.Canvas.Content = root;
        host.AdvanceFrames(3);

        var r = label.DrawingRect;
        var x = r.Left + 95; // inside "wonderful" (about 55 to 145 px at 20 pt)
        var y = r.MidY;
        Send(host, TouchActionResult.Down, Touch(TouchActionType.Pressed, x, y));
        Send(host, TouchActionResult.ContextMenu, Touch(TouchActionType.ContextMenu, x, y)); // Android Chrome, ~0.5 s
        Assert.Equal("wonderful", seen);
        Send(host, TouchActionResult.LongPressing, Touch(TouchActionType.Pressed, x, y)); // the engine's own, later
        Assert.Equal("wonderful", label.SelectedText);
        Assert.True(longPresses <= 1);
    }
}
