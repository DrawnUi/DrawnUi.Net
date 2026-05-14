using SkiaSharp;

namespace DrawnUi.Blazor.Server;

public interface IDrawnUiServerFrameEncoder
{
    string ContentType { get; }

    ValueTask<DrawnUiServerFrame> EncodeAsync(SKImage image, CancellationToken cancellationToken = default);

    ValueTask<DrawnUiServerFrame> EncodeAsync(
        SKImage image,
        DrawnUiServerFrameEncodingOptions options,
        CancellationToken cancellationToken = default)
        => EncodeAsync(image, cancellationToken);
}