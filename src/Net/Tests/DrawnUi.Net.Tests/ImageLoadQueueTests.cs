using System.Net;
using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The image load queue: at most MaxParallelLoads network loads at once, the rest wait by priority.
/// SkiaImageManager exposes RunningCount / QueuedCount; a query on a local file name is ignored for the disk read.
/// </summary>
// the image manager is process-wide: its queue, and its loading lock that any fast fling in another class takes
[Collection("Multithreaded")]
public class ImageLoadQueueTests
{
    [Fact]
    public async Task Queue_HandsSlotsByPriority_AndDropsCancelled()
    {
        var queue = new ImageLoadQueue(() => 2);
        await queue.WaitAsync(LoadPriority.Normal);
        await queue.WaitAsync(LoadPriority.Normal);

        var order = new List<string>();
        var cancelled = new CancellationTokenSource();
        var low = queue.WaitAsync(LoadPriority.Low).ContinueWith(_ => order.Add("low"));
        var normal = queue.WaitAsync(LoadPriority.Normal).ContinueWith(_ => order.Add("normal"));
        var gone = queue.WaitAsync(LoadPriority.High, cancelled.Token);
        var high = queue.WaitAsync(LoadPriority.High).ContinueWith(_ => order.Add("high"));

        Assert.Equal(2, queue.RunningCount);
        Assert.Equal(4, queue.QueuedCount);

        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gone);
        Assert.Equal(3, queue.QueuedCount);

        queue.Release();
        await high;
        queue.Release();
        await normal;
        queue.Release();
        await low;

        Assert.Equal(new[] { "high", "normal", "low" }, order);
        Assert.Equal(2, queue.RunningCount);
        Assert.Equal(0, queue.QueuedCount);
    }

    [Fact]
    public async Task PreloadImages_NetworkLoadsRunMaxParallelAtOnce()
    {
        var port = Random.Shared.Next(41000, 49000);
        using var server = new HttpListener();
        server.Prefixes.Add($"http://localhost:{port}/");
        server.Start();
        var png = SkiaPng();
        _ = Task.Run(async () =>
        {
            while (server.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await server.GetContextAsync(); }
                catch { return; }
                _ = Task.Run(async () =>
                {
                    await Task.Delay(150);
                    ctx.Response.ContentType = "image/png";
                    await ctx.Response.OutputStream.WriteAsync(png);
                    ctx.Response.Close();
                });
            }
        });

        var manager = SkiaImageManager.Instance;
        var previous = SkiaImageManager.MaxParallelLoads;
        SkiaImageManager.MaxParallelLoads = 3;
        try
        {
            var urls = Enumerable.Range(0, 8).Select(i => $"http://localhost:{port}/glass.png?queue={i}").ToList();
            var peakRunning = 0;
            var peakQueued = 0;
            var preload = manager.PreloadImages(urls, LoadPriority.Low);
            while (!preload.IsCompleted)
            {
                peakRunning = Math.Max(peakRunning, manager.RunningCount);
                peakQueued = Math.Max(peakQueued, manager.QueuedCount);
                await Task.Delay(2);
            }

            await preload;
            Assert.Equal(3, peakRunning);
            Assert.True(peakQueued >= 4, $"queued peak {peakQueued}"); // other tests share the static queue, so no upper bound
            Assert.All(urls, url => Assert.NotNull(manager.GetFromCache(url)));
            Assert.True(manager.RemoveFromCache(urls[0]));
            Assert.Null(manager.GetFromCache(urls[0]));
        }
        finally
        {
            SkiaImageManager.MaxParallelLoads = previous;
            server.Stop();
        }
    }

    [Fact]
    public async Task LocalFileWithQuery_LoadsFromDisk()
    {
        var file = Path.Combine(Path.GetTempPath(), $"drawnui-queue-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(file, SkiaPng());
        try
        {
            var urls = Enumerable.Range(0, 3).Select(i => $"{file}?queue={i}").ToList();
            await SkiaImageManager.Instance.PreloadImages(urls, LoadPriority.Low);

            Assert.All(urls, url => Assert.NotNull(SkiaImageManager.Instance.GetFromCache(url)));
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// A fast fling locks image loading for the whole app while it runs. A page closed mid-fling must leave it unlocked:
    /// the canvas drops a disposed control's animators without their stop, and every later load used to wait forever.
    /// </summary>
    [Fact]
    public void ScrollDisposedMidFling_UnlocksImageLoading()
    {
        var host = new DrawnUi.Testing.HeadlessCanvasHost(300, 600, scale: 1f, background: Colors.Black);
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Children = Enumerable.Range(0, 200).Select(i => (SkiaControl)new SkiaShape
                {
                    HeightRequest = 80,
                    BackgroundColor = Colors.DarkSlateBlue,
                    HorizontalOptions = LayoutOptions.Fill,
                }).ToList()
            }
        };
        host.AdvanceFrames(4);

        new DrawnUi.Testing.GestureRobot(host).Pan(150, 560, 150, 60, durationMs: 60, steps: 6); // ~8000 pt/s
        host.AdvanceFrames(2);
        Assert.True(SkiaImageManager.Instance.IsLoadingLocked, "the fling did not lock image loading");

        host.Dispose();

        Assert.False(SkiaImageManager.Instance.IsLoadingLocked, "image loading stayed locked after the scroll was disposed");
    }

    private static byte[] SkiaPng()
    {
        using var bitmap = new SKBitmap(4, 4);
        bitmap.Erase(SKColors.Orange);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
