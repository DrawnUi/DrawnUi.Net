using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// KeyboardWrap on an arrow-key group makes the arrows go round; DrawnView.SetAccessibilityFocus moves the keyboard and
/// screen-reader focus from app code at the frame end, leaving the focus ring as it was. Same as DrawnUi.Rust
/// keyboard_wrap and Cx::set_accessibility_focus.
/// </summary>
public class KeyboardWrapAndFocusTests
{
    private static (HeadlessCanvasHost host, SkiaStack menu, List<SkiaButton> items) Menu(bool wrap)
    {
        var host = new HeadlessCanvasHost(300, 300);
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;
        var items = new List<SkiaButton>();
        var menu = new SkiaStack
        {
            AccessibilityRole = Aria.RoleMenu,
            Children = Enumerable.Range(0, 3).Select(i => (SkiaControl)new SkiaButton($"Item {i}")
            {
                AccessibilityRole = Aria.RoleMenuItem,
                AccessibilityLabel = $"Item {i}",
                HeightRequest = 40,
            }.Assign(out var b).Adapt(me => items.Add(me))).ToList(),
        }.WithKeyboardWrap(wrap);
        host.Canvas.Content = menu;
        host.AdvanceFrames(4);
        return (host, menu, items);
    }

    private static void Press(HeadlessCanvasHost host, InputKey key)
    {
        Assert.True(host.Canvas.HandleKeyboardNavigation(key, false), $"{key} was not used");
        host.AdvanceFrames(2);
    }

    [Fact]
    public void KeyboardWrap_makes_the_arrows_go_round()
    {
        var (host, _, items) = Menu(wrap: true);
        using var _ = host;
        Press(host, InputKey.Tab);
        Assert.Same(items[0], host.Canvas.KeyboardFocusNode);

        Press(host, InputKey.ArrowUp);
        Assert.Same(items[2], host.Canvas.KeyboardFocusNode); // first to last
        Press(host, InputKey.ArrowDown);
        Assert.Same(items[0], host.Canvas.KeyboardFocusNode); // last to first
    }

    [Fact]
    public void Without_KeyboardWrap_the_arrows_stop_at_the_ends()
    {
        var (host, _, items) = Menu(wrap: false);
        using var _ = host;
        Press(host, InputKey.Tab);
        Press(host, InputKey.ArrowUp);
        Assert.Same(items[0], host.Canvas.KeyboardFocusNode);
    }

    [Fact]
    public void SetAccessibilityFocus_moves_the_reader_focus_and_keeps_the_ring_hidden()
    {
        var (host, _, items) = Menu(wrap: false);
        using var _ = host;
        Assert.Null(host.Canvas.KeyboardFocusNode); // no keyboard use yet: no ring

        host.Canvas.SetAccessibilityFocus(items[2]);
        host.AdvanceFrames(2);

        Assert.Same(items[2], host.Canvas.AccessibilityManager.FocusedNode);
        Assert.Null(host.Canvas.KeyboardFocusNode);
    }

    [Fact]
    public void SetAccessibilityFocus_with_the_keyboard_in_use_moves_the_ring_too()
    {
        var (host, _, items) = Menu(wrap: false);
        using var _ = host;
        Press(host, InputKey.Tab); // the keyboard is in use

        host.Canvas.SetAccessibilityFocus(items[1]);
        host.AdvanceFrames(2);

        Assert.Same(items[1], host.Canvas.AccessibilityManager.FocusedNode);
        Assert.Same(items[1], host.Canvas.KeyboardFocusNode);
    }
}
