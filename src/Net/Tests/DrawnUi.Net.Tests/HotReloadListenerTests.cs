using System.Reflection;
using System.Runtime.CompilerServices;
using DrawnUi.Draw;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Hot reload listeners (the WPF element, SkiaShell) are held weakly: a canvas or shell dropped without Dispose is
/// collected, while a live one still reloads.
/// </summary>
[Collection("Multithreaded")] // forces full GCs: never in parallel with timing tests
public class HotReloadListenerTests
{
    private sealed class Owner
    {
        public int Reloads;
    }

    private static void RaiseHotReload() =>
        typeof(Super).GetMethod("RaiseHotReload", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { null });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AddAndDrop()
    {
        var owner = new Owner();
        Super.AddHotReloadListener(owner, _ => owner.Reloads++);
        return new WeakReference(owner);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateShellAndDrop()
    {
        var shell = new SkiaShell { RootBuilder = () => new SkiaLayout() };
        return new WeakReference(shell);
    }

    private static void Collect(WeakReference weak)
    {
        for (var i = 0; i < 3 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    [Fact]
    public void An_owner_dropped_without_removing_its_listener_is_collected()
    {
        var weak = AddAndDrop();
        Collect(weak);
        Assert.False(weak.IsAlive);
    }

    [Fact]
    public void A_shell_dropped_without_Dispose_is_collected()
    {
        var weak = CreateShellAndDrop();
        Collect(weak);
        Assert.False(weak.IsAlive);
    }

    [Fact]
    public void A_live_owner_reloads_and_a_removed_one_does_not()
    {
        var live = new Owner();
        var removed = new Owner();
        Super.AddHotReloadListener(live, _ => live.Reloads++);
        Super.AddHotReloadListener(removed, _ => removed.Reloads++);
        Super.RemoveHotReloadListener(removed);

        RaiseHotReload();

        Assert.Equal(1, live.Reloads);
        Assert.Equal(0, removed.Reloads);
    }
}
