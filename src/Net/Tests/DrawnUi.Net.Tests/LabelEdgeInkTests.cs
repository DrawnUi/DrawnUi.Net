using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Ink past a line's advance box at the label's edges (the hook of a "j", a synthetic italic descender or slant) is
/// drawn whole with every cache type, as without a cache.
/// </summary>
[Collection("FontRegistration")] // registers fonts: never in parallel with another class that does
public class LabelEdgeInkTests
{
    static LabelEdgeInkTests()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Path.Exists(Path.Combine(dir.FullName, ".git")))
            dir = dir.Parent;
        SkiaFontManager.Instance.RegisterFont("EdgeInkText", Path.Combine(dir!.FullName, @"Tests\PreviewTests\Resources\Fonts\OpenSans-Regular.ttf"));
        SkiaFontManager.Instance.Initialize();
    }

    /// <summary>Leftmost and rightmost lit columns.</summary>
    static (int Left, int Right) Ink(string text, bool italic, SkiaCacheType cache)
    {
        using var host = new HeadlessCanvasHost(400, 120, scale: 1f, background: Colors.Black);
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaLabel
                {
                    FontSize = 40, FontFamily = "EdgeInkText", TextColor = Colors.White, UseCache = cache,
                    Margin = new Thickness(50, 10, 0, 0),
                    Spans = { new TextSpan { Text = text, IsItalic = italic } }
                }
            }
        };
        host.AdvanceFrames(4);
        using var image = host.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        int left = int.MaxValue, right = -1;
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            if (bitmap.GetPixel(x, y).Red > 40)
            {
                left = Math.Min(left, x);
                right = Math.Max(right, x);
            }
        }
        return (left, right);
    }

    [Theory]
    [InlineData("jumps", false)]
    [InlineData("jumps", true)]
    [InlineData("Hello world", true)]
    public void CachedLabel_DrawsTheEdgeInkWhole(string text, bool italic)
    {
        var uncached = Ink(text, italic, SkiaCacheType.None);

        Assert.Equal(uncached, Ink(text, italic, SkiaCacheType.Operations));
        Assert.Equal(uncached, Ink(text, italic, SkiaCacheType.Image));
    }
}
