using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Verifies that CacheSharing=Shared causes all instances of the same control type on one Canvas
/// to share a single CachedObject — only the first render creates a cache; subsequent instances
/// reuse it without allocating their own.
/// </summary>
public class CacheSharingTests
{
    private readonly ITestOutputHelper _out;
    public CacheSharingTests(ITestOutputHelper output) => _out = output;

    // ── test control ──────────────────────────────────────────────────────────

    private class DividerLine : SkiaShape
    {
        public DividerLine()
        {
            Type = ShapeType.Rectangle;
            HeightRequest = 1;
            HorizontalOptions = LayoutOptions.Fill;
            BackgroundColor = Colors.Red;
            UseCache = SkiaCacheType.Image;
            CacheSharing = CacheSharingType.Shared;
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static (HeadlessCanvasHost host, List<DividerLine> dividers) BuildScene(
        int count = 5, int width = 400, int height = 600)
    {
        var host = new HeadlessCanvasHost(width, height);

        var dividers = Enumerable.Range(1, count)
            .Select(i => new DividerLine().WithTag(i.ToString()))
            .ToList();

        var column = new SkiaLayout
        {
            Type = LayoutType.Column,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Spacing = 20,
        };

        foreach (var d in dividers)
            column.AddSubView(d);

        host.Canvas.Content = column;

        return (host, dividers);
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Shared_Cache_Created_Only_Once_For_All_Dividers()
    {
        var (host, dividers) = BuildScene(count: 5);
        using var _ = host;

        int cacheCreatedCount = 0;
        foreach (var d in dividers)
            d.CreatedCache += (_, __) => Interlocked.Increment(ref cacheCreatedCount);

        // Warm up — enough frames for all controls to pass through DrawUsingRenderObject
        host.AdvanceFrames(5);

        _out.WriteLine($"CacheCreated events fired: {cacheCreatedCount}");

        // Only 1 actual CachedObject should have been created; others accepted the shared one.
        Assert.Equal(1, cacheCreatedCount);
    }

    [Fact]
    public void All_Dividers_Return_Same_RenderObject_Instance()
    {
        var (host, dividers) = BuildScene(count: 5);
        using var _ = host;

        host.AdvanceFrames(5);

        var first = dividers[0].RenderObject;
        Assert.NotNull(first);

        foreach (var d in dividers)
        {
            var ro = d.RenderObject;
            Assert.NotNull(ro);
            Assert.Same(first, ro);
        }

        _out.WriteLine($"Shared RenderObject id: {first!.Id}");
    }

    [Fact]
    public void Canvas_Cache_Has_Exactly_One_Entry_For_DividerLine_Type()
    {
        var (host, dividers) = BuildScene(count: 5);
        using var _ = host;

        host.AdvanceFrames(5);

        var canvas = host.Canvas;
        var sharedEntry = canvas.SharedCache.Get(typeof(DividerLine));

        Assert.NotNull(sharedEntry);

        // All dividers must point at that single entry
        foreach (var d in dividers)
            Assert.Same(sharedEntry, d.RenderObject);
    }

    [Fact]
    public void Cache_Free_Forces_Single_Re_Render_Then_Stable_Again()
    {
        var (host, dividers) = BuildScene(count: 5);
        using var _ = host;

        host.AdvanceFrames(5);

        int cacheCreatedCount = 0;
        foreach (var d in dividers)
            d.CreatedCache += (_, __) => Interlocked.Increment(ref cacheCreatedCount);

        // Evict shared entry — all dividers must re-render
        host.Canvas.SharedCache.Free<DividerLine>();

        host.AdvanceFrames(5);

        _out.WriteLine($"CacheCreated events after Free<T>: {cacheCreatedCount}");

        // Exactly 1 new cache object created after the eviction
        Assert.Equal(1, cacheCreatedCount);

        // And shared entry is back
        Assert.NotNull(host.Canvas.SharedCache.Get(typeof(DividerLine)));
    }

    [Fact]
    public void Default_Cache_Mode_Each_Divider_Has_Own_RenderObject()
    {
        var (host, dividers) = BuildScene(count: 3);
        using var _ = host;

        // Switch to Default sharing — each must maintain its own cache
        foreach (var d in dividers)
            d.CacheSharing = CacheSharingType.Default;

        host.AdvanceFrames(5);

        var renderObjects = dividers.Select(d => d.RenderObject).ToList();

        Assert.All(renderObjects, ro => Assert.NotNull(ro));

        // Each instance must have a distinct CachedObject
        Assert.Equal(renderObjects.Count, renderObjects.Distinct().Count());
    }
}
