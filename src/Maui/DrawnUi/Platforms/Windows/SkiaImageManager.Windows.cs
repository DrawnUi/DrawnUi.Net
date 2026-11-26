namespace DrawnUi.Draw;

public partial class SkiaImageManager
{
    public async Task<SKBitmap> GetSKBitmapFromStreamImageSource(StreamImageSource streamImageSource)
    {
        var stream = await streamImageSource.Stream(default);
        var data = SKData.Create(stream);
        using var codec = SKCodec.Create(data);
        var skBitmap = new SKBitmap(codec.Info);

        //codec.(skBitmap.Info, skBitmap.GetPixels(), skBitmap.RowBytes, 0, 0);

        return skBitmap;
    }

    /*
    public static async Task<Microsoft.UI.Xaml.Media.ImageSource> LoadNativeImage(ImageSource source, CancellationToken token)
    {
        if (source == null)
            return null;

        try
        {

            var handler = source.GetHandler();
            return await handler.LoadImageAsync(source, token);

        }
        catch (Exception e)
        {
            SkiaImageManager.TraceLog($"[LoadImageOnPlatformAsync] {e}");
        }

        return null;
    }

     
    public async Task<SKBitmap> ImageSourceToSKBitmap(Microsoft.UI.Xaml.Media.ImageSource source)
    {
        WriteableBitmap writeableBitmap = null;

        switch (source)
        {
            case BitmapImage bitmapImage:
                // Convert BitmapImage to WriteableBitmap if necessary
                // This step depends on having the BitmapImage already loaded and available
                // One approach is to draw the BitmapImage onto a WriteableBitmap
                throw new NotImplementedException("Conversion from BitmapImage to WriteableBitmap needs implementation based on specific context.");

            case RenderTargetBitmap renderTargetBitmap:
                // Directly use RenderTargetBitmap if this is the case
                writeableBitmap = new WriteableBitmap(renderTargetBitmap.PixelWidth, renderTargetBitmap.PixelHeight);
                await renderTargetBitmap.RenderAsync(writeableBitmap);
                break;

            case WriteableBitmap bmp:
                writeableBitmap = bmp;
                break;

            default:
                throw new ArgumentException("Unsupported ImageSource type");
        }

        if (writeableBitmap == null)
        {
            throw new InvalidOperationException("Could not convert ImageSource to WriteableBitmap.");
        }

        // Access pixel data
        using var stream = writeableBitmap.PixelBuffer.AsStream();
        var pixels = new byte[stream.Length];
        await stream.ReadAsync(pixels, 0, pixels.Length);

        // Create SKBitmap
        var skBitmap = new SKBitmap(writeableBitmap.PixelWidth, writeableBitmap.PixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        skBitmap.InstallPixels(skBitmap.Info, pixels, writeableBitmap.PixelWidth * 4); // Assuming 4 bytes per pixel (BGRA8888)

        return skBitmap;
    }
    */

    public static async Task<string> HandleRedirects(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (response.IsSuccessStatusCode && (response.StatusCode == System.Net.HttpStatusCode.MovedPermanently || response.StatusCode == System.Net.HttpStatusCode.Redirect))
        {
            return response.Headers.Location.ToString();
        }
        return url;
    }

    static string PrefixNative = "native://";

    /// <summary>
    /// "native://name.png": an image the app packages itself (MAUI Resources/Images), written next to the app as
    /// name.scale-100.png .. name.scale-400.png; the one for the screen's scale is used.
    /// </summary>
    static SKBitmap LoadNativeImage(string name)
    {
        var dir = AppContext.BaseDirectory;
        var path = Path.Combine(dir, name);
        if (!File.Exists(path))
        {
            var stem = Path.GetFileNameWithoutExtension(name);
            var wanted = (int)Math.Round(Super.Screen.Density * 100);
            path = Directory.GetFiles(dir, $"{stem}.scale-*{Path.GetExtension(name)}")
                .Select(f => (file: f, scale: int.TryParse(Path.GetFileNameWithoutExtension(f).Substring(stem.Length + ".scale-".Length), out var s) ? s : 0))
                .OrderBy(x => x.scale >= wanted ? 0 : 1)
                .ThenBy(x => x.scale >= wanted ? x.scale : -x.scale)
                .Select(x => x.file)
                .FirstOrDefault();
        }

        if (path == null)
            return null;

        using var stream = File.OpenRead(path);
        return SKBitmap.Decode(stream);
    }

    public static async Task<SKBitmap> LoadImageOnPlatformAsync(ImageSource source, CancellationToken cancel)
    {
        if (source == null)
            return null;

        try
        {
            if (source is StreamImageSource streamSource)
            {
                using (var stream = await streamSource.Stream(cancel))
                {
                    return SKBitmap.Decode(stream);
                }
            }
            else
            if (source is UriImageSource nativeUri && nativeUri.Uri.OriginalString.StartsWith(PrefixNative, StringComparison.OrdinalIgnoreCase))
            {
                return LoadNativeImage(nativeUri.Uri.OriginalString.Substring(PrefixNative.Length).Trim('/'));
            }
            else
            if (source is UriImageSource uriSource)
            {
                return await LoadImageFromInternetAsync(uriSource, cancel);
            }
            else
            if (source is FileImageSource fileSource)
            {
                return await LoadFromFile(fileSource.File, cancel);
            }
            else
            {
                throw new NotImplementedException();
            }

            //var handler = source.GetHandler();
            //if (handler != null)
            //{
            //	//case of FileImageSourceHandler or FontImageSourceHandler
            //	var imageSource = await handler.LoadImageAsync(source, cancel);

            //	throw new NotImplementedException();
            //}

        }
        catch (TaskCanceledException)
        {

        }
        catch (Exception e)
        {
            Super.Log($"[LoadImageOnPlatformAsync] {e}");
        }

        return null;
    }



}
