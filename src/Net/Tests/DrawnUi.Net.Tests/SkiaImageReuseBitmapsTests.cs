using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// With <see cref="SkiaImageManager.ReuseBitmaps"/> on, every image showing the same source draws the one decoded
/// bitmap the cache holds: an image that finds the source already cached does not get its own copy of the pixels.
/// </summary>
public class SkiaImageReuseBitmapsTests
{
    [Fact]
    public void SameSource_SharesTheCachedPixels()
    {
        var file = Path.Combine(Path.GetTempPath(), $"reuse-{Guid.NewGuid():N}.png");
        using (var bitmap = new SKBitmap(64, 64))
        {
            bitmap.Erase(SKColors.Green);
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(file, data.ToArray());
        }

        var reuse = SkiaImageManager.ReuseBitmaps;
        SkiaImageManager.ReuseBitmaps = true;
        try
        {
            using var host = new HeadlessCanvasHost(300, 100, 1f, Colors.Black);
            var row = new SkiaRow();
            host.Canvas.Content = row;

            // the first image decodes the file and fills the cache
            var first = new SkiaImage { WidthRequest = 20, HeightRequest = 20, Source = file };
            row.AddSubView(first);
            for (var i = 0; i < 100 && first.LoadedSource == null; i++)
            {
                host.RenderFrame();
                Thread.Sleep(20);
            }

            var cached = SkiaImageManager.Instance.GetFromCacheInternal(file);
            Assert.NotNull(cached);

            // the next ones find it cached
            var next = Enumerable.Range(0, 3)
                .Select(_ => new SkiaImage { WidthRequest = 20, HeightRequest = 20, Source = file })
                .ToList();
            foreach (var image in next)
                row.AddSubView(image);
            for (var i = 0; i < 100 && next.Any(x => x.LoadedSource == null); i++)
            {
                host.RenderFrame();
                Thread.Sleep(20);
            }

            foreach (var image in next)
            {
                var loaded = image.LoadedSource;
                Assert.NotNull(loaded);
                var pixels = loaded.Image != null ? loaded.Image.PeekPixels()?.GetPixels() : loaded.Bitmap?.GetPixels();
                Assert.Equal(cached.GetPixels(), pixels);
            }
        }
        finally
        {
            SkiaImageManager.ReuseBitmaps = reuse;
            SkiaImageManager.Instance.RemoveFromCache(file);
            File.Delete(file);
        }
    }
}
