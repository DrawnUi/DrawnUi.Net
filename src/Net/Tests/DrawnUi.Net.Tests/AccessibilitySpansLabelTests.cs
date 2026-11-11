using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A label made of spans is read by screen readers as its spans' text, joined (a link drawn with an underlined span):
/// the same rule as DrawnUI for Rust and DrawnUi.React. An explicit AccessibilityLabel still wins.
/// </summary>
public class AccessibilitySpansLabelTests
{
    [Fact]
    public void LabelOfSpans_IsReadAsItsSpansText()
    {
        using var host = new HeadlessCanvasHost(400, 300);
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;
        host.Canvas.Content = new SkiaStack
        {
            Children =
            {
                new SkiaLabel
                {
                    AccessibilityRole = Aria.RoleLink,
                    Spans = { new TextSpan { Text = "MADE WITH " }, new TextSpan { Text = "DRAWNUI", Underline = true } },
                },
                new SkiaLabel
                {
                    AccessibilityRole = Aria.RoleText,
                    AccessibilityLabel = "Spoken instead",
                    Spans = { new TextSpan { Text = "Shown" } },
                },
            },
        };
        host.AdvanceFrames(4);

        var labels = host.Canvas.AccessibilityManager.Snapshot.Select(n => n.Label).ToArray();
        Assert.Contains("MADE WITH DRAWNUI", labels);
        Assert.Contains("Spoken instead", labels);
        Assert.DoesNotContain("Shown", labels);
    }
}
