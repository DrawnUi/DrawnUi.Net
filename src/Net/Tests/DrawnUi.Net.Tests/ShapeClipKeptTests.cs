using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A shape keeps its clip path between paints only while what the clip is made from stays the same
/// (SkiaShape.ClipDependsOnlyOnShape): an opted-in subclass follows corner and size changes, and a subclass that
/// makes its own clip gets it made on every paint.
/// </summary>
public class ShapeClipKeptTests
{
    sealed class KeptShape : SkiaShape
    {
        protected override bool ClipDependsOnlyOnShape => true;
    }

    /// <summary>Clips its children to its left part, as wide as <see cref="Share"/> of it: a clip no flag can see.</summary>
    sealed class OwnClipShape : SkiaShape
    {
        public float Share = 1;

        public override SKPath CreateClip(object arguments, bool usePosition, SKPath path = null)
        {
            path ??= new SKPath();
            var r = MeasuredStrokeAwareClipSize;
            path.AddRect(new SKRect(r.Left, r.Top, r.Left + r.Width * Share, r.Bottom));
            return path;
        }
    }

    sealed class PlainSubclass : SkiaShape
    {
    }

    /// <summary>Median bytes allocated by a frame redrawing 40 uncached rounded shapes of one kind, each with a child.</summary>
    static long FrameBytes(Func<SkiaShape> make)
    {
        using var host = new HeadlessCanvasHost(800, 800, 1f, Colors.Black);
        var wrap = new SkiaWrap { Spacing = 4, UseCache = SkiaCacheType.None };
        for (var i = 0; i < 40; i++)
        {
            var shape = make();
            shape.WidthRequest = 80;
            shape.HeightRequest = 40;
            shape.CornerRadius = 10;
            shape.BackgroundColor = Colors.Red;
            shape.UseCache = SkiaCacheType.None;
            shape.Children.Add(new SkiaLabel("x") { TextColor = Colors.White });
            wrap.Children.Add(shape);
        }
        host.Canvas.Content = wrap;
        host.AdvanceFrames(60, 16.7);

        var bytes = new long[100];
        for (var i = 0; i < bytes.Length; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            host.RenderFrame(16.7);
            bytes[i] = GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Array.Sort(bytes);
        return bytes[bytes.Length / 2];
    }

    static (HeadlessCanvasHost Host, T Shape) Scene<T>(T shape) where T : SkiaShape
    {
        var host = new HeadlessCanvasHost(200, 200, 1f, Colors.Black);
        shape.WidthRequest = 100;
        shape.HeightRequest = 100;
        shape.UseCache = SkiaCacheType.None;
        shape.Children.Add(new SkiaLayer { BackgroundColor = Colors.Red, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill });
        host.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { shape } };
        host.AdvanceFrames(3);
        return (host, shape);
    }

    static bool IsRed(HeadlessCanvasHost host, int x, int y)
    {
        using var image = host.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.GetPixel(x, y).Red > 200;
    }

    [Fact]
    public void A_kept_clip_follows_corner_and_size_changes()
    {
        var (host, shape) = Scene(new KeptShape());
        using var _ = host;
        Assert.True(IsRed(host, 1, 1), "square corner: the child shows in it");

        shape.CornerRadius = 40;
        host.AdvanceFrames(3);
        Assert.False(IsRed(host, 1, 1), "rounded corner: the clip cuts the child there");

        shape.WidthRequest = 150;
        host.AdvanceFrames(3);
        Assert.True(IsRed(host, 140, 50), "wider: the clip follows the new size");
        Assert.False(IsRed(host, 148, 2), "and keeps its rounded corner at the new edge");
    }

    [Fact]
    public void A_shape_with_its_own_clip_gets_it_made_on_every_paint()
    {
        var (host, shape) = Scene(new OwnClipShape());
        using var _ = host;
        Assert.True(IsRed(host, 90, 50));

        shape.Share = 0.5f;
        shape.Update();
        host.AdvanceFrames(3);
        Assert.False(IsRed(host, 90, 50), "the clip made by the subclass changed with the same shape and size");
        Assert.True(IsRed(host, 20, 50));
    }

    [Fact]
    public void An_opted_in_subclass_keeps_its_clip_as_a_plain_shape_does()
    {
        var plain = FrameBytes(() => new SkiaShape());
        var kept = FrameBytes(() => new KeptShape());
        var made = FrameBytes(() => new PlainSubclass());

        // a clip made again on every paint costs a path builder per shape (about 80 B each, 40 shapes)
        Assert.True(made - plain > 40 * 40, $"a subclass that does not opt in makes its clip on every paint: {made} B vs {plain} B");
        Assert.True(kept <= plain + 256, $"an opted-in subclass keeps its clip: {kept} B vs {plain} B for plain shapes");
    }
}
