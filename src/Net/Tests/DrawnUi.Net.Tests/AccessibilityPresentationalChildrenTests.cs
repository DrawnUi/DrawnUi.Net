using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The texts inside a button, a radio, a switch, a tab or a slider are its look, not separate nodes (ARIA "children
/// presentational"): a screen reader says the control's name once, not the name and then each text again. A link and a
/// group keep their texts. Found in the 3D gallery with plain labels read as text: a fabric radio "Linen, $1,190" was
/// followed by a text "Linen", a hotspot button "Seat, hotspot" by a text "1".
/// </summary>
public class AccessibilityPresentationalChildrenTests
{
    static SkiaShape Control(string role, string name, string inner) => new()
    {
        WidthRequest = 120,
        HeightRequest = 40,
        AccessibilityRole = role,
        AccessibilityLabel = name,
        Children = { new SkiaLabel(inner) { AccessibilityRole = Aria.RoleText, AccessibilityLabel = inner } }
    };

    [Theory]
    [InlineData("radio")]
    [InlineData("button")]
    [InlineData("switch")]
    [InlineData("tab")]
    public void TextsInside_AControlWithPresentationalChildren_AreNotNodes(string role)
    {
        using var host = new HeadlessCanvasHost(400, 300);
        var manager = host.Canvas.AccessibilityManager;
        manager.MinUpdateIntervalMs = 0;
        host.Canvas.Content = new SkiaStack { Children = { Control(role, "Linen, $1,190", "Linen") } };
        host.AdvanceFrames(4);

        Assert.Single(manager.Snapshot, n => n.Label == "Linen, $1,190");
        Assert.DoesNotContain(manager.Snapshot, n => n.Label == "Linen");
    }

    [Fact]
    public void SelectableTextInside_AButton_Stays()
    {
        using var host = new HeadlessCanvasHost(400, 300);
        var manager = host.Canvas.AccessibilityManager;
        manager.MinUpdateIntervalMs = 0;
        var control = Control("button", "Linen, $1,190", "Linen");
        ((SkiaLabel)control.Children[0]).AccessibilityTextSelectable = true;
        host.Canvas.Content = new SkiaStack { Children = { control } };
        host.AdvanceFrames(4);

        Assert.Contains(manager.Snapshot, n => n.Label == "Linen");
    }

    [Theory]
    [InlineData("link")]
    [InlineData("group")]
    public void TextsInside_ALinkOrAGroup_StayNodes(string role)
    {
        using var host = new HeadlessCanvasHost(400, 300);
        var manager = host.Canvas.AccessibilityManager;
        manager.MinUpdateIntervalMs = 0;
        host.Canvas.Content = new SkiaStack { Children = { Control(role, "Upholstery", "Linen") } };
        host.AdvanceFrames(4);

        Assert.Contains(manager.Snapshot, n => n.Label == "Linen");
    }
}
