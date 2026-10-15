using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A horizontal strip slid away with TranslationX, set every frame as an animation does (FiltersCamera, 2026-10-12):
/// its moved box stays full of cells at every step, templated or plain, and its cells end exactly at the box's
/// right edge. The scroll clips to its own box, which moves with the translation: the screen to the right of that
/// edge is outside the strip, so it is empty by design (a cell cut there is the clip, not culling).
/// </summary>
public class TranslatedScrollCullingTests
{
    readonly ITestOutputHelper _out;
    public TranslatedScrollCullingTests(ITestOutputHelper output) => _out = output;

    const int W = 400, H = 300, StripTop = 100, StripHeight = 92;

    static SkiaScroll Strip(bool autoCache, bool skipOutOfBounds, bool templated) => new SkiaScroll
    {
        AutoCache = autoCache,
        Orientation = ScrollOrientation.Horizontal,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Start,
        Margin = new Thickness(0, StripTop, 0, 0),
        HeightRequest = StripHeight,
        Padding = new Thickness(0, 8),
        SkipRenderingOutOfBounds = skipOutOfBounds,
        Header = new SkiaLayout { WidthRequest = 60, VerticalOptions = LayoutOptions.Fill },
        Footer = new SkiaLayout { WidthRequest = 12, VerticalOptions = LayoutOptions.Fill },
        Content = templated ? new SkiaLayout
        {
            Type = LayoutType.Row,
            VerticalOptions = LayoutOptions.Center,
            Spacing = 8,
            RecyclingTemplate = RecyclingTemplate.Disabled,
            VirtualisationInflated = 50,
            ItemTemplate = new DataTemplate(() => new SkiaShape
            {
                WidthRequest = 62,
                HeightRequest = 62,
                BackgroundColor = Colors.Orange,
                UseCache = SkiaCacheType.Image,
            }),
            ItemsSource = Enumerable.Range(0, 20).ToList(),
        }
        : new SkiaRow // OptionStrip: plain children
        {
            VerticalOptions = LayoutOptions.Center,
            Spacing = 8,
            Children = Enumerable.Range(0, 20).Select(i => (SkiaControl)new SkiaShape
            {
                WidthRequest = 62,
                HeightRequest = 62,
                BackgroundColor = Colors.Orange,
                UseCache = SkiaCacheType.Image,
            }).ToList(),
        }
    };

    /// <summary>Fraction of drawn (non-black) pixels along the strip's middle row between x0 and x1.</summary>
    static double Filled(HeadlessCanvasHost host, float scale, int x0, int x1)
    {
        using var image = host.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        var y = (int)((StripTop + StripHeight / 2) * scale);
        int drawn = 0, total = 0;
        for (int x = (int)(Math.Max(0, x0) * scale); x < (int)(Math.Min(W, x1) * scale); x++, total++)
        {
            var c = bitmap.GetPixel(x, y);
            if (c.Red > 40 || c.Green > 40 || c.Blue > 40)
                drawn++;
        }
        return total == 0 ? 1 : drawn / (double)total;
    }

    static double LastDrawn(HeadlessCanvasHost host, float scale)
    {
        using var image = host.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        var y = (int)((StripTop + StripHeight / 2) * scale);
        for (int x = bitmap.Width - 1; x >= 0; x--)
        {
            var c = bitmap.GetPixel(x, y);
            if (c.Red > 40 || c.Green > 40 || c.Blue > 40)
                return x / scale;
        }
        return -1;
    }

    [Theory]
    [InlineData(true, true, true, 1f)]
    [InlineData(false, false, true, 1f)]
    [InlineData(true, true, false, 1f)]
    [InlineData(true, true, true, 1.25f)]
    [InlineData(true, true, false, 1.25f)]
    public void SlidingLeft_StripBoxStaysFull(bool autoCache, bool skipOutOfBounds, bool templated, float scale)
    {
        using var host = new HeadlessCanvasHost((int)(W * scale), (int)(H * scale), scale: scale, background: Colors.Black);
        SkiaScroll strip = null;
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { Strip(autoCache, skipOutOfBounds, templated).Assign(out strip) }
        };
        host.AdvanceFrames(20);

        var failures = new List<string>();
        // TranslationX set every frame, as an animation does (CubicOut over 220 ms, out and back)
        var steps = Enumerable.Range(1, 14).Select(i => 1 - Math.Pow(1 - i / 14.0, 3)).ToList();
        steps.AddRange(steps.Select(x => 1 - x).ToList());
        foreach (var t in steps)
        {
            strip.TranslationX = -W * t;
            host.RenderFrame(16);
            // on screen the strip covers [0, W - W*t); its cells start after the 60 pt header
            var right = (int)(W - W * t);
            var filled = Filled(host, scale, 60, right - 8);
            var edge = right > 0 ? LastDrawn(host, scale) : -1;
            if (right > 8 && Math.Abs(edge - (W - W * t)) > 2)
                failures.Add($"t={t:0.00}: cells end at {edge:0}, the box at {W - W * t:0}");
            _out.WriteLine($"t={t:0.00}: drawn {filled:P0} of [60, {right - 8}), last drawn x {LastDrawn(host, scale):0} pt, box right edge {W - W * t:0} pt");
            if (right - 8 > 60 && filled < 0.7)
                failures.Add($"t={t:0.00}: {filled:P0}");
        }
        Assert.Empty(failures);
    }
}
