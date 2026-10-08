using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SafeAction / SyncUniqueAction merge repeated work of one control into one run per frame by a key made with
/// CombineToLong(Uid, value). That key was negative for about half the Uids, and SafeAction reads a negative key
/// as "no key", so those controls ran every queued call (MAUI Padding / size request / IsVisible bursts).
/// </summary>
public class KeyedActionTests
{
    [Fact]
    public void CombineToLong_NeverNegative()
    {
        for (int i = 0; i < 20000; i++)
        {
            var uid = Guid.NewGuid();
            Assert.True(SkiaControl.CombineToLong(uid, i % 200) >= 0);
        }
    }

    [Fact]
    public void CombineToLong_SameInputSameKey_DifferentValueDifferentKey()
    {
        var uid = Guid.NewGuid();
        Assert.Equal(SkiaControl.CombineToLong(uid, 1), SkiaControl.CombineToLong(uid, 1));
        Assert.NotEqual(SkiaControl.CombineToLong(uid, 1), SkiaControl.CombineToLong(uid, 2));
    }

    /// <summary>
    /// Calls from a thread other than the drawing one are queued: three with one key run once, before the next
    /// frame, for every control (64 random Uids, about half of which had a negative key before).
    /// </summary>
    [Fact]
    public void SyncUniqueAction_FromAnotherThread_RunsOncePerFrame_ForEveryControl()
    {
        using var host = new HeadlessCanvasHost(200, 200, scale: 1f, background: Colors.Black);
        var controls = Enumerable.Range(0, 64).Select(_ => new SkiaControl
        {
            WidthRequest = 10,
            HeightRequest = 10,
        }).ToList();
        var layer = new SkiaLayout { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };
        foreach (var control in controls)
            layer.AddSubView(control);
        host.Canvas.Content = layer;
        host.AdvanceFrames(3);

        var runs = new int[controls.Count];
        // a real other thread: Task.Wait may run a task inline on the waiting (drawing) thread
        var other = new Thread(() =>
        {
            for (int i = 0; i < controls.Count; i++)
            {
                var index = i;
                for (int call = 0; call < 3; call++)
                    controls[i].SyncUniqueAction(() => runs[index]++, 1);
            }
        });
        other.Start();
        other.Join();

        Assert.All(runs, r => Assert.Equal(0, r)); // queued, not run inline
        host.RenderFrame(16);
        Assert.All(runs, r => Assert.Equal(1, r));
    }
}
