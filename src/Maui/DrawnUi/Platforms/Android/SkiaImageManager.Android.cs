using SkiaSharp.Views.Android;

namespace DrawnUi.Draw;

public partial class SkiaImageManager
{
    /*
     if (handler is IPlatformViewHandler platformViewHandler && platformViewHandler.ViewController is not null)
       {
        var provider = handler.GetRequiredService<IImageSourceServiceProvider>();
        platformViewHandler.ViewController.View?.UpdateBackground(page, provider);
       }
     */

    public static bool UseGlide = true;

    static string PrefixNative = "native://";

    public static async Task<SKBitmap> LoadImageOnPlatformAsync(ImageSource source, CancellationToken cancel)
    {
        if (source == null)
            return null;

        try
        {
            if (source is UriImageSource uriSource)
            {
                var url = uriSource.Uri.AbsoluteUri;
                if (url.Contains(PrefixNative))
                {
                    var native = url.Replace(PrefixNative, string.Empty).Trim('/');
                    source = new FileImageSource() { File = native };
                    var nativeFile = await LoadViaMauiAsync(source);
                    if (nativeFile != null)
                    {
                        SkiaImageManager.TraceLog($"[LoadImageOnPlatformAsync] loaded {source} ToSKBitmap");
                        return nativeFile;
                    }
                }
                else
                {
                    if (UseGlide)
                    {
                        var glide = await source.LoadOriginalViaGlideWithRetry(Platform.CurrentActivity, cancel);
                        if (glide != null)
                        {
                            SkiaImageManager.TraceLog($"[LoadImageOnPlatformAsync-GLIDE] loaded {source} ToSKBitmap");
                            return glide.ToSKBitmap();
                        }

                        SkiaImageManager.TraceLog($"[LoadImageOnPlatformAsync-GLIDE] loaded NULL for {source}");
                        return null;
                    }
                    else
                    {
                        return await LoadImageFromInternetAsync(uriSource, cancel);
                    }
                }
            }

            if (source is FileImageSource fileSource)
            {
                return await LoadFromFile(fileSource.File, cancel);
            }

            if (source is StreamImageSource streamSource)
            {
                using var stream = await streamSource.Stream(cancel);
                return SKBitmap.Decode(stream);
            }

            var bitmap = await LoadViaMauiAsync(source);
            if (bitmap != null)
            {
                SkiaImageManager.TraceLog($"[LoadImageOnPlatformAsync] loaded {source} ToSKBitmap");
                return bitmap;
            }
        }
        catch (TaskCanceledException)
        {
            SkiaImageManager.TraceLog($"[LoadImageOnPlatformAsync] TaskCanceledException for {source}");
        }
        catch (System.Exception e)
        {
            SkiaImageManager.TraceLog($"[LoadImageOnPlatformAsync] {e}");
        }

        SkiaImageManager.TraceLog($"[LoadImageOnPlatformAsync] loaded NULL for {source}");
        return null;
    }

    /// <summary>
    /// Loads an image source with the MAUI image services of the app (app resources, fonts..)
    /// </summary>
    static async Task<SKBitmap> LoadViaMauiAsync(ImageSource source)
    {
        var context = Application.Current?.Handler?.MauiContext;
        if (context == null)
            return null;

        using var result = await source.GetPlatformImageAsync(context);
        var drawable = result?.Value;
        if (drawable == null)
            return null;

        if (drawable is Android.Graphics.Drawables.BitmapDrawable bitmapDrawable && bitmapDrawable.Bitmap != null)
            return bitmapDrawable.Bitmap.ToSKBitmap();

        var width = Math.Max(1, drawable.IntrinsicWidth);
        var height = Math.Max(1, drawable.IntrinsicHeight);
        using var androidBitmap = Android.Graphics.Bitmap.CreateBitmap(width, height, Android.Graphics.Bitmap.Config.Argb8888);
        using var canvas = new Android.Graphics.Canvas(androidBitmap);
        drawable.SetBounds(0, 0, width, height);
        drawable.Draw(canvas);
        return androidBitmap.ToSKBitmap();
    }
}
