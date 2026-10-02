using AppoMobi.Gestures;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// LockChildrenGestures applies on both gesture dispatch paths: the render-tree path (layouts, the default)
/// and the live-children fallback (controls without a rendering tree, e.g. SkiaScroll). A locked gesture
/// type never reaches a child, an unlocked one does, and the parent ends up with the same result either way.
/// </summary>
public class LockChildrenGesturesTests
{
    /// <summary>Same layer, forced onto the fallback dispatch path (no rendering tree).</summary>
    private class FallbackLayer : SkiaLayer
    {
        public override bool UsesRenderingTree => false;
    }

    private record Result(int ChildDowns, int ChildTaps, int ChildPans, int ParentTaps);

    private static Result Run(LockTouch lockTouch, bool renderTree)
    {
        using var host = new HeadlessCanvasHost(400, 300);
        var robot = new GestureRobot(host);
        int childDowns = 0, childTaps = 0, childPans = 0, parentTaps = 0;

        var parent = renderTree ? new SkiaLayer() : new FallbackLayer();
        parent.LockChildrenGestures = lockTouch;
        parent.Children.Add(new SkiaShape
        {
            WidthRequest = 200,
            HeightRequest = 200,
            BackgroundColor = Colors.Red,
        }.WithGestures((me, args, apply) =>
        {
            switch (args.Type)
            {
                case TouchActionResult.Down: childDowns++; break;
                case TouchActionResult.Tapped: childTaps++; break;
                case TouchActionResult.Panning: childPans++; break;
            }
            return me;
        }));
        parent.OnTapped(me => parentTaps++);
        host.Canvas.Content = parent;
        host.AdvanceFrames(4);

        robot.Tap(100, 100);
        robot.Pan(100, 150, 100, 50, durationMs: 160, steps: 8);
        host.AdvanceFrames(2);

        return new Result(childDowns, childTaps, childPans, parentTaps);
    }

    [Theory]
    [InlineData(LockTouch.Disabled, true, true)]
    [InlineData(LockTouch.Enabled, false, false)]
    [InlineData(LockTouch.PassNone, false, false)]
    [InlineData(LockTouch.PassTap, true, false)]
    [InlineData(LockTouch.PassTapAndLongPress, true, false)]
    public void RenderTreePath_HonoursLock_LikeFallbackPath(LockTouch lockTouch, bool tapReachesChild, bool panReachesChild)
    {
        var tree = Run(lockTouch, renderTree: true);
        var fallback = Run(lockTouch, renderTree: false);

        Assert.Equal(tapReachesChild ? 1 : 0, tree.ChildTaps);
        Assert.Equal(panReachesChild, tree.ChildPans > 0);
        Assert.Equal(panReachesChild, tree.ChildDowns > 0);

        Assert.Equal(fallback, tree);
    }
}
