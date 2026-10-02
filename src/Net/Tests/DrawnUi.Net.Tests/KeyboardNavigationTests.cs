using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// DrawnView.HandleKeyboardNavigation, the keyboard navigation of heads with no accessibility layer of their own (MAUI Mac
/// Catalyst): Tab / Shift+Tab walk the interactive nodes in reading order and leave past either end, a group of items
/// (Aria.RoleList) is one Tab stop whose items the arrows walk, Enter / Space activate, Escape leaves.
/// </summary>
public class KeyboardNavigationTests
{
    private static (HeadlessCanvasHost host, SkiaButton[] buttons, SkiaButton[] items) Build(Action<string> tapped)
    {
        var host = new HeadlessCanvasHost(300, 400, background: Colors.Black);
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;

        SkiaButton Button(string name) => new SkiaButton(name)
        {
            HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Fill,
            AccessibilityRole = Aria.RoleButton,
            Clicked = (_, _) => tapped(name),
        };

        var buttons = new[] { Button("a"), Button("b") };
        var items = new[] { Button("item 0"), Button("item 1"), Button("item 2") };
        host.Canvas.Content = new SkiaStack
        {
            Spacing = 4,
            Children =
            {
                buttons[0],
                new SkiaStack { AccessibilityRole = Aria.RoleList, Spacing = 0, Children = { items[0], items[1], items[2] } },
                buttons[1],
            }
        };
        host.AdvanceFrames(4);
        return (host, buttons, items);
    }

    private static void Press(HeadlessCanvasHost host, InputKey key, bool shift = false)
    {
        Assert.True(host.Canvas.HandleKeyboardNavigation(key, shift), $"{key} was not used");
        host.AdvanceFrames(2); // a group move lands at the next frame end
    }

    [Fact]
    public void Tab_WalksStops_GroupIsOneStop_ShiftTabBack_LeavesPastTheEnd()
    {
        var (host, buttons, items) = Build(_ => { });
        using var _ = host;
        var canvas = host.Canvas;

        Press(host, InputKey.Tab);
        Assert.Same(buttons[0], canvas.KeyboardFocusNode);
        Press(host, InputKey.Tab);
        Assert.Same(items[0], canvas.KeyboardFocusNode); // the group's first item
        Press(host, InputKey.Tab);
        Assert.Same(buttons[1], canvas.KeyboardFocusNode); // the other items are not stops
        Press(host, InputKey.Tab, shift: true);
        Assert.Same(items[0], canvas.KeyboardFocusNode);

        Press(host, InputKey.Tab);
        Press(host, InputKey.Tab);
        Assert.Null(canvas.KeyboardFocusNode); // past the end: focus leaves, the next Tab starts over
        Press(host, InputKey.Tab);
        Assert.Same(buttons[0], canvas.KeyboardFocusNode);
    }

    [Fact]
    public void Arrows_WalkTheGroup_TabReentersAtTheLastItem()
    {
        var (host, buttons, items) = Build(_ => { });
        using var _ = host;
        var canvas = host.Canvas;

        Press(host, InputKey.Tab);
        Press(host, InputKey.Tab);
        Press(host, InputKey.ArrowDown);
        Assert.Same(items[1], canvas.KeyboardFocusNode);
        Press(host, InputKey.ArrowDown);
        Assert.Same(items[2], canvas.KeyboardFocusNode);

        Press(host, InputKey.Tab);
        Assert.Same(buttons[1], canvas.KeyboardFocusNode);
        Press(host, InputKey.Tab, shift: true);
        Assert.Same(items[2], canvas.KeyboardFocusNode); // the group remembers its current item
    }

    [Fact]
    public void EnterAndSpaceActivate_EscapeLeaves_KeysWithoutFocusAreNotUsed()
    {
        var log = new List<string>();
        var (host, buttons, _) = Build(log.Add);
        using var _ = host;
        var canvas = host.Canvas;

        Assert.False(canvas.HandleKeyboardNavigation(InputKey.Space, false)); // nothing in focus: the app's key

        Press(host, InputKey.Tab);
        Press(host, InputKey.Enter);
        Press(host, InputKey.Space);
        Assert.Equal(new[] { "a", "a" }, log);

        Press(host, InputKey.Escape);
        Assert.Null(canvas.KeyboardFocusNode);
    }
}
