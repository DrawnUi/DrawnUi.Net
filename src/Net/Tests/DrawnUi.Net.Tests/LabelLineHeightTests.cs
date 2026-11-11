using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Each line is as tall as its own content (default, LineHeightUniform false): round(largest ascent + largest descent)
/// of the fonts on the line (the label's own font included), times LineHeight, its baseline at the line's bottom minus
/// that descent, LineHeight's extra space split above and below. Labels of one font size draw the same pixels as with
/// LineHeightUniform true. The same reference rows are pinned by DrawnUi.React and DrawnUi.Rust (drawnui-cross 6bm).
/// </summary>
[Collection("FontRegistration")] // registers fonts: never in parallel with another class that does
public class LabelLineHeightTests
{
    const int W = 256, H = 160;

    static LabelLineHeightTests()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Path.Exists(Path.Combine(dir.FullName, ".git")))
            dir = dir.Parent;
        var fonts = Path.Combine(dir!.FullName, @"Tests\PreviewTests\Resources\Fonts");
        SkiaFontManager.Instance.RegisterFont("LineOrbitron", Path.Combine(fonts, "Orbitron-Black.ttf"));
        SkiaFontManager.Instance.RegisterFont("LineSans", Path.Combine(fonts, "OpenSans-Regular.ttf"));
        SkiaFontManager.Instance.Initialize();
    }

    static TextSpan Span(string text, double size = -1, string family = null)
    {
        var span = new TextSpan { Text = text };
        if (size > 0) span.FontSize = size;
        if (family != null) span.FontFamily = family;
        return span;
    }

    /// <summary>Renders white text on black in a 256 x 160 layer; returns the pixels and the label.</summary>
    static (byte[] Pixels, SkiaLabel Label) Draw(Action<SkiaLabel> setup, bool? uniform = null)
    {
        using var host = new HeadlessCanvasHost(W, H, scale: 1f, background: Colors.Black);
        SkiaLabel label = null;
        host.Canvas.Content = new SkiaLayer
        {
            WidthRequest = W, HeightRequest = H,
            Children =
            {
                new SkiaLabel
                {
                    FontFamily = "LineOrbitron", FontSize = 38, TextColor = Colors.White,
                    HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center,
                    UseCache = SkiaCacheType.None,
                }.Assign(out label)
            }
        };
        setup(label);
        if (uniform.HasValue)
            label.LineHeightUniform = uniform.Value;
        host.AdvanceFrames(3);
        using var image = host.Snapshot();
        using var bitmap = new SKBitmap(new SKImageInfo(W, H, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        image.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0);
        return (bitmap.Bytes, label);
    }

    /// <summary>First and last rows between y0 and y1 with a lit pixel (red > 128), as "first-last".</summary>
    static string Rows(byte[] pixels, int y0 = 0, int y1 = H)
    {
        int first = -1, last = -1;
        for (var y = y0; y < y1; y++)
        for (var x = 0; x < W; x++)
        {
            if (pixels[(y * W + x) * 4] > 128)
            {
                if (first < 0) first = y;
                last = y;
                break;
            }
        }
        return $"{first}-{last}";
    }

    static (float A, float D) Metrics(string family, float size)
    {
        using var font = new SKFont(SkiaFontManager.Instance.GetFont(family, 400), size);
        var m = font.Metrics;
        return (-m.Ascent, m.Descent);
    }

    public static IEnumerable<object[]> SingleSize => new[]
    {
        new object[] { "one line", (Action<SkiaLabel>)(l => l.Text = "GAME") },
        new object[] { "wrapped, centered", (Action<SkiaLabel>)(l => { l.Text = "GAME OVER"; l.VerticalOptions = LayoutOptions.Center; }) },
        new object[] { "three lines", (Action<SkiaLabel>)(l => { l.FontSize = 20; l.Text = "GAME\nOVER\nAGAIN"; }) },
        new object[] { "LineHeight 1.5", (Action<SkiaLabel>)(l => { l.Text = "GAME"; l.LineHeight = 1.5; }) },
        new object[] { "LineHeight 1.5, two lines", (Action<SkiaLabel>)(l => { l.FontSize = 20; l.Text = "GAME\nOVER"; l.LineHeight = 1.5; }) },
        new object[] { "LineSpacing 1.5", (Action<SkiaLabel>)(l => { l.FontSize = 20; l.Text = "GAME\nOVER"; l.LineSpacing = 1.5; }) },
        new object[] { "paragraph spacing", (Action<SkiaLabel>)(l => { l.FontSize = 20; l.Text = "GAME\nOVER"; l.ParagraphSpacing = 0.5; }) },
        new object[] { "empty line", (Action<SkiaLabel>)(l => { l.FontSize = 20; l.Text = "GAME\n\nOVER"; }) },
        new object[] { "vertical center", (Action<SkiaLabel>)(l => { l.FontSize = 20; l.Text = "GAME\nOVER"; l.VerticalOptions = LayoutOptions.Fill; l.VerticalTextAlignment = TextAlignment.Center; }) },
        new object[] { "vertical end", (Action<SkiaLabel>)(l => { l.FontSize = 20; l.Text = "GAME\nOVER"; l.VerticalOptions = LayoutOptions.Fill; l.VerticalTextAlignment = TextAlignment.End; }) },
        new object[] { "MaxLines 2", (Action<SkiaLabel>)(l => { l.FontSize = 20; l.Text = "GAME\nOVER\nAGAIN"; l.MaxLines = 2; }) },
        new object[] { "spans of the label's size", (Action<SkiaLabel>)(l => { l.FontSize = 20; l.Spans.Add(Span("GAME ")); l.Spans.Add(Span("OVER\nAGAIN", 20)); }) },
        new object[] { "stroke and shadow", (Action<SkiaLabel>)(l => { l.FontSize = 20; l.Text = "GAME\nOVER"; l.StrokeColor = Colors.Red; l.StrokeWidth = 1; l.DropShadowColor = Colors.Gray; l.DropShadowSize = 2; l.DropShadowOffsetY = 2; }) },
        new object[] { "OpenSans with descenders", (Action<SkiaLabel>)(l => { l.FontFamily = "LineSans"; l.FontSize = 17; l.Text = "gypsy jig\nquay pegs\nfog"; l.LineSpacing = 1.2; }) },
    };

    [Theory]
    [MemberData(nameof(SingleSize))]
    public void OneFontSize_SamePixelsAsUniform(string name, Action<SkiaLabel> setup)
    {
        var uniform = Draw(setup, true);
        var own = Draw(setup, false);
        Assert.Equal(uniform.Label.MeasuredSize.Pixels, own.Label.MeasuredSize.Pixels);
        Assert.Equal(uniform.Label.MeasuredLineHeight, own.Label.MeasuredLineHeight);
        Assert.True(uniform.Pixels.AsSpan().SequenceEqual(own.Pixels), $"{name}: pixels differ, rows {Rows(uniform.Pixels)} vs {Rows(own.Pixels)}");
    }

    [Fact]
    public void DefaultIsPerLine()
    {
        Assert.False(new SkiaLabel().LineHeightUniform);
    }

    [Fact]
    public void MixedSizes_EachLineItsOwnHeight_NothingOverlaps()
    {
        // a 40 pt line over a 14 pt line, and the other way round
        foreach (var bigFirst in new[] { true, false })
        {
            var (pixels, label) = Draw(l =>
            {
                l.FontFamily = "LineSans"; l.FontSize = 14; l.ParagraphSpacing = 0;
                if (bigFirst) { l.Spans.Add(Span("Ggy\n", 40)); l.Spans.Add(Span("gqy")); }
                else { l.Spans.Add(Span("gqy\n")); l.Spans.Add(Span("Ggy", 40)); }
            });
            var m14 = Metrics("LineSans", 14);
            var m40 = Metrics("LineSans", 40);
            var h14 = (float)Math.Round(m14.A + m14.D);
            var h40 = (float)Math.Round(m40.A + m40.D);

            Assert.Equal(2, label.Lines.Length);
            var big = label.Lines[bigFirst ? 0 : 1];
            var small = label.Lines[bigFirst ? 1 : 0];
            Assert.Equal(h40, big.Height);
            Assert.Equal(h14, small.Height);
            Assert.Equal(h40 + h14, label.MeasuredSize.Pixels.Height);

            // the line boxes touch and every line's ink stays inside its box
            var first = label.Lines[0].Bounds;
            var second = label.Lines[1].Bounds;
            Assert.Equal(first.Bottom, second.Top, 0.01);
            Assert.Equal(first.Height, label.Lines[0].Height, 0.01);
            Assert.Equal(second.Height, label.Lines[1].Height, 0.01);
            var top = Rows(pixels, 0, (int)Math.Round(first.Bottom));
            var bottom = Rows(pixels, (int)Math.Round(second.Top), H);
            var inkFirst = top.Split('-').Select(int.Parse).ToArray();
            var inkSecond = bottom.Split('-').Select(int.Parse).ToArray();
            Assert.True(inkFirst[0] >= first.Top && inkFirst[1] < first.Bottom, $"big first {bigFirst}: line 1 ink {top} outside {first}");
            Assert.True(inkSecond[0] >= second.Top && inkSecond[1] < second.Bottom, $"big first {bigFirst}: line 2 ink {bottom} outside {second}");
        }
    }

    [Fact]
    public void TwoFontsOnALine_RoundOfLargestAscentPlusLargestDescent()
    {
        // Orbitron 44 has the larger ascent, OpenSans 40 the larger descent: they share one baseline
        var o44 = Metrics("LineOrbitron", 44);
        var s40 = Metrics("LineSans", 40);
        var perSpan = Math.Max(Math.Round(o44.A + o44.D), Math.Round(s40.A + s40.D));
        var shared = (float)Math.Round(Math.Max(o44.A, s40.A) + Math.Max(o44.D, s40.D));
        Assert.True(o44.A > s40.A && s40.D > o44.D && perSpan != shared, $"fonts do not split: {o44} {s40}");

        var (pixels, label) = Draw(l =>
        {
            l.FontSize = 10;
            l.Spans.Add(Span("GAME", 44));
            l.Spans.Add(Span("gy", 40, "LineSans"));
        });
        Assert.Single(label.Lines);
        Assert.Equal(shared, label.Lines[0].Height);
        Assert.Equal(shared, label.MeasuredSize.Pixels.Height);
        // baseline = line bottom - the larger descent: the OpenSans descenders end inside the line
        var ink = Rows(pixels).Split('-').Select(int.Parse).ToArray();
        Assert.True(ink[0] >= 0 && ink[1] < shared, $"ink {ink[0]}-{ink[1]} outside the line of {shared}");
    }

    [Fact]
    public void Uniform_KeepsTheTallestLineForEveryLine()
    {
        // pinned with the layout before per-line heights became the default
        var (pixels, label) = Draw(l =>
        {
            l.FontFamily = "LineSans"; l.FontSize = 14; l.ParagraphSpacing = 0;
            l.Spans.Add(Span("Ggy\n", 40)); l.Spans.Add(Span("gqy"));
        }, uniform: true);
        var m40 = Metrics("LineSans", 40);
        var h40 = (float)Math.Round(m40.A + m40.D);
        Assert.Equal(h40, label.MeasuredLineHeight);
        Assert.Equal(2 * h40, label.MeasuredSize.Pixels.Height);
        Assert.Equal(h40, label.Lines[1].Bounds.Height, 0.01);
        Assert.Equal(UniformRowsBefore, $"{Rows(pixels, 0, (int)h40)} {Rows(pixels, (int)h40, H)}");
    }

    const string UniformRowsBefore = "21-53 54-106";

    /// <summary>The reference rows every engine pins (drawnui-cross 6bm).</summary>
    [Fact]
    public void ReferenceRows()
    {
        var over = Draw(l => { l.Text = "GAME OVER"; l.VerticalOptions = LayoutOptions.Center; });
        var mixed = Draw(l =>
        {
            l.FontSize = 14; l.ParagraphSpacing = 0; // the other engines have no paragraph spacing by default
            l.Spans.Add(Span("OVER\n"));
            l.Spans.Add(Span("GAME", 40));
        });
        var tall = Draw(l => { l.Text = "GAME"; l.LineHeight = 1.5; });
        var line0 = (int)Math.Round(mixed.Label.Lines[0].Bounds.Bottom);
        var report = $"over {Rows(over.Pixels, 0, 80)} h{over.Label.MeasuredSize.Pixels.Height}; " +
                     $"mixed {Rows(mixed.Pixels, 0, line0)} {Rows(mixed.Pixels, line0, H)} lines {mixed.Label.Lines[0].Height}+{mixed.Label.Lines[1].Height} h{mixed.Label.MeasuredSize.Pixels.Height}; " +
                     $"tall {Rows(tall.Pixels)} h{tall.Label.MeasuredSize.Pixels.Height}";
        Assert.Equal(ReferenceRowsExpected, report);
    }

    // the bottom rows follow the baseline and are the same in every engine; the top row can differ by one with the
    // rasterizer (row 43 of "GAME" is 162 here, under 128 in CanvasKit)
    const string ReferenceRowsExpected = "over 43-70 h96; mixed 5-14 29-57 lines 18+50 h68; tall 22-49 h71";
}
