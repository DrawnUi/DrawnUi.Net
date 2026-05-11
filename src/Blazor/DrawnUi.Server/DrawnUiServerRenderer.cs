using DrawnUi.Draw;
using SkiaSharp;

namespace DrawnUi.Blazor.Server;

public sealed class DrawnUiServerRenderer : IDrawnUiServerRenderer
{
    private static int _initialized;
    private readonly IDrawnUiServerFrameEncoder _encoder;

    public DrawnUiServerRenderer(IDrawnUiServerFrameEncoder encoder)
    {
        _encoder = encoder;

        if (Interlocked.Exchange(ref _initialized, 1) == 0)
        {
            Super.Init();
        }
    }

    public async ValueTask<DrawnUiServerFrame> RenderAsync(
        SkiaControl rootControl,
        int width,
        int height,
        Color backgroundColor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootControl);

        using var host = new HeadlessCanvasHost(width, height, backgroundColor);
        host.Canvas.Children = new List<SkiaControl> { rootControl };
        host.Render();

        using var image = host.Snapshot();
        return await _encoder.EncodeAsync(image, cancellationToken);
    }
}