using System.Runtime.CompilerServices;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A page removed from a live canvas is collected even when controls deep inside it run or paused animations: the
/// canvas keeps no animator of a removed subtree.
/// </summary>
[Collection("Multithreaded")] // forces full GCs
public class RemovedSubtreeLifetimeTests
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AddRunRemove(HeadlessCanvasHost host, SkiaLayout root, bool pause)
    {
        var spinner = new SkiaShape { WidthRequest = 20, HeightRequest = 20, BackgroundColor = Colors.Red }
            .AnimateRotation(0, 360, seconds: 1, repeat: -1);
        var page = new SkiaLayout
        {
            Children = new List<SkiaControl> { new SkiaLayout { Children = new List<SkiaControl> { spinner } } },
        };
        root.AddSubView(page);
        host.AdvanceFrames(10);
        if (pause)
            host.Canvas.SetPauseStateOfAllAnimatorsByParent(spinner, true);
        root.RemoveSubView(page);
        host.AdvanceFrames(5);
        return new WeakReference(page);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_removed_page_with_animated_children_is_collected(bool pause)
    {
        using var host = new HeadlessCanvasHost(200, 200);
        var root = new SkiaLayout { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };
        host.Canvas.Content = root;
        host.AdvanceFrames(2);

        var weak = AddRunRemove(host, root, pause);
        for (var i = 0; i < 5 && weak.IsAlive; i++)
        {
            host.AdvanceFrames(2);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(weak.IsAlive);
    }

    [Fact]
    public void A_removed_page_animation_pauses_and_resumes_when_added_back()
    {
        using var host = new HeadlessCanvasHost(200, 200);
        var root = new SkiaLayout { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };
        host.Canvas.Content = root;
        var spinner = new SkiaShape { WidthRequest = 20, HeightRequest = 20, BackgroundColor = Colors.Red }
            .AnimateRotation(0, 360, seconds: 1, repeat: -1);
        var page = new SkiaLayout { Children = new List<SkiaControl> { new SkiaLayout { Children = new List<SkiaControl> { spinner } } } };
        root.AddSubView(page);
        host.AdvanceFrames(10);

        root.RemoveSubView(page);
        host.AdvanceFrames(2);
        var paused = spinner.Rotation;
        host.AdvanceFrames(10);
        Assert.Equal(paused, spinner.Rotation); // off the canvas: nothing ticks
        Assert.DoesNotContain(host.Canvas.AnimatingControls.Values, a => a.Parent == spinner);

        root.AddSubView(page);
        host.AdvanceFrames(10);
        Assert.NotEqual(paused, spinner.Rotation); // back on the canvas: it runs again
    }

    [Fact]
    public void Key_handlers_hold_a_removed_control_weakly_and_fire_only_on_the_canvas()
    {
        using var host = new HeadlessCanvasHost(200, 200);
        var root = new SkiaLayout { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };
        host.Canvas.Content = root;
        var keys = 0;
        var page = new SkiaLayout().OnKeyDown((me, key) => keys++);
        root.AddSubView(page);
        host.AdvanceFrames(2);

        KeyboardManager.KeyboardPressed(InputKey.KeyA);
        Assert.Equal(1, keys);

        root.RemoveSubView(page);
        KeyboardManager.KeyboardPressed(InputKey.KeyA);
        Assert.Equal(1, keys); // off the canvas: no keys

        root.AddSubView(page);
        KeyboardManager.KeyboardPressed(InputKey.KeyA);
        Assert.Equal(2, keys);
        KeyboardManager.KeyboardReleased(InputKey.KeyA);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AddKeyedAndRemove(HeadlessCanvasHost host, SkiaLayout root)
    {
        var page = new SkiaLayout().OnKeyDown((me, key) => { });
        root.AddSubView(page);
        host.AdvanceFrames(2);
        root.RemoveSubView(page);
        host.AdvanceFrames(3); // the canvas's per-frame lists let go at the next frame
        return new WeakReference(page);
    }

    [Fact]
    public void A_removed_page_with_key_handlers_is_collected()
    {
        using var host = new HeadlessCanvasHost(200, 200);
        var root = new SkiaLayout { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };
        host.Canvas.Content = root;
        var weak = AddKeyedAndRemove(host, root);
        for (var i = 0; i < 5 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Assert.False(weak.IsAlive);
    }
}
