using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Screen-reader rules (drawnui-cross 6c, range values and timing): a range control carries its value as a value, never
/// as its name; Increment / Decrement step it, SetValue snaps to the step; the snapshot follows on the next frame after
/// such an action; a reader whose node left is moved to the first node that says something; Up in a 2D group moves by
/// the first row's length.
/// </summary>
public class AccessibilityActionsTests
{
    [Fact]
    public void Slider_ValueNotName_AdjustAndSetValue_ReadBackOnTheNextFrame()
    {
        using var host = new HeadlessCanvasHost(400, 400);
        var manager = host.Canvas.AccessibilityManager;
        manager.MinUpdateIntervalMs = 60_000; // only the action's refresh can rebuild after the first snapshot

        var slider = new SkiaSlider { WidthRequest = 200, HeightRequest = 30, Min = 0, Max = 100, Step = 5, End = 25, AccessibilityLabel = "Volume" };
        var button = new SkiaButton { Text = "Save", WidthRequest = 120, HeightRequest = 40, AccessibilityRole = Aria.RoleButton };
        host.Canvas.Content = new SkiaStack { Children = { slider, button } };
        host.AdvanceFrames(3);

        var node = Assert.Single(manager.Snapshot, n => n.Role == Aria.RoleSlider);
        Assert.Equal("Volume", node.Label);
        Assert.Equal(new AccessibilityValue(25, 0, 100, 5), node.Value);

        Assert.True(SkiaAccessibilityManager.Adjust(slider, increment: true));
        Assert.Equal(30, slider.End);
        host.AdvanceFrames(1);
        Assert.Equal(30, Assert.Single(manager.Snapshot, n => n.Role == Aria.RoleSlider).Value!.Value.Now);

        Assert.True(SkiaAccessibilityManager.SetValue(slider, 47)); // snapped to the step
        Assert.Equal(45, slider.End);
        host.AdvanceFrames(1);
        Assert.Equal(45, Assert.Single(manager.Snapshot, n => n.Role == Aria.RoleSlider).Value!.Value.Now);

        Assert.False(SkiaAccessibilityManager.Adjust(button, increment: true)); // no value, nothing to step
    }

    [Fact]
    public void Progress_ReadOnlyPercentValue_NoName()
    {
        using var host = new HeadlessCanvasHost(400, 400);
        var manager = host.Canvas.AccessibilityManager;
        manager.MinUpdateIntervalMs = 0;

        var progress = new SkiaProgress { WidthRequest = 200, HeightRequest = 10, Min = 0, Max = 200, Value = 130 };
        host.Canvas.Content = new SkiaStack { Children = { progress } };
        host.AdvanceFrames(3);

        var node = Assert.Single(manager.Snapshot, n => n.Role == Aria.RoleProgressBar);
        Assert.Null(node.Label);
        Assert.Equal(new AccessibilityValue(130, 0, 200, 0, "65%"), node.Value);
        Assert.False(SkiaAccessibilityManager.SetValue(progress, 20));
    }

    [Fact]
    public void ReaderOnARemovedNode_IsMovedToTheFirstNodeThatSaysSomething()
    {
        using var host = new HeadlessCanvasHost(400, 400);
        var manager = host.Canvas.AccessibilityManager;
        manager.MinUpdateIntervalMs = 0;

        var first = new SkiaButton { Text = "First", WidthRequest = 120, HeightRequest = 40, AccessibilityRole = Aria.RoleButton };
        var back = new SkiaButton { Text = "Back", WidthRequest = 120, HeightRequest = 40, AccessibilityRole = Aria.RoleButton };
        var page = new SkiaStack { Children = { back } };
        host.Canvas.Content = new SkiaStack { Children = { first, page } };
        host.AdvanceFrames(3);

        AccessibilityNode? moved = null;
        manager.ReaderRefocusRequested += n => moved = n;
        manager.NotifyReaderFocused(back);

        page.IsVisible = false; // the page with the reader's node closes
        back.NotifyAccessibility();
        host.AdvanceFrames(2);

        Assert.NotNull(moved);
        Assert.Equal(first.AccessibilityId, moved!.Id);
        Assert.Same(first, manager.ReaderNode);
    }

    [Fact]
    public void EveryNameIsSaidOnce_ControlKeepsIt_CardLetsItsTitleSayIt_SelectableTextStays()
    {
        using var host = new HeadlessCanvasHost(400, 600);
        var manager = host.Canvas.AccessibilityManager;
        manager.MinUpdateIntervalMs = 0;

        SkiaLabel Text(string text, bool selectable = false) => new()
        {
            Text = text, AccessibilityRole = Aria.RoleText, AccessibilityTextSelectable = selectable, HeightRequest = 30,
        };

        host.Canvas.Content = new SkiaStack
        {
            Children =
            {
                // a control and its caption: the control says the name, the caption is left out
                new SkiaStack { AccessibilityRole = Aria.RoleButton, AccessibilityLabel = "Open", AccessibilityCanInteract = true, Children = { Text("Open") } },
                // a card that takes no input and its title: the title says the name, the card is not named again
                new SkiaStack { AccessibilityRole = Aria.RoleGroup, AccessibilityLabel = "Images", Children = { Text("Images") } },
                // selectable text always stays itself
                new SkiaStack { AccessibilityRole = Aria.RoleButton, AccessibilityLabel = "Copy me", AccessibilityCanInteract = true, Children = { Text("Copy me", selectable: true) } },
            }
        };
        host.AdvanceFrames(3);

        var snap = manager.Snapshot;
        Assert.Equal("Open", Assert.Single(snap, n => n.Role == Aria.RoleButton && n.Label == "Open").Label);
        Assert.DoesNotContain(snap, n => n.Role == Aria.RoleText && n.Label == "Open");

        var card = Assert.Single(snap, n => n.Role == Aria.RoleGroup);
        Assert.Null(card.Label);
        Assert.True(card.NamedByChild);
        Assert.Single(snap, n => n.Role == Aria.RoleText && n.Label == "Images");

        Assert.Single(snap, n => n.Role == Aria.RoleText && n.Label == "Copy me");
    }

    [Fact]
    public void Paging_MovesTheScrollAboveTheNodeByTheViewportLessATenth_NoPageWhereItCannotMove()
    {
        using var host = new HeadlessCanvasHost(300, 400);
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;

        var first = new SkiaButton("first") { HeightRequest = 40, AccessibilityRole = Aria.RoleButton };
        var scroll = new SkiaScroll
        {
            Orientation = ScrollOrientation.Vertical,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Children = Enumerable.Range(0, 40).Select(i => (SkiaControl)new SkiaLayout { HeightRequest = 50, HorizontalOptions = LayoutOptions.Fill })
                    .Prepend(first).ToList(),
            },
        };
        host.Canvas.Content = scroll;
        host.AdvanceFrames(4);

        Assert.False(SkiaAccessibilityManager.Page(first, vertical: true, forward: false, probe: true)); // already at the top
        Assert.False(SkiaAccessibilityManager.Page(first, vertical: false, forward: true, probe: true)); // no horizontal travel
        Assert.True(SkiaAccessibilityManager.Page(first, vertical: true, forward: true));
        host.AdvanceFrames(30); // the animated move ends

        Assert.Equal(-400 * 0.9f, scroll.ViewportOffsetY, 1);
        Assert.True(SkiaAccessibilityManager.Page(first, vertical: true, forward: false, probe: true));
    }

    [Fact]
    public void UpFromAShortLastRow_MovesByTheFirstRowsLength()
    {
        using var host = new HeadlessCanvasHost(300, 400);
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;

        var tiles = Enumerable.Range(1, 12).Select(i => new SkiaButton($"{i}")
        {
            WidthRequest = 28, HeightRequest = 28, AccessibilityRole = Aria.RoleButton, AccessibilityLabel = $"{i}",
        }).ToArray();
        host.Canvas.Content = new SkiaWrap // 10 tiles of 28 per 300 px row: rows of 10 and 2
        {
            AccessibilityRole = Aria.RoleGrid,
            Spacing = 0,
            HorizontalOptions = LayoutOptions.Fill,
            Children = tiles.Cast<SkiaControl>().ToList(),
        };
        host.AdvanceFrames(4);

        void Press(InputKey key)
        {
            Assert.True(host.Canvas.HandleKeyboardNavigation(key, false), $"{key} was not used");
            host.AdvanceFrames(2);
        }

        Press(InputKey.Tab);
        Press(InputKey.End);
        Assert.Same(tiles[11], host.Canvas.KeyboardFocusNode);
        Press(InputKey.ArrowUp);
        Assert.Same(tiles[1], host.Canvas.KeyboardFocusNode); // 12 - 10, not 12 - 2
    }
}
