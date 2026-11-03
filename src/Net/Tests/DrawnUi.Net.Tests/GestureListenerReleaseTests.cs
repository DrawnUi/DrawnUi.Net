using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A disposed control is not kept alive by the canvas' gesture dispatch: the control that took the last input (the wheel,
/// a hover, a screen reader's activation) leaves HadInput when its tree is disposed, also with no pointer press after it.
/// </summary>
public class GestureListenerReleaseTests
{
    [Fact]
    public void DisposedTree_LeavesHadInput_WithoutAnotherPress()
    {
        using var host = new HeadlessCanvasHost(400, 400);
        var page = new SkiaScroll
        {
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack { Children = { new SkiaShape { HeightRequest = 2000, BackgroundColor = Colors.Gray } } },
        };
        host.Canvas.Content = page;
        host.AdvanceFrames(3);

        // the wheel: no press or release follows, so nothing ends the gesture
        new GestureRobot(host).WheelScroll(200, 200, -120);
        host.AdvanceFrames(3);
        Assert.NotEmpty(host.Canvas.HadInput);

        // the app rebuilds its screen (a language switch): the old tree is disposed
        host.Canvas.Content = new SkiaLayer();
        page.Dispose();
        host.AdvanceFrames(3);

        Assert.Empty(host.Canvas.HadInput);
    }
}
