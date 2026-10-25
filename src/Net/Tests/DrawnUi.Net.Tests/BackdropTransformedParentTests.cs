using SkiaSharp;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A SkiaBackdrop shows what is behind it. Inside a scaled or moved parent it must still copy the screen area it
/// covers on screen: with no blur, every pixel it covers equals the background at that same screen point.
/// </summary>
[Collection("Multithreaded")] // reads pixels of fresh canvases
public class BackdropTransformedParentTests
{
    readonly ITestOutputHelper _output;

    public BackdropTransformedParentTests(ITestOutputHelper output) => _output = output;

    static readonly Color[] Stripes = { Colors.Red, Colors.Green, Colors.Blue, Colors.Yellow };

    static SKBitmap Render(double scale, double translateX, double rotation)
    {
        using var host = new HeadlessCanvasHost(400, 200, scale: 1f, background: Colors.Black);
        var stripes = new SkiaRow { Spacing = 0, VerticalOptions = LayoutOptions.Fill };
        foreach (var c in Stripes)
            stripes.AddSubView(new SkiaShape { BackgroundColor = c, WidthRequest = 100, VerticalOptions = LayoutOptions.Fill });

        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                stripes,
                new SkiaLayer // the popup frame: scaled / moved around its center
                {
                    WidthRequest = 200,
                    HeightRequest = 100,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Scale = scale,
                    TranslationX = translateX,
                    Rotation = rotation,
                    Children =
                    {
                        new SkiaBackdrop { Blur = 0, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill },
                    }
                },
            }
        };
        host.AdvanceFrames(8);
        using var image = host.Snapshot();
        return SKBitmap.FromImage(image);
    }

    static SKBitmap Background()
    {
        var bmp = new SKBitmap(400, 200);
        for (var x = 0; x < 400; x++)
        {
            var c = Stripes[x / 100];
            for (var y = 0; y < 200; y++)
                bmp.SetPixel(x, y, new SKColor((byte)(c.Red * 255), (byte)(c.Green * 255), (byte)(c.Blue * 255)));
        }
        return bmp;
    }

    [Theory]
    [InlineData(1.0, 0, 0)]
    [InlineData(0.75, 0, 0)]
    [InlineData(0.5, 0, 0)]
    [InlineData(1.0, 40, 0)]
    [InlineData(0.75, 30, 0)]
    [InlineData(1.0, 0, 30)]
    [InlineData(0.75, 20, 30)]
    [InlineData(1.0, 150, 0)] // partly off screen
    public void Backdrop_InTransformedParent_CopiesTheScreenItCovers(double scale, double translateX, double rotation)
    {
        using var shot = Render(scale, translateX, rotation);
        using var bg = Background();

        // the frame covers 200x100 around the center, scaled, moved, turned: sample a central ellipse that stays
        // inside it at any angle, on screen only
        float w = (float)(200 * scale), h = (float)(100 * scale);
        float cx = 200 + (float)translateX, cy = 100;
        float r = Math.Min(w, h) * 0.4f;
        int wrong = 0, total = 0;
        for (var x = (int)(cx - r); x < (int)(cx + r); x += 2)
        for (var y = (int)(cy - r); y < (int)(cy + r); y += 2)
        {
            if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r || x < 0 || x >= 400 || y < 0 || y >= 200)
                continue;
            total++;
            var a = shot.GetPixel(x, y);
            var b = bg.GetPixel(x, y);
            if (Math.Abs(a.Red - b.Red) > 40 || Math.Abs(a.Green - b.Green) > 40 || Math.Abs(a.Blue - b.Blue) > 40)
                wrong++;
        }
        _output.WriteLine($"scale={scale} tx={translateX} rot={rotation}: {wrong}/{total} pixels differ from the background behind");
        Assert.Equal(0, wrong);
    }

    /// <summary>The BlockChampion popup: the backdrop sits in a SkiaShape (picture cache by default) inside a frame
    /// that zooms 0.75 to 1. After the zoom the glass must show the screen behind it at its final place.</summary>
    [Theory]
    [InlineData(SkiaCacheType.Operations)]
    [InlineData(SkiaCacheType.None)]
    public void Backdrop_InShape_AfterParentZoom_CopiesTheScreenItCovers(SkiaCacheType shapeCache)
    {
        using var host = new HeadlessCanvasHost(400, 200, scale: 1f, background: Colors.Black);
        var stripes = new SkiaRow { Spacing = 0, VerticalOptions = LayoutOptions.Fill };
        foreach (var c in Stripes)
            stripes.AddSubView(new SkiaShape { BackgroundColor = c, WidthRequest = 50, VerticalOptions = LayoutOptions.Fill });
        foreach (var c in Stripes)
            stripes.AddSubView(new SkiaShape { BackgroundColor = c, WidthRequest = 50, VerticalOptions = LayoutOptions.Fill });
        SkiaLayer frame = null;
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                stripes,
                new SkiaLayer
                {
                    WidthRequest = 200, HeightRequest = 100,
                    HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
                    Scale = 0.75,
                    Children =
                    {
                        new SkiaShape
                        {
                            UseCache = shapeCache,
                            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
                            Children = { new SkiaBackdrop { Blur = 0, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill } },
                        },
                    }
                }.Assign(out frame),
            }
        };
        host.AdvanceFrames(6);
        frame.Scale = 1; // the zoom ends
        host.AdvanceFrames(6);

        using var image = host.Snapshot();
        using var shot = SKBitmap.FromImage(image);
        int wrong = 0, total = 0;
        for (var x = 110; x < 290; x += 3)
        for (var y = 60; y < 140; y += 5)
        {
            total++;
            var expected = Stripes[(x / 50) % 4];
            var a = shot.GetPixel(x, y);
            if (Math.Abs(a.Red - expected.Red * 255) > 40 || Math.Abs(a.Green - expected.Green * 255) > 40 || Math.Abs(a.Blue - expected.Blue * 255) > 40)
                wrong++;
        }
        _output.WriteLine($"shape cache {shapeCache}: {wrong}/{total} pixels differ after the zoom");
        Assert.Equal(0, wrong);
    }

    /// <summary>The iPhone game-over popup: the backdrop sits in a SkiaShape with its default picture cache. The
    /// picture was recorded once with the copy of the screen inside, then replayed frozen while the frame zoomed and
    /// what was behind changed. The glass must follow what is behind it now.</summary>
    [Theory]
    [InlineData(SkiaCacheType.Operations)]
    [InlineData(SkiaCacheType.OperationsFull)]
    [InlineData(SkiaCacheType.None)]
    public void Backdrop_InPictureCachedShape_FollowsWhatIsBehind(SkiaCacheType shapeCache)
    {
        using var host = new HeadlessCanvasHost(400, 200, scale: 1f, background: Colors.Black);
        SkiaShape behind = null;
        SkiaShape shape = null;
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaShape { BackgroundColor = Colors.Red, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill }.Assign(out behind),
                new SkiaLayer
                {
                    WidthRequest = 200, HeightRequest = 100,
                    HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new SkiaShape
                        {
                            UseCache = shapeCache,
                            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
                            Children = { new SkiaBackdrop { Blur = 0, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill } },
                        }.Assign(out shape),
                    }
                },
            }
        };
        host.AdvanceFrames(6);
        behind.BackgroundColor = Colors.Blue; // what is behind the glass changes
        host.AdvanceFrames(6);

        using var image = host.Snapshot();
        using var shot = SKBitmap.FromImage(image);
        var center = shot.GetPixel(200, 100);
        _output.WriteLine($"shape cache {shapeCache}: glass shows {center}, uses {shape.UsingCacheType}");
        Assert.True(center.Blue > 200 && center.Red < 50, $"the glass shows a frozen copy: {center}");
    }
}
