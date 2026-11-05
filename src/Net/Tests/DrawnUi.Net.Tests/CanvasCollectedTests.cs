using System.Runtime.CompilerServices;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A canvas that was used and dropped is collected: the global refresh event holds canvases weakly, and the
/// background bake workers keep no reference to the last control they baked.
/// </summary>
[Collection("Multithreaded")] // real bake threads
public class CanvasCollectedTests
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference canvas, WeakReference baked) UseAndDrop(bool dispose)
    {
        var host = new HeadlessCanvasHost(200, 200);
        var baked = new SkiaLayout
        {
            WidthRequest = 100, HeightRequest = 100, BackgroundColor = Colors.Red,
            UseCache = SkiaCacheType.ImageDoubleBuffered,
        };
        host.Canvas.Content = baked;
        for (var i = 0; i < 40; i++)
        {
            host.AdvanceFrames(2);
            if (host.NonBackgroundFraction(Colors.Black) > 0.01)
                break;
            Thread.Sleep(20);
        }
        var result = (new WeakReference(host.Canvas), new WeakReference(baked));
        if (dispose)
            host.Dispose();
        return result;
    }

    private static void Collect(WeakReference weak)
    {
        for (var i = 0; i < 5 && weak.IsAlive; i++)
        {
            Thread.Sleep(50);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_used_canvas_and_its_baked_control_are_collected(bool dispose)
    {
        var (canvas, baked) = UseAndDrop(dispose);
        Collect(baked);
        Collect(canvas);
        Assert.False(baked.IsAlive, "the baked control is still alive");
        Assert.False(canvas.IsAlive, "the canvas is still alive");
    }
}
