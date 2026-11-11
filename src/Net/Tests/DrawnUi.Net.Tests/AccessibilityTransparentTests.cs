using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A control at Opacity 0 is not drawn, so screen readers do not hear it (as with IsVisible false, as an ancestor at
/// Opacity 0 already did): the same rule as DrawnUI for Rust and DrawnUi.React. It comes back when it shows again.
/// </summary>
public class AccessibilityTransparentTests
{
    [Fact]
    public void TransparentControl_IsNotInTheTree_UntilItShows()
    {
        using var host = new HeadlessCanvasHost(400, 300);
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;
        SkiaLabel hidden = null;
        host.Canvas.Content = new SkiaStack
        {
            Children =
            {
                new SkiaLabel("Shown") { AccessibilityRole = Aria.RoleText },
                new SkiaLabel("x2") { AccessibilityRole = Aria.RoleText, Opacity = 0 }.Assign(out hidden),
                new SkiaStack
                {
                    Opacity = 0, AccessibilityRole = Aria.RoleStatus, AccessibilityLabel = "Health 100 percent",
                    Children = { new SkiaShape { WidthRequest = 20, HeightRequest = 10 } },
                },
            },
        };
        host.AdvanceFrames(4);

        var labels = host.Canvas.AccessibilityManager.Snapshot.Select(n => n.Label).ToArray();
        Assert.Contains("Shown", labels);
        Assert.DoesNotContain("x2", labels);
        Assert.DoesNotContain("Health 100 percent", labels);

        hidden.Opacity = 1;
        host.AdvanceFrames(4);
        Assert.Contains("x2", host.Canvas.AccessibilityManager.Snapshot.Select(n => n.Label));
    }
}
