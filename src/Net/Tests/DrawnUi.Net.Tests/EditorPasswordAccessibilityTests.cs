using DrawnUi.Draw;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>A password field never gives its typed text to screen readers or UI Automation: its name is the placeholder.</summary>
public class EditorPasswordAccessibilityTests
{
    [Fact]
    public void Password_text_is_never_the_accessibility_name()
    {
        var editor = new SkiaEditor { IsPassword = true, PlaceholderText = "Password", Text = "secret123" };
        Assert.Equal("Password", ((ISkiaAccessibilityNode)editor).AccessibilityLabel);
    }

    [Fact]
    public void A_plain_field_is_named_by_its_text()
    {
        var editor = new SkiaEditor { PlaceholderText = "Name", Text = "Nick" };
        Assert.Equal("Nick", ((ISkiaAccessibilityNode)editor).AccessibilityLabel);
    }
}
