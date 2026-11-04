using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A live region's new text reaches the accessibility snapshot (and with it every screen reader and the web overlays)
/// on the next frame; other changes keep the snapshot's rate limit.
/// </summary>
public class AccessibilityLiveRegionTests
{
    [Fact]
    public void A_live_region_reaches_the_snapshot_on_the_next_frame()
    {
        using var host = new HeadlessCanvasHost(400, 400);
        var manager = host.Canvas.AccessibilityManager;
        manager.MinUpdateIntervalMs = 60_000;

        var plain = new SkiaLabel { Text = "Plain 1", AccessibilityRole = Aria.RoleText };
        var live = new SkiaLabel { Text = "Waiting", AccessibilityRole = Aria.RoleText, AccessibilityLive = "polite" };
        host.Canvas.Content = new SkiaLayout
        {
            Type = LayoutType.Column,
            HorizontalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl> { plain, live },
        };
        host.AdvanceFrames(3);
        Assert.Contains(manager.Snapshot, n => n.Label == "Waiting");

        plain.Text = "Plain 2";
        host.RenderFrame(16);
        Assert.DoesNotContain(manager.Snapshot, n => n.Label == "Plain 2"); // waits for the rate limit

        live.Text = "Saved";
        host.RenderFrame(16);
        Assert.Contains(manager.Snapshot, n => n.Label == "Saved");
    }
}
