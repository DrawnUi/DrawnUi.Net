using AppoMobi.Gestures;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// LockChildrenGestures applies on both gesture dispatch paths: the render-tree path (layouts, the default)
/// and the live-children fallback (controls without a rendering tree, e.g. SkiaScroll). A locked gesture
/// type never reaches a child, an unlocked one does, and the locked control itself still gets its Tapped:
/// "lock the children, handle the tap on the card" works. Enabled also consumes what it locks, so a control stacked
/// below gets nothing; PassNone only locks the children. Both paths give the same result.
/// </summary>
public class LockChildrenGesturesTests
{
    /// <summary>Same layer, forced onto the fallback dispatch path (no rendering tree).</summary>
    private class FallbackLayer : SkiaLayer
    {
        public override bool UsesRenderingTree => false;
    }

    private record Result(int ChildDowns, int ChildTaps, int ChildPans, int ParentTapsOnChild, int ParentTapsOnEmpty);

    private static Result Run(LockTouch lockTouch, bool renderTree)
    {
        using var host = new HeadlessCanvasHost(400, 300);
        var robot = new GestureRobot(host);
        int childDowns = 0, childTaps = 0, childPans = 0, parentTaps = 0;

        var parent = renderTree ? new SkiaLayer() : new FallbackLayer();
        parent.LockChildrenGestures = lockTouch;
        parent.HorizontalOptions = LayoutOptions.Fill;
        parent.VerticalOptions = LayoutOptions.Fill;
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
        host.AdvanceFrames(2);
        var parentTapsOnChild = parentTaps;

        robot.Tap(300, 250);
        host.AdvanceFrames(2);
        var parentTapsOnEmpty = parentTaps - parentTapsOnChild;

        robot.Pan(100, 150, 100, 50, durationMs: 160, steps: 8);
        host.AdvanceFrames(2);

        return new Result(childDowns, childTaps, childPans, parentTapsOnChild, parentTapsOnEmpty);
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

        // the locked control keeps the tap its child did not get, over the child and over empty space
        Assert.Equal(tapReachesChild ? 0 : 1, tree.ParentTapsOnChild);
        Assert.Equal(1, tree.ParentTapsOnEmpty);

        Assert.Equal(fallback, tree);
    }

    private record SiblingResult(int Downs, int Taps, int Pans);

    /// <summary>
    /// A sibling stacked under the locked layer: taps over the layer's child and over its empty space, then a pan
    /// over its empty space. The locked layer has no handlers of its own.
    /// </summary>
    private static SiblingResult RunSibling(LockTouch lockTouch, bool renderTree)
    {
        using var host = new HeadlessCanvasHost(400, 300);
        var robot = new GestureRobot(host);
        int downs = 0, taps = 0, pans = 0;

        var locked = renderTree ? new SkiaLayer() : new FallbackLayer();
        locked.LockChildrenGestures = lockTouch;
        locked.HorizontalOptions = LayoutOptions.Fill;
        locked.VerticalOptions = LayoutOptions.Fill;
        locked.Children.Add(new SkiaShape { WidthRequest = 200, HeightRequest = 200, BackgroundColor = Colors.Red }
            .WithGestures((me, args, apply) => me));

        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaShape { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, BackgroundColor = Colors.Blue }
                    .WithGestures((me, args, apply) =>
                    {
                        switch (args.Type)
                        {
                            case TouchActionResult.Down: downs++; break;
                            case TouchActionResult.Tapped: taps++; break;
                            case TouchActionResult.Panning: pans++; break;
                        }
                        return me;
                    }),
                locked,
            }
        };
        host.AdvanceFrames(4);

        robot.Tap(100, 100);
        robot.Tap(300, 250);
        robot.Pan(300, 250, 300, 150, durationMs: 160, steps: 8);
        host.AdvanceFrames(2);

        return new SiblingResult(downs, taps, pans);
    }

    [Theory]
    [InlineData(LockTouch.Disabled, 1)]
    [InlineData(LockTouch.Enabled, 0)]
    [InlineData(LockTouch.PassNone, 2)]
    [InlineData(LockTouch.PassTap, 1)]
    [InlineData(LockTouch.PassTapAndLongPress, 1)]
    public void Enabled_ConsumesForItself_SiblingBelowGetsNothing(LockTouch lockTouch, int siblingTaps)
    {
        var tree = RunSibling(lockTouch, renderTree: true);
        var fallback = RunSibling(lockTouch, renderTree: false);

        Assert.Equal(siblingTaps, tree.Taps);
        var reachesBelow = lockTouch != LockTouch.Enabled;
        Assert.Equal(reachesBelow, tree.Downs > 0);
        Assert.Equal(reachesBelow, tree.Pans > 0);

        Assert.Equal(fallback, tree);
    }
}
