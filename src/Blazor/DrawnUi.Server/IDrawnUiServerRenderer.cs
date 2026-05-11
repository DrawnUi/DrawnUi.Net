using DrawnUi.Draw;

namespace DrawnUi.Blazor.Server;

public interface IDrawnUiServerRenderer
{
    ValueTask<DrawnUiServerFrame> RenderAsync(
        SkiaControl rootControl,
        int width,
        int height,
        Color backgroundColor,
        CancellationToken cancellationToken = default);
}