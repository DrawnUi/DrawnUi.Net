using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaAccessibilityManager snapshot rules: nodes under a hidden ancestor are pruned even though their own
/// IsVisible stays true, and Changed is raised only when the snapshot really differs.
/// </summary>
public class AccessibilitySnapshotTests
{
    private static SkiaButton Button(string text) => new()
    {
        Text = text,
        WidthRequest = 120,
        HeightRequest = 40,
        AccessibilityRole = Aria.RoleButton,
        AccessibilityLabel = text,
        AccessibilityCanInteract = true,
    };

    [Fact]
    public void HiddenAncestor_PrunesNodes_And_ChangedFiresOnlyOnRealChange()
    {
        using var host = new HeadlessCanvasHost(400, 400);
        var manager = host.Canvas.AccessibilityManager;
        manager.MinUpdateIntervalMs = 0;

        var hiddenGroup = new SkiaLayout { Type = LayoutType.Column, HorizontalOptions = LayoutOptions.Fill };
        var inner = Button("Inner");
        hiddenGroup.AddSubView(inner);

        var root = new SkiaLayout { Type = LayoutType.Column, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };
        root.AddSubView(Button("First"));
        root.AddSubView(hiddenGroup);
        host.Canvas.Content = root;

        int changed = 0;
        manager.Changed += () => changed++;

        host.AdvanceFrames(3);
        Assert.Equal(2, manager.Snapshot.Length);
        Assert.Equal(1, changed); // two more identical rebuilds stayed silent

        hiddenGroup.IsVisible = false;
        inner.NotifyAccessibility();
        host.AdvanceFrames(2);
        Assert.Single(manager.Snapshot);
        Assert.Equal("First", manager.Snapshot[0].Label);
        Assert.Equal(2, changed);

        hiddenGroup.IsVisible = true;
        inner.NotifyAccessibility();
        host.AdvanceFrames(2);
        Assert.Equal(2, manager.Snapshot.Length);
        Assert.Equal(3, changed);
    }
    [Fact]
    public void Defaults_FeedTheNode_And_PresentationHidesInnerLabel()
    {
        using var host = new HeadlessCanvasHost(400, 400);
        var manager = host.Canvas.AccessibilityManager;
        manager.MinUpdateIntervalMs = 0;

        var button = new SkiaButton { Text = "Save", WidthRequest = 120, HeightRequest = 40, AccessibilityRole = Aria.RoleButton };
        var toggle = new SkiaSwitch { WidthRequest = 60, HeightRequest = 30 }; // default role switch, no opt-in
        var slider = new SkiaSlider { WidthRequest = 200, HeightRequest = 30, Min = 0, Max = 100, End = 25 };
        var plain = new SkiaLabel { Text = "not exposed", WidthRequest = 120, HeightRequest = 20 };

        var root = new SkiaLayout { Type = LayoutType.Column, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };
        root.AddSubView(button);
        root.AddSubView(toggle);
        root.AddSubView(slider);
        root.AddSubView(plain);
        host.Canvas.Content = root;
        host.AdvanceFrames(3);

        var snap = manager.Snapshot;
        Assert.Equal(3, snap.Length); // button, switch, slider; plain label and the button's inner label stay out
        Assert.DoesNotContain(snap, n => n.Label == "not exposed");

        var b = Assert.Single(snap, n => n.Role == Aria.RoleButton);
        Assert.Equal("Save", b.Label);
        Assert.True(b.CanInteract);
        Assert.Equal(button.AccessibilityId, b.Id);

        var t = Assert.Single(snap, n => n.Role == Aria.RoleSwitch);
        Assert.False(t.IsPressed);
        toggle.IsToggled = true;
        Assert.True(toggle.IsToggled, "IsToggled did not stick");
        Assert.True(toggle.AccessibilityIsPressed, "AccessibilityIsPressed does not follow IsToggled");
        host.AdvanceFrames(2);
        Assert.True(Assert.Single(manager.Snapshot, n => n.Role == Aria.RoleSwitch).IsPressed, "snapshot not rebuilt after toggle");

        var sl = Assert.Single(manager.Snapshot, n => n.Role == Aria.RoleSlider);
        Assert.Equal("25", sl.Label);

        button.IsDisabled = true;
        button.AccessibilityLabel = "Custom";
        host.AdvanceFrames(2);
        var b2 = Assert.Single(manager.Snapshot, n => n.Role == Aria.RoleButton);
        Assert.False(b2.CanInteract);
        Assert.Equal("Custom", b2.Label);
    }
}
