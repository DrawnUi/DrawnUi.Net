using SkiaSharp;

namespace DrawnUi.Blazor.Server;

public sealed class PngDrawnUiServerFrameEncoder : IDrawnUiServerFrameEncoder
{
    public string ContentType => "image/png";

    public ValueTask<DrawnUiServerFrame> EncodeAsync(SKImage image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        if (data is null)
        {
            throw new InvalidOperationException("DrawnUI server frame encoding returned no image data.");
        }

        return ValueTask.FromResult(new DrawnUiServerFrame(ContentType, data.ToArray()));
    }

    public ValueTask<DrawnUiServerFrame> EncodeAsync(
        SKImage image,
        DrawnUiServerFrameEncodingOptions options,
        CancellationToken cancellationToken = default)
        => EncodeAsync(image, cancellationToken);
}