using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The desktop heads' keyboard navigation (Mac Catalyst, OpenTK: DrawnView.HandleKeyboardNavigation): Tab and Shift+Tab
/// move from the editor that has the caret, also when a click focused it, to the next and previous control; the editor
/// left gives up its caret, the one reached takes it.
/// </summary>
[Collection("Tooltips")] // keys go through the static KeyboardManager
public class EditorTabNavigationTests
{
    static (HeadlessCanvasHost Host, SkiaEditor Name, SkiaEditor Email, SkiaEditor Password) Form()
    {
        var host = new HeadlessCanvasHost(400, 400);
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;
        SkiaEditor name, email, password;
        host.Canvas.Content = new SkiaStack
        {
            Spacing = 8,
            Children =
            {
                new SkiaEditor { WidthRequest = 300, HeightRequest = 40, MaxLines = 1, AccessibilityLabel = "Name" }.Assign(out name),
                new SkiaEditor { WidthRequest = 300, HeightRequest = 40, MaxLines = 1, AccessibilityLabel = "Email" }.Assign(out email),
                new SkiaEditor { WidthRequest = 300, HeightRequest = 40, MaxLines = 1, AccessibilityLabel = "Password" }.Assign(out password),
            }
        };
        host.AdvanceFrames(4);
        return (host, name, email, password);
    }

    static void Settle(HeadlessCanvasHost host)
    {
        // the editors take and give up focus on a short delay
        for (var i = 0; i < 20; i++)
        {
            Thread.Sleep(15);
            host.AdvanceFrames(1);
        }
    }

    [Fact]
    public void Tab_FromAClickedEditor_GoesToTheNext_AndBack()
    {
        var (host, name, email, password) = Form();
        using var _ = host;
        var robot = new GestureRobot(host);
        var r = name.DrawingRect;
        robot.Tap(r.MidX, r.MidY);
        Settle(host);
        Assert.True(name.IsFocused, "the click focused Name");

        Assert.True(host.Canvas.HandleKeyboardNavigation(InputKey.Tab, false));
        Settle(host);
        Assert.False(name.IsFocused, "Name keeps its caret");
        Assert.True(email.IsFocused, "Tab did not reach Email");
        Assert.False(password.IsFocused);

        Assert.True(host.Canvas.HandleKeyboardNavigation(InputKey.Tab, true));
        Settle(host);
        Assert.True(name.IsFocused, "Shift+Tab did not go back to Name");
        Assert.False(email.IsFocused);
    }
}
