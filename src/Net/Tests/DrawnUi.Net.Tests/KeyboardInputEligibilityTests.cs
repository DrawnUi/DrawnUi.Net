using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Keyboard navigation uses the pointer's input eligibility: a control the pointer cannot use (InputTransparent on it
/// or an ancestor, disabled, gestures locked by an ancestor) is not a Tab stop, is not activated and takes no keys.
/// Opacity alone does not count, exactly like for the pointer.
/// </summary>
public class KeyboardInputEligibilityTests
{
    [Fact]
    public void InputTransparent_OnNodeOrAncestor_BlocksTabActivationAndKeys_AndFlipsBackAtRuntime()
    {
        using var host = new HeadlessCanvasHost(400, 300);
        var manager = host.Canvas.AccessibilityManager;
        manager.MinUpdateIntervalMs = 0;

        SkiaSwitch remove = null;
        SkiaSlider locked = null;
        SkiaLayout group = null;
        host.Canvas.Content = new SkiaStack
        {
            Children =
            {
                // the "Default" card's hidden remove button: invisible and not hit-testable
                new SkiaSwitch { WidthRequest = 60, HeightRequest = 30, Opacity = 0, InputTransparent = true }.Assign(out remove),
                // a premium-locked group: dimmed and not hit-testable, the slider inside is a node
                new SkiaLayout
                {
                    Type = LayoutType.Column,
                    Opacity = 0.35,
                    InputTransparent = true,
                    HorizontalOptions = LayoutOptions.Fill,
                    Children = { new SkiaSlider { WidthRequest = 300, HeightRequest = 40, Min = 0, Max = 10, Step = 1, End = 5 }.Assign(out locked) }
                }.Assign(out group),
            }
        };
        host.AdvanceFrames(4);

        Assert.False(remove.AccessibilityCanInteract);
        Assert.False(SkiaAccessibilityManager.Activate(remove));
        Assert.False(remove.IsToggled);

        Assert.False(locked.AccessibilityCanInteract);
        Assert.False(SkiaAccessibilityManager.Key(locked, InputKey.ArrowRight));
        Assert.Equal(5, locked.End);
        Assert.All(manager.Snapshot, n => Assert.False(n.CanInteract));

        // premium starts: the group takes input again, and the snapshot follows
        group.InputTransparent = false;
        host.AdvanceFrames(3);
        Assert.True(locked.AccessibilityCanInteract);
        Assert.True(SkiaAccessibilityManager.Key(locked, InputKey.ArrowRight));
        Assert.Equal(6, locked.End);
        Assert.True(Assert.Single(manager.Snapshot, n => n.Role == Aria.RoleSlider).CanInteract);
    }

    [Fact]
    public void OpacityZeroAlone_StillTakesInput_LikeThePointer()
    {
        using var host = new HeadlessCanvasHost(300, 200);
        var taps = 0;
        var button = new SkiaButton("x") { WidthRequest = 100, HeightRequest = 40, Opacity = 0, AccessibilityRole = Aria.RoleButton }
            .OnTapped(me => taps++);
        host.Canvas.Content = new SkiaLayer { Children = { button } };
        host.AdvanceFrames(4);

        // the pointer reaches an invisible control: the gesture dispatch does not look at opacity
        host.Canvas.HandleDesktopPointerDown(50, 20, 300, 200);
        host.Canvas.HandleDesktopPointerUp(50, 20, 300, 200);
        host.AdvanceFrames(2);
        Assert.Equal(1, taps);

        // so keyboard navigation does too
        Assert.True(button.AccessibilityCanInteract);
    }

    [Fact]
    public void DisabledButton_And_LockedChildren_AreNotUsable()
    {
        using var host = new HeadlessCanvasHost(400, 300);
        SkiaButton disabled = null;
        SkiaSlider underPassTap = null;
        SkiaSwitch underLock = null;
        host.Canvas.Content = new SkiaStack
        {
            Children =
            {
                new SkiaButton("off") { WidthRequest = 100, HeightRequest = 40, IsDisabled = true, AccessibilityRole = Aria.RoleButton, AccessibilityCanInteract = true }.Assign(out disabled),
                new SkiaLayout
                {
                    LockChildrenGestures = LockTouch.PassTap,
                    Children = { new SkiaSlider { WidthRequest = 300, HeightRequest = 40, Min = 0, Max = 10, Step = 1, End = 5 }.Assign(out underPassTap) }
                },
                new SkiaLayout
                {
                    LockChildrenGestures = LockTouch.Enabled,
                    Children = { new SkiaSwitch { WidthRequest = 60, HeightRequest = 30 }.Assign(out underLock) }
                },
            }
        };
        host.AdvanceFrames(4);

        Assert.False(disabled.AccessibilityCanInteract); // even with an explicit true

        // taps pass the PassTap layer, pans do not: Tab and Enter reach the slider, arrows do not move it
        Assert.True(underPassTap.AccessibilityCanInteract);
        Assert.False(SkiaAccessibilityManager.Key(underPassTap, InputKey.ArrowRight));
        Assert.Equal(5, underPassTap.End);

        Assert.False(underLock.AccessibilityCanInteract);
    }
}
