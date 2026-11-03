using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// DrawnView.FrameRequested: a host that draws on demand learns that the canvas needs a frame, once per frame, so it
/// never has to check IsDirty on a timer.
/// </summary>
public class FrameRequestedTests
{
    [Fact]
    public void RaisedOncePerFrame_WhenSomethingChanges()
    {
        using var host = new HeadlessCanvasHost(200, 200);
        SkiaShape box = null;
        host.Canvas.Content = new SkiaShape { WidthRequest = 50, HeightRequest = 50, BackgroundColor = Colors.Red }.Assign(out box);
        host.AdvanceFrames(3);

        var requests = 0;
        host.Canvas.FrameRequested += (_, _) => requests++;

        box.BackgroundColor = Colors.Blue;
        box.BackgroundColor = Colors.Green;
        host.Canvas.Update();
        Assert.Equal(1, requests); // several changes before the frame: one request

        host.RenderFrame(16);
        box.BackgroundColor = Colors.Yellow;
        Assert.Equal(2, requests); // after a frame, the next change asks again

        for (var i = 0; i < 10 && host.NeedsFrame; i++)
            host.RenderFrame(16);
        var settled = requests;
        host.AdvanceFrames(3);
        Assert.Equal(settled, requests); // nothing changes: no requests
    }
}
