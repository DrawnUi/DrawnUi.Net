using System.Runtime.CompilerServices;
using DrawnUi.Draw;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The image cache keeps at most <see cref="SkiaImageManager.CacheBudgetMb"/> of decoded pictures: over it the pictures
/// used longest ago leave the cache. A picture something still holds is found again (never decoded twice); one nobody
/// holds is gone; one removed on purpose is never served again.
/// </summary>
public class ImageCacheBudgetTests
{
    static SKBitmap Picture() => new(400, 400); // 640 KB

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void AddUnheld(SkiaImageManager manager, string key) => manager.AddToCache(key, Picture(), 1800);

    [Fact]
    public void OverBudget_OldestLeave_HeldOnesAreFoundAgain_OthersGo()
    {
        var manager = SkiaImageManager.Instance;
        var budget = SkiaImageManager.CacheBudgetMb;
        var id = Guid.NewGuid().ToString("N");
        string Key(string name) => $"budget-{id}-{name}.png";
        SkiaImageManager.CacheBudgetMb = 1; // room for one picture
        try
        {
            var held = Picture();
            Assert.True(manager.AddToCache(Key("held"), held, 1800));
            AddUnheld(manager, Key("unheld"));
            var newest = Picture();
            Assert.True(manager.AddToCache(Key("newest"), newest, 1800));

            Assert.True(manager.CacheMemoryMb <= 1, $"cache holds {manager.CacheMemoryMb:0.00} MB");

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // still held by "a control": the same picture comes back, no second copy
            Assert.Same(held, manager.GetFromCacheInternal(Key("held")));
            // nobody held it: gone, the next load reads the file again
            Assert.Null(manager.GetFromCacheInternal(Key("unheld")));

            // removed on purpose: never served again, even while held
            manager.RemoveFromCache(Key("held"));
            Assert.Null(manager.GetFromCacheInternal(Key("held")));

            GC.KeepAlive(held);
            GC.KeepAlive(newest);
        }
        finally
        {
            SkiaImageManager.CacheBudgetMb = budget;
            foreach (var name in new[] { "held", "unheld", "newest" })
                manager.RemoveFromCache(Key(name));
        }
    }

    [Fact]
    public void UnderBudget_NothingLeaves()
    {
        var manager = SkiaImageManager.Instance;
        var id = Guid.NewGuid().ToString("N");
        var key = $"budget-{id}-keep.png";
        try
        {
            AddUnheld(manager, key);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Assert.NotNull(manager.GetFromCacheInternal(key));
        }
        finally
        {
            manager.RemoveFromCache(key);
        }
    }
}
