using DrawnUi.Draw;
using SkiaSharp;

namespace DrawnUi.Blazor.Server;

public sealed class DrawnUiServerFrameEncoder : IDrawnUiServerFrameEncoder
{
    private const int DefaultJpegQuality = 85;

    public string ContentType => GetContentType(DrawnUiServerFrameFormat.Jpeg);

    public ValueTask<DrawnUiServerFrame> EncodeAsync(SKImage image, CancellationToken cancellationToken = default)
        => EncodeAsync(image, DrawnUiServerFrameEncodingOptions.Default, cancellationToken);

    public ValueTask<DrawnUiServerFrame> EncodeAsync(
        SKImage image,
        DrawnUiServerFrameEncodingOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(options.Format switch
        {
            DrawnUiServerFrameFormat.Png => EncodePng(image),
            _ => EncodeJpeg(image, options.BackgroundColor, options.JpegQuality)
        });
    }

    private static DrawnUiServerFrame EncodePng(SKImage image)
    {
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        if (data is null)
        {
            throw new InvalidOperationException("DrawnUI server frame encoding returned no image data.");
        }

        return new DrawnUiServerFrame(GetContentType(DrawnUiServerFrameFormat.Png), data.ToArray());
    }

    private static DrawnUiServerFrame EncodeJpeg(SKImage image, Color backgroundColor, int jpegQuality)
    {
        var quality = NormalizeJpegQuality(jpegQuality);

        if (backgroundColor.Alpha >= 1f)
        {
            using var jpegData = image.Encode(SKEncodedImageFormat.Jpeg, quality);
            if (jpegData is null)
            {
                throw new InvalidOperationException("DrawnUI server frame encoding returned no image data.");
            }

            return new DrawnUiServerFrame(GetContentType(DrawnUiServerFrameFormat.Jpeg), jpegData.ToArray());
        }

        var opaqueBackground = ResolveOpaqueJpegBackground(backgroundColor);
        using var surface = SKSurface.Create(new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        if (surface is null)
        {
            throw new InvalidOperationException("DrawnUI server frame encoding could not allocate a JPEG surface.");
        }

        var canvas = surface.Canvas;
        canvas.Clear(ToSkColor(opaqueBackground));
        canvas.DrawImage(image, 0, 0);
        canvas.Flush();

        using var flattened = surface.Snapshot();
        using var data = flattened.Encode(SKEncodedImageFormat.Jpeg, quality);
        if (data is null)
        {
            throw new InvalidOperationException("DrawnUI server frame encoding returned no image data.");
        }

        return new DrawnUiServerFrame(GetContentType(DrawnUiServerFrameFormat.Jpeg), data.ToArray());
    }

    private static Color ResolveOpaqueJpegBackground(Color backgroundColor)
    {
        if (backgroundColor.Alpha <= 0)
        {
            return Colors.White;
        }

        var alpha = backgroundColor.Alpha;
        var inverseAlpha = 1f - alpha;
        return new Color(
            backgroundColor.Red * alpha + inverseAlpha,
            backgroundColor.Green * alpha + inverseAlpha,
            backgroundColor.Blue * alpha + inverseAlpha,
            1f);
    }

    private static int NormalizeJpegQuality(int jpegQuality)
    {
        if (jpegQuality < 0)
        {
            return 0;
        }

        if (jpegQuality > 100)
        {
            return 100;
        }

        return jpegQuality;
    }

    private static SKColor ToSkColor(Color color)
    {
        return new SKColor(
            (byte)Math.Clamp((int)Math.Round(color.Red * 255f), 0, 255),
            (byte)Math.Clamp((int)Math.Round(color.Green * 255f), 0, 255),
            (byte)Math.Clamp((int)Math.Round(color.Blue * 255f), 0, 255),
            255);
    }

    private static string GetContentType(DrawnUiServerFrameFormat format)
    {
        return format == DrawnUiServerFrameFormat.Png
            ? "image/png"
            : "image/jpeg";
    }
}