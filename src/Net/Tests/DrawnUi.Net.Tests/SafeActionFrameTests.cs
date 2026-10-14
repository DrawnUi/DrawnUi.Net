using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SafeAction queues its action on the canvas, to run before the next frame. It asked for that frame through the
/// control's own Repaint, which climbs the parents and stops at one that is hidden, as the root under a shell modal
/// that froze it. No frame came, so the action waited until something else redrew the canvas (FiltersCamera: a GPU
/// photo render from a modal over the camera page hung until the user touched the screen).
/// The host here draws only when a frame was asked for, as an app does.
/// </summary>
public class SafeActionFrameTests
{
    [Fact]
    public void ControlUnderHiddenParent_SafeActionRunsOnTheNextFrame()
    {
        using var host = new HeadlessCanvasHost(200, 200);
        var control = new SkiaShape { WidthRequest = 50, HeightRequest = 50 };
        var frozen = new SkiaLayer { Children = { control } };
        host.Canvas.Content = new SkiaLayer { Children = { frozen } };
        host.AdvanceFrames(3);

        frozen.IsVisible = false;
        for (var i = 0; i < 10 && host.NeedsFrame; i++)
            host.RenderFrame();

        var ran = false;
        control.SafeAction(() => ran = true);
        for (var i = 0; i < 10 && host.NeedsFrame; i++)
            host.RenderFrame();

        Assert.True(ran);
    }
}
