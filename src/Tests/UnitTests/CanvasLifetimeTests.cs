using System.Reflection;
using System.Runtime.CompilerServices;
using DrawnUi.Draw;
using DrawnUi.Views;
using Xunit;

namespace UnitTests;

public class CanvasLifetimeTests : DrawnTestsBase
{
    class ReloadCounter : DrawnView
    {
        public int Reloads;

        public override void OnHotReload() => Reloads++;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference CreateAndDrop() => new(new DrawnView());

    static void RaiseHotReload() =>
        typeof(Super).GetMethod("OnHotReload", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { Array.Empty<Type>() });

    [Fact]
    public void A_canvas_dropped_without_Dispose_is_collected()
    {
        var weak = CreateAndDrop();

        for (var i = 0; i < 3 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(weak.IsAlive);
    }

    [Fact]
    public void Hot_reload_reaches_a_live_canvas_and_not_a_disposed_one()
    {
        var live = new ReloadCounter();
        var disposed = new ReloadCounter();
        disposed.Dispose();

        RaiseHotReload();

        Assert.Equal(1, live.Reloads);
        Assert.Equal(0, disposed.Reloads);
        GC.KeepAlive(live);
    }
}
