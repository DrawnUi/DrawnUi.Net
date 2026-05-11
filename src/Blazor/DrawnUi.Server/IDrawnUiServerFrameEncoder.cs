using SkiaSharp;

namespace DrawnUi.Blazor.Server;

public interface IDrawnUiServerFrameEncoder
{
    string ContentType { get; }

    ValueTask<DrawnUiServerFrame> EncodeAsync(SKImage image, CancellationToken cancellationToken = default);
}