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
}
