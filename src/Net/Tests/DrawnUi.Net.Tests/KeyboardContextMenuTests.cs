using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The keyboard's context-menu request (Menu key, Shift+F10) on the desktop canvas (WPF, OpenTK) goes to the control in
/// keyboard focus, at its center, as on MAUI Windows; with nothing drawn in focus it goes to the given point.
/// </summary>
public class KeyboardContextMenuTests
{
    const int W = 400, H = 300;

    static HeadlessCanvasHost Scene(List<string> got)
    {
        var host = new HeadlessCanvasHost(W, H);
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;
        SkiaShape Row(string name) => new SkiaShape
        {
            WidthRequest = 200, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start, BackgroundColor = Colors.Gray,
            AccessibilityRole = Aria.RoleButton, AccessibilityCanInteract = true, AccessibilityLabel = name,
        }.OnContextMenu((me, e) =>
        {
            got.Add($"{name} {e.Source} {e.Local.X:0},{e.Local.Y:0}");
            return true;
        });
        host.Canvas.Content = new SkiaStack { Spacing = 60, Children = { Row("First"), Row("Second") } };
        host.AdvanceFrames(4);
        return host;
    }

    [Fact]
    public void Menu_key_opens_the_menu_of_the_focused_control_at_its_center()
    {
        var got = new List<string>();
        using var host = Scene(got);

        host.Canvas.HandleKeyboardNavigation(InputKey.Tab, false);
        host.Canvas.HandleKeyboardNavigation(InputKey.Tab, false); // the keyboard is on "Second"
        host.AdvanceFrames(2);

        // the pointer last rested on "First": the keyboard's request still goes to the focused control
        Assert.True(host.Canvas.HandleDesktopContextMenu(50, 20, W, H, null));
        Assert.Equal(new[] { "Second Keyboard 100,20" }, got);
    }

    [Fact]
    public void Without_keyboard_focus_the_request_goes_to_the_given_point()
    {
        var got = new List<string>();
        using var host = Scene(got);

        Assert.True(host.Canvas.HandleDesktopContextMenu(50, 20, W, H, null));
        Assert.Equal(new[] { "First Keyboard 50,20" }, got);
    }

    [Fact]
    public void A_right_click_still_goes_where_the_mouse_is()
    {
        var got = new List<string>();
        using var host = Scene(got);
        host.Canvas.HandleKeyboardNavigation(InputKey.Tab, false);
        host.Canvas.HandleKeyboardNavigation(InputKey.Tab, false);
        host.AdvanceFrames(2);

        Assert.True(host.Canvas.HandleDesktopContextMenu(50, 20, W, H, AppoMobi.Gestures.PointerDeviceType.Mouse));
        Assert.Equal(new[] { "First Mouse 50,20" }, got);
    }
}
