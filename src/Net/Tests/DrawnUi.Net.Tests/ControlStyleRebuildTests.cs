using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A ControlStyle change at runtime rebuilds the default content. Removing the old children one by one runs FindViews
/// (OnChildrenChanged), which picked up the old parts still attached; the new content then never got the button's text
/// ("Test" for Cupertino / Windows, nothing for Material / Material3) or the progress' value.
/// </summary>
public class ControlStyleRebuildTests
{
    [Theory]
    [InlineData(PrebuiltControlStyle.Cupertino)]
    [InlineData(PrebuiltControlStyle.Material)]
    [InlineData(PrebuiltControlStyle.Material3)]
    [InlineData(PrebuiltControlStyle.Windows)]
    [InlineData(PrebuiltControlStyle.Unset)]
    public void Button_KeepsItsCaption_AfterAStyleChange(PrebuiltControlStyle style)
    {
        using var host = new HeadlessCanvasHost(300, 120, background: Colors.Black);
        var button = new SkiaButton("Hello");
        host.Canvas.Content = button;
        host.AdvanceFrames(3);

        button.ControlStyle = style == PrebuiltControlStyle.Windows ? PrebuiltControlStyle.Cupertino : PrebuiltControlStyle.Windows;
        host.AdvanceFrames(3);
        button.ControlStyle = style;
        host.AdvanceFrames(3);

        var label = button.MainLabel;
        Assert.NotNull(label);
        Assert.False(label.IsDisposed, "MainLabel points at the old, disposed label");
        Assert.True(IsInside(label, button), "MainLabel is not part of the button's content");
        Assert.Equal("Hello", label.Text);
        Assert.True(button.MainFrame is { IsDisposed: false } && IsInside(button.MainFrame, button));
    }

    [Fact]
    public void Progress_KeepsItsParts_AfterAStyleChange()
    {
        using var host = new HeadlessCanvasHost(300, 120, background: Colors.Black);
        var progress = new ProbeProgress { Value = 40, WidthRequest = 200 };
        host.Canvas.Content = progress;
        host.AdvanceFrames(3);

        progress.ControlStyle = PrebuiltControlStyle.Material;
        host.AdvanceFrames(3);

        Assert.True(progress.TrackPart is { IsDisposed: false } && IsInside(progress.TrackPart, progress), "Track points at the old part");
        Assert.True(progress.TrailPart is { IsDisposed: false } && IsInside(progress.TrailPart, progress), "ProgressTrail points at the old part");
    }

    private class ProbeProgress : SkiaProgress
    {
        public SkiaControl TrackPart => Track;
        public SkiaControl TrailPart => ProgressTrail;
    }

    private static bool IsInside(SkiaControl control, SkiaControl ancestor)
    {
        for (var parent = control.Parent as SkiaControl; parent != null; parent = parent.Parent as SkiaControl)
        {
            if (ReferenceEquals(parent, ancestor))
                return true;
        }
        return false;
    }
}
