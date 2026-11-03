using DrawnUi.Views;
using SkiaSharp;
using System.Runtime.CompilerServices;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The surface pool keeps reusable surfaces per size. A window that is resized asks for a new size at every step:
/// the old sizes must not keep their surfaces forever.
/// </summary>
public class SurfacePoolBudgetTests
{
    private sealed class DisposeNow : IDisposeManager
    {
        public int Disposed;

        public void DisposeObject(IDisposable disposable, [CallerMemberName] string caller = null)
        {
            disposable?.Dispose();
            Disposed++;
        }
    }

    [Fact]
    public void ResizingThroughManySizes_KeepsThePoolWithinItsBudget()
    {
        var disposer = new DisposeNow();
        var pool = new SurfaceCacheManager(disposer);

        // a window resized through 150 sizes, each frame drawing two window-sized caches
        for (var i = 0; i < 150; i++)
        {
            var a = pool.GetSurface(1200 + i * 4, 900 + i * 2);
            var b = pool.GetSurface(1200 + i * 4, 900 + i * 2);
            pool.ReturnSurface(a);
            pool.ReturnSurface(b);
        }

        Assert.InRange(pool.PooledBytes, 1, 128L * 1024 * 1024);
        Assert.True(disposer.Disposed > 250, $"old sizes were not released: {disposer.Disposed} surfaces disposed");

        // the current size is still reused
        var again = pool.GetSurface(1200 + 149 * 4, 900 + 149 * 2);
        var bytes = pool.PooledBytes;
        pool.ReturnSurface(again);
        Assert.Equal(bytes + 4L * (1200 + 149 * 4) * (900 + 149 * 2), pool.PooledBytes);
        pool.Dispose();
        Assert.Equal(0, pool.PooledBytes);
    }
}
